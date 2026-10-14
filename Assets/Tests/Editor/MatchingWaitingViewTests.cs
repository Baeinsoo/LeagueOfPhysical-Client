using System.IO;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>매칭 대기 창 — 화면을 어둡게 덮지 않는 위쪽 알약(대기 중에도 터널이 보인다).</summary>
    public class MatchingWaitingViewTests
    {
        [Test]
        public void 화면을_어둡게_덮지_않는다()
        {
            Assert.IsFalse(new MatchingWaitingView().DimsBackground);
        }

        [Test]
        public void 키트_알약_요소()
        {
            var uxml = File.ReadAllText("Assets/UI/Overlay/MatchingWaitingView.uxml");
            StringAssert.Contains("matching-pill", uxml);
            foreach (var name in new[] { "matching-queue", "matching-elapsed", "cancel-button" })
                StringAssert.Contains($"name=\"{name}\"", uxml);
            StringAssert.DoesNotContain("scrim", uxml);
        }
    }
}
