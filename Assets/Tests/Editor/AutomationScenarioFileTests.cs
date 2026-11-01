using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>단계 카탈로그와 레포에 실린 첫 시나리오(spec §9)가 실제 단계들로 오류 없이 읽히는지.</summary>
    public class AutomationScenarioFileTests
    {
        [Test]
        public void 카탈로그는_열네_단계를_op로_가진다()
        {
            CollectionAssert.AreEquivalent(
                new[] { "ui_wait", "ui_click", "ui_read", "input_tap", "wait_state", "play_enter", "play_exit", "boot_to_lobby",
                        "select_game", "auto_flap", "set_environment", "screenshot", "log", "wait" },
                StepCatalog.CreateAll().Keys.ToArray());
        }

        [Test]
        public void 첫_시나리오는_오류_없이_읽히고_필수_단계는_optional이_아니다()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "..", "Automation", "Scenarios", "flappy-quest-grind.json");
            var errors = new List<string>();

            var s = ScenarioParser.Parse(File.ReadAllText(path), StepCatalog.CreateAll(), errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual("flappy-quest-grind", s.Name);
            CollectionAssert.AreEqual(new[] { "set_environment", "auto_flap", "play_enter", "boot_to_lobby" }, s.Setup.Select(x => x.Op).ToArray());
            Assert.IsTrue(s.Loop.Any(x => x.Op == "select_game" && x.Int("game_mode_id") == 6 && !x.Optional));
            Assert.IsTrue(s.Loop.Any(x => x.Op == "ui_click" && x.Str("name") == "play-button" && !x.Optional));
            Assert.IsTrue(s.Loop.Any(x => x.Op == "wait_state" && x.Str("state") == "PostMatch" && !x.Optional));
            Assert.IsTrue(s.Loop.Any(x => x.Op == "ui_click" && x.Str("name") == "confirm-button" && !x.Optional));
            Assert.IsTrue(s.Loop.Any(x => x.Op == "screenshot" && x.Str("as") == "result"));
            Assert.AreEqual("play_exit", s.Teardown.Single().Op);
            //  퀘스트 판은 아직 없을 수 있다(B2b 전) — 그 단계는 실패해도 멈추지 않아야 한다.
            Assert.IsTrue(s.Loop.Where(x => (x.Str("name") ?? string.Empty).StartsWith("quest") || x.Str("name") == "nav-quests").All(x => x.Optional));
        }
    }
}
