using System.Collections.Generic;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 글자 칸(TextField)에 친다 — UI Toolkit이 진짜 키보드에서 받는 KeyDownEvent로(spec §8.5), 칸을 누를 수 있게 될 때까지 기다린다.
    /// 칠 글자는 value — text는 선택자다.
    /// </summary>
    public sealed class UiTypeStep : IStepHandler
    {
        public string Op => "ui_type";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 15;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            UiStepRules.RequireSelector(step, errors);
            if (step.Str("value") == null) errors.Add($"{step}: value(칠 글자, 빈 글자도 된다)가 없다 — text는 선택자다");
        }

        public StepResult Tick(StepContext ctx)
        {
            var selector = UiSelector.FromStep(ctx.Step);
            var found = ctx.Driver.FindElement(selector);
            if (found == null || !found.Clickable)
            {
                ctx.Waiting = UiStepRules.Describe(selector, found);
                return StepResult.Running;
            }
            string value = ctx.Step.Str("value");
            return ctx.Driver.TypeIntoField(selector, value, ctx.Step.Bool("clear", true), ctx.Step.Bool("submit", false), out var why)
                ? StepResult.Done(value)
                : StepResult.Fail(why);
        }
    }

    /// <summary>Input System 키보드에 글자 이벤트를 넣는다 — Keyboard.onTextInput을 듣는 게임 코드용. UI Toolkit 글자 칸은 ui_type(spec §8.5).</summary>
    public sealed class InputTextStep : IStepHandler
    {
        public string Op => "input_text";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 5;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            if (string.IsNullOrEmpty(step.Str("value"))) errors.Add($"{step}: value(칠 글자)가 없다");
        }

        public StepResult Tick(StepContext ctx)
        {
            if (!ctx.Driver.IsPlaying) return StepResult.Fail("재생 중이 아니다 — 글자 입력은 재생 중에만");
            if (!InputRules.EnsureGameInput(ctx, out var why)) return StepResult.Fail(why);
            string value = ctx.Step.Str("value");
            return ctx.Driver.QueueText(value, out why) ? StepResult.Done(value) : StepResult.Fail(why);
        }
    }
}
