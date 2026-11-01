using System.Collections.Generic;
using System.Linq;
using LOP.EditorTools;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>play_enter·play_exit·wait_state·boot_to_lobby·select_game(spec §4.2).</summary>
    public class AppStepTests
    {
        private StepTestKit _kit;

        [SetUp]
        public void SetUp() => _kit = new StepTestKit();

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void play_enter는_입구_씬을_시작_씬으로_걸고_재생을_한_번만_요청한다()
        {
            _kit.Driver.PlayModeStartScene = "Assets/Art/Scenes/FlappyRaceMap.unity";
            var step = new PlayEnterStep();

            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("play_enter")).Status);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("play_enter")).Status);
            CollectionAssert.AreEqual(new[] { true }, _kit.Driver.PlayRequests);
            Assert.AreEqual("Assets/Scenes/Entrance.unity", _kit.Driver.PlayModeStartScene);

            _kit.Driver.IsPlaying = true;
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("play_enter")).Status);

            var restore = _kit.State.Restores.Single();
            Assert.AreEqual(RestoreKinds.PlayStartScene, restore.Kind);
            Assert.AreEqual("Assets/Art/Scenes/FlappyRaceMap.unity", restore.Value);
        }

        [Test]
        public void play_enter는_from_entrance가_false면_시작_씬을_건드리지_않는다()
        {
            _kit.Driver.PlayModeStartScene = "X.unity";

            new PlayEnterStep().Tick(_kit.Ctx("play_enter", "{\"from_entrance\":false}"));

            Assert.AreEqual("X.unity", _kit.Driver.PlayModeStartScene);
            Assert.IsEmpty(_kit.State.Restores);
            CollectionAssert.AreEqual(new[] { true }, _kit.Driver.PlayRequests);
        }

        [Test]
        public void play_exit는_재생을_한_번_끄고_꺼질_때까지_기다린다()
        {
            _kit.Driver.IsPlaying = true;
            var step = new PlayExitStep();

            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("play_exit")).Status);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("play_exit")).Status);
            CollectionAssert.AreEqual(new[] { false }, _kit.Driver.PlayRequests);

            _kit.Driver.IsPlaying = false;
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("play_exit")).Status);
        }

        [Test]
        public void wait_state는_상태와_씬이_둘_다_맞아야_끝난다()
        {
            const string args = "{\"state\":\"PostMatch\",\"scene\":\"Result\"}";
            var step = new WaitStateStep();
            _kit.Driver.AppState = "InMatch";

            var ctx = _kit.Ctx("wait_state", args);
            Assert.AreEqual(StepStatus.Running, step.Tick(ctx).Status);
            StringAssert.Contains("InMatch", ctx.Waiting);

            _kit.Driver.AppState = "PostMatch";
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("wait_state", args)).Status);

            _kit.Driver.LoadedScenes.Add("Result");
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("wait_state", args)).Status);

            var errors = new List<string>();
            step.Validate(StepTestKit.Step("wait_state"), errors);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void boot_to_lobby는_로그인_창이_뜨면_입장을_한_번만_누르고_로비에서_끝난다()
        {
            _kit.Driver.IsPlaying = true;
            _kit.Driver.AppState = "Boot";
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element(BootToLobbyStep.GuestButton));
            var step = new BootToLobbyStep();

            for (int i = 0; i < 3; i++) Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("boot_to_lobby")).Status);
            CollectionAssert.AreEqual(new[] { "guest-login" }, _kit.Driver.Clicks);

            _kit.Driver.Elements.Clear();
            _kit.Driver.AppState = "FrontEnd";
            _kit.Driver.LoadedScenes.Add("Lobby");
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element(BootToLobbyStep.PlayButton));
            var done = step.Tick(_kit.Ctx("boot_to_lobby"));

            Assert.AreEqual(StepStatus.Done, done.Status);
            Assert.AreEqual("게스트 로그인", done.Detail);
        }

        [Test]
        public void boot_to_lobby는_경기장_접속_실패_대화상자가_뜨면_다시_시도를_누른다()
        {
            // Task 6 S8에서 재현: 재생 진입 직후 첫 서버 접속 확인이 실패하면(콜드스타트) "경기장에 닿지 않아요"
            // 대화상자가 뜬다 — boot_to_lobby가 이걸 모르면 로그인 창도 안 뜨고 120초를 그냥 날린다.
            _kit.Driver.IsPlaying = true;
            _kit.Driver.AppState = "Boot";
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element(BootToLobbyStep.RetryButton, "다시 시도"));
            var step = new BootToLobbyStep();

            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("boot_to_lobby")).Status);
            CollectionAssert.AreEqual(new[] { BootToLobbyStep.RetryButton }, _kit.Driver.Clicks);

            _kit.Driver.Elements.Clear();
            _kit.Driver.AppState = "FrontEnd";
            _kit.Driver.LoadedScenes.Add("Lobby");
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element(BootToLobbyStep.PlayButton));
            var done = step.Tick(_kit.Ctx("boot_to_lobby"));

            Assert.AreEqual(StepStatus.Done, done.Status);
        }

        [Test]
        public void boot_to_lobby는_접속_실패_대화상자_다시_시도가_상한을_넘기면_실패한다()
        {
            // L3: 서버가 정말 죽었으면 대화상자가 매번 다시 뜬다 — 상한 없이 계속 누르면 120s 시간 제한이
            // "앱 상태=Boot 로비=False 로그인 창=없음"이라는 무의미한 문구로 진짜 장애를 가린다.
            _kit.Driver.IsPlaying = true;
            _kit.Driver.AppState = "Boot";
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element(BootToLobbyStep.RetryButton, "다시 시도"));
            var step = new BootToLobbyStep();

            StepResult last = default;
            for (int i = 0; i < BootToLobbyStep.MaxRetries; i++) last = step.Tick(_kit.Ctx("boot_to_lobby"));
            Assert.AreEqual(StepStatus.Running, last.Status);
            Assert.AreEqual(BootToLobbyStep.MaxRetries, _kit.Driver.Clicks.Count);

            var failed = step.Tick(_kit.Ctx("boot_to_lobby"));
            Assert.AreEqual(StepStatus.Failed, failed.Status);
            StringAssert.Contains("다시 시도", failed.Detail);
            StringAssert.Contains(BootToLobbyStep.MaxRetries.ToString(), failed.Detail);
            //  상한을 넘겼으면 더는 누르지 않는다.
            Assert.AreEqual(BootToLobbyStep.MaxRetries, _kit.Driver.Clicks.Count);
        }

        [Test]
        public void boot_to_lobby는_재생_중이_아니면_바로_실패한다()
        {
            Assert.AreEqual(StepStatus.Failed, new BootToLobbyStep().Tick(_kit.Ctx("boot_to_lobby")).Status);
        }

        [Test]
        public void select_game은_강제_게임_모드가_켜져_있으면_멈춘다()
        {
            _kit.Driver.Prefs[ForceGameModeMenu.ModeKey] = 10;

            var result = new SelectGameStep().Tick(_kit.Ctx("select_game", "{\"game_mode_id\":6}"));

            Assert.AreEqual(StepStatus.Failed, result.Status);
            StringAssert.Contains("강제", result.Detail);
            Assert.IsNull(_kit.Driver.Selected);
        }

        [Test]
        public void select_game은_게임_맵_큐를_함께_넘긴다()
        {
            Assert.AreEqual(StepStatus.Done, new SelectGameStep().Tick(_kit.Ctx("select_game", "{\"game_mode_id\":6,\"map_id\":2,\"queue\":\"ranked\"}")).Status);
            Assert.AreEqual((6, (int?)2, true), _kit.Driver.Selected.Value);

            new SelectGameStep().Tick(_kit.Ctx("select_game", "{\"game_mode_id\":6}"));
            Assert.AreEqual((6, (int?)null, false), _kit.Driver.Selected.Value);
        }

        [Test]
        public void select_game_검증은_game_mode_id와_queue를_본다()
        {
            var errors = new List<string>();

            new SelectGameStep().Validate(StepTestKit.Step("select_game"), errors);
            new SelectGameStep().Validate(StepTestKit.Step("select_game", "{\"game_mode_id\":6,\"queue\":\"solo\"}"), errors);
            new SelectGameStep().Validate(StepTestKit.Step("select_game", "{\"game_mode_id\":6}"), errors);

            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("game_mode_id", errors[0]);
            StringAssert.Contains("queue", errors[1]);
        }

        [Test]
        public void select_game은_로비_스코프나_게임_목록이_아직_없으면_시간_제한까지_기다린다()
        {
            // L2: 로비 진입 직후엔 스코프·목록이 비동기로 채워질 수 있다 — 바로 실패시키면 안 된다.
            _kit.Driver.SelectGameWhy = "로비 스코프가 없다 — 로비에서만 고를 수 있다";
            var waiting = new SelectGameStep().Tick(_kit.Ctx("select_game", "{\"game_mode_id\":6}"));
            Assert.AreEqual(StepStatus.Running, waiting.Status);
            Assert.IsNull(_kit.Driver.Selected);

            _kit.Driver.SelectGameWhy = "게임 6이 고를 수 있는 목록에 없다";
            var stillWaiting = new SelectGameStep().Tick(_kit.Ctx("select_game", "{\"game_mode_id\":6}"));
            Assert.AreEqual(StepStatus.Running, stillWaiting.Status);
            Assert.IsNull(_kit.Driver.Selected);
        }

        [Test]
        public void select_game은_맵이_없으면_기다리지_않고_바로_실패한다()
        {
            // L2: 맵 없음은 시나리오 설정 실수라 기다릴 이유가 없다.
            _kit.Driver.SelectGameWhy = "게임 6에 맵 99이 없다";

            var result = new SelectGameStep().Tick(_kit.Ctx("select_game", "{\"game_mode_id\":6,\"map_id\":99}"));

            Assert.AreEqual(StepStatus.Failed, result.Status);
        }
    }
}
