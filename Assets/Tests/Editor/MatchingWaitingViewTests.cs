using System.IO;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>매칭 대기 창 — 가운데 큰 키트 판, 뒤는 어둡게(모달로 읽혀야 다른 버튼을 누르려 하지 않는다. 사용자 10-15).</summary>
    public class MatchingWaitingViewTests
    {
        [Test]
        public void 뒤를_어둡게_덮는_모달이다()
        {
            var view = new MatchingWaitingView();
            Assert.IsTrue(view.DimsBackground);
            Assert.IsTrue(view.BlocksUnderlyingInput);
        }

        [Test]
        public void 가운데_키트_판_요소()
        {
            var uxml = File.ReadAllText("Assets/UI/Overlay/MatchingWaitingView.uxml");
            StringAssert.Contains("kit-panel", uxml);
            foreach (var name in new[] { "matching-queue", "matching-elapsed", "cancel-button" })
                StringAssert.Contains($"name=\"{name}\"", uxml);
            StringAssert.DoesNotContain("matching-pill", uxml);
            StringAssert.Contains(".matching-root { flex-grow: 1; align-items: center; justify-content: center; }",
                File.ReadAllText("Assets/UI/Overlay/MatchingWaitingView.uss"));
        }
    }
}
