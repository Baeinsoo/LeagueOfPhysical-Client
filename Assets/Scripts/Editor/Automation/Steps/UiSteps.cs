using System.Collections.Generic;

namespace LOP.EditorTools.Automation
{
    internal static class UiStepRules
    {
        public static void RequireSelector(ScenarioStep step, List<string> errors)
        {
            if (UiSelector.FromStep(step).IsEmpty) errors.Add($"{step}: name·text·text_contains 중 하나는 있어야 한다");
        }

        public static string Describe(UiSelector selector, UiElementInfo found) =>
            found == null ? $"{selector} 없음" : $"{selector} 보임={found.Visible} 켜짐={found.Enabled} 맨위={found.OnTop}";
    }

    /// <summary>요소가 보일 때까지(clickable이면 누를 수 있을 때까지) 기다린다.</summary>
    public sealed class UiWaitStep : IStepHandler
    {
        public string Op => "ui_wait";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 30;
        public void Validate(ScenarioStep step, List<string> errors) => UiStepRules.RequireSelector(step, errors);

        public StepResult Tick(StepContext ctx)
        {
            var selector = UiSelector.FromStep(ctx.Step);
            var found = ctx.Driver.FindElement(selector);
            bool ready = found != null && (ctx.Step.Bool("clickable", false) ? found.Clickable : found.Visible);
            if (ready) return StepResult.Done(found.Text);
            ctx.Waiting = UiStepRules.Describe(selector, found);
            return StepResult.Running;
        }
    }

    /// <summary>누를 수 있게 될 때까지 기다렸다가 진짜 포인터처럼 한 번 누른다(spec §8.1).</summary>
    public sealed class UiClickStep : IStepHandler
    {
        public string Op => "ui_click";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 15;
        public void Validate(ScenarioStep step, List<string> errors) => UiStepRules.RequireSelector(step, errors);

        public StepResult Tick(StepContext ctx)
        {
            var selector = UiSelector.FromStep(ctx.Step);
            var found = ctx.Driver.FindElement(selector);
            if (found == null || !found.Clickable)
            {
                ctx.Waiting = UiStepRules.Describe(selector, found);
                return StepResult.Running;
            }
            return ctx.Driver.Click(selector, out var why) ? StepResult.Done(found.Text) : StepResult.Fail(why);
        }
    }

    /// <summary>보이면 그 글자(안쪽 글자 포함)를 기록의 <c>as</c> 칸에 남긴다.</summary>
    public sealed class UiReadStep : IStepHandler
    {
        public string Op => "ui_read";
        public double DefaultTimeoutSeconds(ScenarioStep step) => 10;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            UiStepRules.RequireSelector(step, errors);
            if (string.IsNullOrEmpty(step.Str("as"))) errors.Add($"{step}: as(기록 이름)가 없다");
        }

        public StepResult Tick(StepContext ctx)
        {
            var selector = UiSelector.FromStep(ctx.Step);
            var found = ctx.Driver.FindElement(selector);
            if (found == null || !found.Visible)
            {
                ctx.Waiting = UiStepRules.Describe(selector, found);
                return StepResult.Running;
            }
            ctx.Recorder.Capture(ctx.Step.Str("as"), found.Text ?? string.Empty);
            return StepResult.Done(found.Text);
        }
    }
}
