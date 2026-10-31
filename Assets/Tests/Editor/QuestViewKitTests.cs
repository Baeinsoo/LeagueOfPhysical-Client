using System.IO;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>키트 퀘스트 판 — 셸 틀, 오늘·이번 주 묶음 요소, 카탈로그 등록.</summary>
    public class QuestViewKitTests
    {
        private const string Uxml = "Assets/UI/Quest/QuestView.uxml";
        private const string Uss = "Assets/UI/Quest/QuestView.uss";

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            ShellFrameTests.AssertShellFrame(x);
            foreach (var n in new[] { "quest-message", "quest-scroll", "quest-daily-remaining", "quest-reroll-left", "quest-daily-list", "quest-weekly-remaining", "quest-weekly-list" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("text=\"오늘\"", x);
            StringAssert.Contains("text=\"이번 주\"", x);
        }

        [Test]
        public void 끝난_칸_규칙과_색()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains(".quest-card--done", s);
            StringAssert.Contains(".quest-card__stamp", s);
            StringAssert.DoesNotContain("#000", s);
        }

        [Test]
        public void 카탈로그에_있다() => StringAssert.Contains("viewName: QuestView", File.ReadAllText("Assets/UI/UIViewCatalog.asset"));
    }
}
