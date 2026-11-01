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
    }
}
