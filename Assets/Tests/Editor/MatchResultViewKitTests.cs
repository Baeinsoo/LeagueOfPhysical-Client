using System.IO;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>키트 결과판 — 요소 이름·키트 루트·랭크 반전 규칙, 무대를 가리지 않는 창.</summary>
    public class MatchResultViewKitTests
    {
        private const string Uxml = "Assets/UI/MatchResult/MatchResultView.uxml";
        private const string Uss = "Assets/UI/MatchResult/MatchResultView.uss";

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            foreach (var n in new[] { "matchresult-root", "matchresult-board", "matchresult-tag", "matchresult-title", "matchresult-subtitle", "matchresult-draw", "matchresult-rows", "matchresult-empty", "matchresult-lines", "matchresult-rank", "matchresult-reward", "confirm-button", "matchresult-flash" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("class=\"kit ", x);
            StringAssert.Contains("kit-panel", x);
        }

        [Test]
        public void 랭크_반전과_등장_규칙()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains(".ranked .matchresult-board", s);
            StringAssert.Contains("--out", s);
            StringAssert.DoesNotContain("#000", s);
        }

        [Test]
        public void 무대를_어둡게_덮지_않는다() => Assert.IsFalse(new MatchResultView(null).DimsBackground);
    }
}
