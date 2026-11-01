using System.Collections.Generic;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    /// <summary>입력 단계의 공통 규칙(spec §8.3·§8.4).</summary>
    internal static class InputRules
    {
        /// <summary>
        /// 입력을 넣기 전에 — Game 뷰 초점과 상관없이 게임이 받게 입력 모드를 바꾼다(spec §8.3). 처음 값을 **먼저** 기억한다:
        /// 되돌리기는 거꾸로 돌므로, 그래야 키·포인터 떼기가 이 모드가 살아 있을 때 들어간다.
        /// </summary>
        public static bool EnsureGameInput(StepContext ctx, out string why)
        {
            string now = ctx.Driver.GameInputMode;
            if (now == GameInputModes.AlwaysToGame)
            {
                why = null;
                return true;
            }
            ctx.RememberRestore(RestoreKinds.GameInput, string.Empty, now, hadValue: true);
            return ctx.Driver.SetGameInputMode(GameInputModes.AlwaysToGame, out why);
        }

        public static void ValidatePoint(ScenarioStep step, string xKey, string yKey, List<string> errors)
        {
            double? x = step.Num(xKey);
            double? y = step.Num(yKey);
            if (x == null || y == null)
            {
                errors.Add($"{step}: {xKey}·{yKey}(0..1 정규화 좌표)가 없다");
                return;
            }
            if (x < 0 || x > 1 || y < 0 || y > 1) errors.Add($"{step}: {xKey}·{yKey}는 0..1(왼쪽 위 원점) — 화면 픽셀은 input_tap의 x·y");
        }

        public static void ValidateDevice(ScenarioStep step, List<string> errors)
        {
            if (!PointerArgs.TryParseDevice(step.Str("device"), out _)) errors.Add($"{step}: device는 mouse·touch 중 하나");
        }

        public static Vector2 Point(ScenarioStep step, string xKey, string yKey) =>
            new Vector2((float)step.Num(xKey).Value, (float)step.Num(yKey).Value);

        /// <summary>누르기 전에 — 실행이 누른 채 끝나도 떼게 기억한다. 합성 터치스크린 치우기는 떼기 뒤에 돌아야 하므로 먼저 기억한다.</summary>
        public static void RememberRelease(StepContext ctx, PointerDevice device, Vector2 at)
        {
            if (device == PointerDevice.Touch) ctx.RememberRestore(RestoreKinds.Touchscreen, string.Empty, null, false);
            ctx.RememberRestore(RestoreKinds.PointerUp, PointerArgs.DeviceName(device), PointerArgs.Format(at), true);
        }
    }

    /// <summary>
    /// 화면 한 점을 hold_ms 동안 눌렀다 뗀다 — Input System 포인터 장치로(spec §8.4), 게임 코드와 UI Toolkit이 사람 손처럼 같이 받는다.
    /// touch·long_press는 기본 누름 시간만 다르다.
    /// </summary>
    public abstract class PointerPressStep : IStepHandler
    {
        public abstract string Op { get; }
        protected abstract int DefaultHoldMs { get; }

        private int HoldMs(ScenarioStep step) => step.Int("hold_ms") ?? DefaultHoldMs;

        public double DefaultTimeoutSeconds(ScenarioStep step) => HoldMs(step) / 1000.0 + 5;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            InputRules.ValidatePoint(step, "x", "y", errors);
            InputRules.ValidateDevice(step, errors);
            if (step.Has("hold_ms") && !(step.Int("hold_ms") > 0)) errors.Add($"{step}: hold_ms는 0보다 큰 정수");
        }

        public StepResult Tick(StepContext ctx)
        {
            if (!ctx.Driver.IsPlaying) return StepResult.Fail("재생 중이 아니다 — 좌표 입력은 재생 중에만");
            var device = PointerArgs.ParseDevice(ctx.Step.Str("device"));
            var at = InputRules.Point(ctx.Step, "x", "y");
            string why;

            if (ctx.Scratch["down_at"] == null)
            {
                if (!InputRules.EnsureGameInput(ctx, out why)) return StepResult.Fail(why);
                InputRules.RememberRelease(ctx, device, at);
                if (!ctx.Driver.QueuePointer(device, at, PointerPhase.Down, out why)) return StepResult.Fail(why);
                ctx.Scratch["down_at"] = ctx.ElapsedSeconds;
                return StepResult.Running;
            }
            if (ctx.ElapsedSeconds - (double)ctx.Scratch["down_at"] < HoldMs(ctx.Step) / 1000.0)
            {
                ctx.Waiting = "누르고 있음";
                return StepResult.Running;
            }
            return ctx.Driver.QueuePointer(device, at, PointerPhase.Up, out why) ? StepResult.Done(PointerArgs.Format(at)) : StepResult.Fail(why);
        }
    }

    public sealed class TouchStep : PointerPressStep
    {
        public override string Op => "touch";
        protected override int DefaultHoldMs => 100;
    }

    public sealed class LongPressStep : PointerPressStep
    {
        public override string Op => "long_press";
        protected override int DefaultHoldMs => 800;
    }

    /// <summary>
    /// (x,y)에서 눌러 duration_ms 동안 (to_x,to_y)로 끌고 뗀다. 움직임은 에디터 틱(초당 10번)마다 한 번 — 그보다 고운 궤적은 없다.
    /// 끝점에 한 틱 머문 뒤 뗀다: 같은 프레임에 움직임과 뗌이 오면 끝점을 못 보는 코드가 있다.
    /// </summary>
    public sealed class DragStep : IStepHandler
    {
        public string Op => "drag";

        private static int DurationMs(ScenarioStep step) => step.Int("duration_ms") ?? 300;

        public double DefaultTimeoutSeconds(ScenarioStep step) => DurationMs(step) / 1000.0 + 5;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            InputRules.ValidatePoint(step, "x", "y", errors);
            InputRules.ValidatePoint(step, "to_x", "to_y", errors);
            InputRules.ValidateDevice(step, errors);
            if (step.Has("duration_ms") && !(step.Int("duration_ms") > 0)) errors.Add($"{step}: duration_ms는 0보다 큰 정수");
        }

        public StepResult Tick(StepContext ctx)
        {
            if (!ctx.Driver.IsPlaying) return StepResult.Fail("재생 중이 아니다 — 좌표 입력은 재생 중에만");
            var device = PointerArgs.ParseDevice(ctx.Step.Str("device"));
            var from = InputRules.Point(ctx.Step, "x", "y");
            var to = InputRules.Point(ctx.Step, "to_x", "to_y");
            string why;

            if (ctx.Scratch["down_at"] == null)
            {
                if (!InputRules.EnsureGameInput(ctx, out why)) return StepResult.Fail(why);
                InputRules.RememberRelease(ctx, device, from);
                if (!ctx.Driver.QueuePointer(device, from, PointerPhase.Down, out why)) return StepResult.Fail(why);
                ctx.Scratch["down_at"] = ctx.ElapsedSeconds;
                return StepResult.Running;
            }
            if (ctx.Scratch["at_end"] != null)
                return ctx.Driver.QueuePointer(device, to, PointerPhase.Up, out why) ? StepResult.Done(PointerArgs.Format(to)) : StepResult.Fail(why);

            double t = (ctx.ElapsedSeconds - (double)ctx.Scratch["down_at"]) / (DurationMs(ctx.Step) / 1000.0);
            if (t >= 1)
            {
                if (!ctx.Driver.QueuePointer(device, to, PointerPhase.Move, out why)) return StepResult.Fail(why);
                ctx.Scratch["at_end"] = true;
                return StepResult.Running;
            }
            if (!ctx.Driver.QueuePointer(device, Vector2.Lerp(from, to, (float)t), PointerPhase.Move, out why)) return StepResult.Fail(why);
            ctx.Waiting = "끄는 중";
            return StepResult.Running;
        }
    }
}
