using System.Collections.Generic;

namespace LOP.EditorTools.Automation
{
    /// <summary>재생을 켠다. 기본은 입구 씬부터 — 열린 씬(맵 작업 중일 수 있다)에서 시작하면 앱 흐름이 안 선다.</summary>
    public sealed class PlayEnterStep : IStepHandler
    {
        public string Op => "play_enter";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 180;
        public void Validate(ScenarioStep step, List<string> errors) { }

        public StepResult Tick(StepContext ctx)
        {
            if (ctx.Driver.IsPlaying) return StepResult.Done(ctx.Scratch["requested"] == null ? "이미 재생 중" : null);
            //  요청은 한 번 — 이 틱 뒤 도메인이 다시 불러와져도 scratch가 파일에 남아 두 번 켜지 않는다.
            if (ctx.Scratch["requested"] != null)
            {
                ctx.Waiting = "재생 시작 기다림";
                return StepResult.Running;
            }

            if (ctx.Step.Bool("from_entrance", true))
            {
                ctx.RememberRestore(RestoreKinds.PlayStartScene, string.Empty, ctx.Driver.PlayModeStartScene, hadValue: true);
                ctx.Driver.PlayModeStartScene = ctx.Driver.FirstBuildScene;
            }
            ctx.Driver.SetPlaying(true);
            ctx.Scratch["requested"] = true;
            return StepResult.Running;
        }
    }

    public sealed class PlayExitStep : IStepHandler
    {
        public string Op => "play_exit";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 60;
        public void Validate(ScenarioStep step, List<string> errors) { }

        public StepResult Tick(StepContext ctx)
        {
            if (!ctx.Driver.IsPlaying) return StepResult.Done();
            if (ctx.Scratch["requested"] == null)
            {
                ctx.Driver.SetPlaying(false);
                ctx.Scratch["requested"] = true;
            }
            ctx.Waiting = "재생 끄기 기다림";
            return StepResult.Running;
        }
    }

    /// <summary>앱 상태(Boot·FrontEnd·InMatch·PostMatch)와/또는 씬이 맞을 때까지.</summary>
    public sealed class WaitStateStep : IStepHandler
    {
        public string Op => "wait_state";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 60;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            if (step.Str("state") == null && step.Str("scene") == null) errors.Add($"{step}: state나 scene 중 하나는 있어야 한다");
        }

        public StepResult Tick(StepContext ctx)
        {
            string state = ctx.Step.Str("state");
            string scene = ctx.Step.Str("scene");
            string now = ctx.Driver.AppState;
            bool stateOk = state == null || now == state;
            bool sceneOk = scene == null || ctx.Driver.IsSceneLoaded(scene);
            if (stateOk && sceneOk) return StepResult.Done(now);

            ctx.Waiting = $"앱 상태={now ?? "-"}" + (scene != null ? $" {scene} 씬={(sceneOk ? "있음" : "없음")}" : string.Empty);
            return StepResult.Running;
        }
    }

    /// <summary>
    /// 입구에서 로비까지. 저장된 자격증명이 있으면 조용히 지나가고, 로그인 창이 뜨면 "입장"(게스트)을 한 번 누른다.
    /// 앱이 FrontEnd이고 Lobby 씬이 섰고 PLAY를 누를 수 있으면 끝.
    /// </summary>
    public sealed class BootToLobbyStep : IStepHandler
    {
        public const string GuestButton = "guest-login";
        public const string PlayButton = "play-button";

        public string Op => "boot_to_lobby";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 120;
        public void Validate(ScenarioStep step, List<string> errors) { }

        public StepResult Tick(StepContext ctx)
        {
            if (!ctx.Driver.IsPlaying) return StepResult.Fail("재생 중이 아니다 — play_enter를 먼저");

            bool lobby = ctx.Driver.AppState == "FrontEnd" && ctx.Driver.IsSceneLoaded("Lobby");
            var play = ctx.Driver.FindElement(new UiSelector { Name = PlayButton });
            if (lobby && play != null && play.Clickable)
                return StepResult.Done(ctx.Scratch["guest"] != null ? "게스트 로그인" : "자동 로그인");

            var guest = ctx.Driver.FindElement(new UiSelector { Name = GuestButton });
            //  한 번만 — 실패하면 창이 오류 문구와 함께 남는데, 계속 누르면 요청만 쌓인다.
            if (guest != null && guest.Clickable && ctx.Scratch["guest"] == null)
            {
                if (!ctx.Driver.Click(new UiSelector { Name = GuestButton }, out var why)) return StepResult.Fail(why);
                ctx.Scratch["guest"] = true;
            }

            ctx.Waiting = $"앱 상태={ctx.Driver.AppState ?? "-"} 로비={lobby} 로그인 창={(guest?.Visible == true ? "보임" : "없음")}";
            return StepResult.Running;
        }
    }

    /// <summary>
    /// 로비에서 게임·맵·큐를 고른다 — 매칭 화면 커맨드(드롭다운이 부르는 그 커맨드)를 부른다. 실제 매칭은 다음 단계의 PLAY 클릭이 시작한다.
    /// </summary>
    public sealed class SelectGameStep : IStepHandler
    {
        public string Op => "select_game";
        //  로비 스코프·게임 목록은 로비 진입 직후 아직 안 채워졌을 수 있다 — 기다릴 몫을 10s보다 넉넉히 둔다.
        public double DefaultTimeoutSeconds(ScenarioStep step) => 30;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            if (step.Int("game_mode_id") == null) errors.Add($"{step}: game_mode_id(정수)가 없다");
            string queue = step.Str("queue");
            if (queue != null && queue != "casual" && queue != "ranked") errors.Add($"{step}: queue는 casual·ranked 중 하나");
        }

        public StepResult Tick(StepContext ctx)
        {
            //  강제 모드가 켜져 있으면 고른 것과 다른 판이 열린다 — 엉뚱한 판을 조용히 도는 것보다 멈추는 게 낫다.
            if (ctx.Driver.HasPref(ForceGameModeMenu.ModeKey))
                return StepResult.Fail("다음 매치 게임 모드 강제가 켜져 있다 — LOP/Debug/다음 매치 게임 모드 강제/끄기");

            int gameModeId = ctx.Step.Int("game_mode_id").Value;
            bool ranked = ctx.Step.Str("queue") == "ranked";
            if (ctx.Driver.SelectGame(gameModeId, ctx.Step.Int("map_id"), ranked, out var why)) return StepResult.Done($"게임 {gameModeId}");

            //  로비 스코프·게임 목록이 아직 안 채워졌으면(로비 진입 직후) 기다린다 — 시간 제한이 실패를 낸다.
            //  맵이 없다는 건 시나리오 설정 실수라 기다릴 이유가 없어 바로 실패시킨다.
            if (why != null && (why.Contains("스코프") || why.Contains("목록에")))
            {
                ctx.Waiting = why;
                return StepResult.Running;
            }
            return StepResult.Fail(why);
        }
    }
}
