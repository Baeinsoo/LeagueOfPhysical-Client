using System.IO;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>로딩판 — 종목 소개판 요소와 하얀 빛.</summary>
    public class GameLoadingViewTests
    {
        [Test]
        public void 종목_소개판_요소가_있다()
        {
            var uxml = File.ReadAllText("Assets/UI/Overlay/GameLoadingView.uxml");
            foreach (var name in new[] { "loading-tag", "loading-title", "loading-map", "loading-desc", "loading-dots", "loading-flash" })
                StringAssert.Contains($"name=\"{name}\"", uxml);
            StringAssert.DoesNotContain("loading_screen", File.ReadAllText("Assets/UI/Overlay/GameLoadingView.uss"));
        }

        [Test]
        public void 하얀_빛이_걷히며_판이_드러난다()
        {
            var uss = File.ReadAllText("Assets/UI/Overlay/GameLoadingView.uss");
            StringAssert.Contains(".loading-flash--on { opacity: 1; }", uss);
            StringAssert.Contains("transition-property: opacity;", uss);
        }
    }
}
