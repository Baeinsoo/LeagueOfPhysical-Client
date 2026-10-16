using System.IO;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>키트 이름 바꾸기 모달 — View 코드가 찾는 요소 이름 그대로, 키트 루트·판.</summary>
    public class ChangeDisplayNameViewKitTests
    {
        private const string Uxml = "Assets/UI/ChangeDisplayName/ChangeDisplayNameView.uxml";
        private const string Uss = "Assets/UI/ChangeDisplayName/ChangeDisplayNameView.uss";

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            foreach (var n in new[] { "cdn-root", "cdn-input", "cdn-message", "cdn-cancel", "cdn-confirm" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("class=\"kit cdn-root", x);
            StringAssert.Contains("kit-panel", x);
            StringAssert.Contains("kit-btn--primary", x);
        }

        [Test]
        public void 키트_색만_쓴다()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains("var(--lop-live)", s);
            StringAssert.DoesNotContain("rgb(255, 120, 120)", s);
            StringAssert.DoesNotContain("#000", s);
        }
    }
}
