using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Pipeline.Commands;

namespace LOP.EditorTools.Automation
{
    /// <summary>unity CLI(com.unity.pipeline) 명령(spec §7). 모두 바로 돌아온다 — 기다리는 일은 실행기가 에디터 틱에서 한다.</summary>
    public static class AutomationCommands
    {
        private const string Tag = "lop/automation";
        private static string ProjectRoot => Directory.GetCurrentDirectory();

        [CliCommand("automation_start", "LOP 자동화 시나리오를 시작하고 바로 돌아온다. 진행은 automation_status, 멈춤은 automation_stop.", Tags = new[] { Tag })]
        public static AutomationStatus Start(
            [CliArg("scenario", "Automation/Scenarios 아래 이름(예: flappy-quest-grind) 또는 프로젝트 기준 .json 경로", Required = true)] string scenario,
            [CliArg("repeat", "반복 횟수. 0이면 시나리오 파일의 repeat")] int repeat = 0)
        {
            if (!ScenarioFiles.TryResolve(scenario, ProjectRoot, out var path, out var why)) throw new ArgumentException(why);
            return AutomationHost.Session.Start(File.ReadAllText(path), path, repeat, DateTime.UtcNow);
        }

        [CliCommand("automation_step", "단계 하나를 작은 실행으로 돌린다(ui_wait·input_tap처럼 기다리는 단계도 막지 않는다). 진행은 automation_status.", Tags = new[] { Tag })]
        public static AutomationStatus Step(
            [CliArg("step", "단계 JSON 한 줄. 예: {\"op\":\"ui_wait\",\"name\":\"play-button\",\"clickable\":true}", Required = true)] string step)
        {
            return AutomationHost.Session.Start(ScenarioParser.SingleStepScenario(step), "(step)", 1, DateTime.UtcNow);
        }

        [CliCommand("automation_status", "지금(없으면 마지막) 실행의 상태. done=true면 끝났다 — report가 결과 파일.", Tags = new[] { Tag })]
        public static AutomationStatus Status() => AutomationHost.Session.Status(DateTime.UtcNow);

        [CliCommand("automation_stop", "도는 실행을 멈춘다: 지금 단계를 끊고 teardown과 설정 되돌리기. 한 번 더 부르면 teardown도 건너뛴다.", Tags = new[] { Tag })]
        public static AutomationStatus Stop()
        {
            AutomationHost.Session.Stop();
            return AutomationHost.Session.Status(DateTime.UtcNow);
        }

        [CliCommand("automation_list", "시나리오 이름과 단계(op) 목록", Tags = new[] { Tag })]
        public static object List() => new
        {
            scenarios = ScenarioFiles.List(ProjectRoot).ToArray(),
            ops = StepCatalog.CreateAll().Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
        };

        [CliCommand("ui_query", "지금 화면의 UI Toolkit 요소를 찾아 보임·켜짐·맨위·글자를 돌려준다(기다리지 않는다)", Tags = new[] { Tag })]
        public static IReadOnlyList<UiElementInfo> UiQuery(
            [CliArg("name", "요소 이름(UXML name)")] string name = null,
            [CliArg("text", "글자 그대로")] string text = null,
            [CliArg("text_contains", "글자 일부")] string textContains = null)
        {
            return new EditorAutomationDriver().QueryElements(Selector(name, text, textContains));
        }

        [CliCommand("ui_click", "지금 누를 수 있는 요소를 진짜 포인터처럼 누른다(기다리지 않는다 — 기다리려면 automation_step의 ui_click)", Tags = new[] { Tag })]
        public static UiElementInfo UiClick(
            [CliArg("name", "요소 이름(UXML name)")] string name = null,
            [CliArg("text", "글자 그대로")] string text = null,
            [CliArg("text_contains", "글자 일부")] string textContains = null)
        {
            if (AutomationHost.Session.IsRunning) throw new InvalidOperationException("자동화 실행이 도는 중이다 — 끝나거나 멈춘 뒤에");
            var selector = Selector(name, text, textContains);
            var driver = new EditorAutomationDriver();
            var found = driver.FindElement(selector);
            if (!driver.Click(selector, out var why)) throw new InvalidOperationException(why);
            return found;
        }

        private static UiSelector Selector(string name, string text, string textContains)
        {
            var selector = new UiSelector { Name = name, Text = text, TextContains = textContains };
            if (selector.IsEmpty) throw new ArgumentException("name·text·text_contains 중 하나는 준다");
            return selector;
        }
    }
}
