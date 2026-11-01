using System.Collections.Generic;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    public sealed class ScreenshotStep : IStepHandler
    {
        public string Op => "screenshot";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 10;
        public void Validate(ScenarioStep step, List<string> errors) { }

        public StepResult Tick(StepContext ctx)
        {
            var (relative, absolute) = ctx.Recorder.NextShotPath(ctx.Step.Str("as") ?? "shot");
            int width = ctx.Step.Int("width") ?? 1280;
            int height = ctx.Step.Int("height") ?? 720;
            if (!ctx.Driver.SaveScreenshot(absolute, width, height, out var why)) return StepResult.Fail(why);
            ctx.Recorder.AddScreenshot(relative);
            return StepResult.Done(relative);
        }
    }

    public sealed class LogStep : IStepHandler
    {
        public string Op => "log";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 5;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            if (string.IsNullOrEmpty(step.Str("message"))) errors.Add($"{step}: message가 없다");
        }

        public StepResult Tick(StepContext ctx)
        {
            ctx.Recorder.Log("[log] " + ctx.Step.Str("message"));
            return StepResult.Done(ctx.Step.Str("message"));
        }
    }

    public sealed class WaitStep : IStepHandler
    {
        public string Op => "wait";
        public double DefaultTimeoutSeconds(ScenarioStep step) => (step.Num("seconds") ?? 0) + 10;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            if (!(step.Num("seconds") > 0)) errors.Add($"{step}: seconds(0보다 큰 수)가 없다");
        }

        public StepResult Tick(StepContext ctx)
        {
            double seconds = ctx.Step.Num("seconds").Value;
            if (ctx.ElapsedSeconds >= seconds) return StepResult.Done();
            ctx.Waiting = $"{seconds}초 기다림";
            return StepResult.Running;
        }
    }

    /// <summary>
    /// 키 하나 또는 화면 한 점을 hold_ms 동안 눌렀다 뗀다 — Input System 장치에 상태 이벤트를 넣는, 진짜 장치와 같은 길(spec §8.2).
    /// 누름과 뗌을 다른 프레임에 둔다: 한 번에 넣으면 "이번 프레임에 눌렸나"를 보는 코드가 놓친다.
    /// </summary>
    public sealed class InputTapStep : IStepHandler
    {
        public string Op => "input_tap";
        public double DefaultTimeoutSeconds(ScenarioStep step) => (step.Int("hold_ms") ?? 120) / 1000.0 + 5;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            bool key = step.Str("key") != null;
            bool touch = step.Num("x") != null && step.Num("y") != null;
            if (key == touch) errors.Add($"{step}: key 하나 또는 x·y(화면 픽셀) 하나만");
        }

        public StepResult Tick(StepContext ctx)
        {
            double holdSeconds = (ctx.Step.Int("hold_ms") ?? 120) / 1000.0;
            if (ctx.Scratch["down_at"] == null)
            {
                if (!Press(ctx, true, out var why)) return StepResult.Fail(why);
                ctx.Scratch["down_at"] = ctx.ElapsedSeconds;
                return StepResult.Running;
            }
            if (ctx.ElapsedSeconds - (double)ctx.Scratch["down_at"] < holdSeconds)
            {
                ctx.Waiting = "누르고 있음";
                return StepResult.Running;
            }
            return Press(ctx, false, out var whyUp) ? StepResult.Done() : StepResult.Fail(whyUp);
        }

        private static bool Press(StepContext ctx, bool down, out string why)
        {
            string key = ctx.Step.Str("key");
            if (key != null)
            {
                //  누른 채로 멈춰도 끝에 뗀다.
                if (down) ctx.RememberRestore(RestoreKinds.KeyUp, key, null, false);
                return ctx.Driver.SetKey(key, down, out why);
            }
            if (down) ctx.RememberRestore(RestoreKinds.Touchscreen, string.Empty, null, false);
            var at = new Vector2((float)ctx.Step.Num("x").Value, (float)ctx.Step.Num("y").Value);
            return ctx.Driver.SetTouch(at, down, out why);
        }
    }
}
