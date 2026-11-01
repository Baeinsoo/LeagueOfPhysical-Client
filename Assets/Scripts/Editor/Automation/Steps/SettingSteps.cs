using System.Collections.Generic;

namespace LOP.EditorTools.Automation
{
    /// <summary>Flappy 자동 비행(<see cref="FlappyAutoFlapSystem"/>)을 켜고 끈다. 에디터 전역 설정이라 실행 끝에 되돌린다.</summary>
    public sealed class AutoFlapStep : IStepHandler
    {
        public string Op => "auto_flap";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 5;
        public void Validate(ScenarioStep step, List<string> errors) { }

        public StepResult Tick(StepContext ctx)
        {
            Set(ctx, FlappyAutoFlapSystem.EditorPrefsKey, ctx.Step.Bool("on", true));
            if (ctx.Step.Has("dash")) Set(ctx, FlappyAutoFlapSystem.DashEditorPrefsKey, ctx.Step.Bool("dash", true));
            return StepResult.Done();
        }

        private static void Set(StepContext ctx, string key, bool value)
        {
            bool had = ctx.Driver.HasPref(key);
            ctx.RememberRestore(RestoreKinds.BoolPref, key, had && ctx.Driver.GetBoolPref(key, false) ? "true" : "false", had);
            ctx.Driver.SetBoolPref(key, value);
        }
    }

    /// <summary>접속 환경(local·local-k8s·dev)을 바꾼다. 재생 전에만 — 앱은 부팅 때 한 번 읽는다. 실행 끝에 되돌린다.</summary>
    public sealed class SetEnvironmentStep : IStepHandler
    {
        public string Op => "set_environment";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 5;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            if (string.IsNullOrEmpty(step.Str("env"))) errors.Add($"{step}: env가 없다");
        }

        public StepResult Tick(StepContext ctx)
        {
            if (ctx.Driver.IsPlaying) return StepResult.Fail("재생 중엔 환경을 못 바꾼다 — play_enter 앞에 둔다");

            string key = EnvironmentSettings.EditorPrefsKey;
            bool had = ctx.Driver.HasPref(key);
            ctx.RememberRestore(RestoreKinds.Environment, key, had ? ctx.Driver.GetStringPref(key, null) : null, had);
            ctx.Driver.SetStringPref(key, ctx.Step.Str("env"));
            ctx.Driver.ReloadEnvironment();
            return StepResult.Done(ctx.Step.Str("env"));
        }
    }
}
