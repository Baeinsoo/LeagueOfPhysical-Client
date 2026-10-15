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
        public void 마스터데이터가_아직_없어도_던지지_않고_입장_중()
        {
            //  재접속이면 입구 씬에서 마스터데이터를 읽기 전에 로딩판이 열린다. 여기서 던지면 닫을 수 없는 판이 남는다(리뷰 지적).
            var text = LOP.UI.GameLoadingView.ComposeText(0, 0, 0, null, null);
            Assert.AreEqual(LOP.UI.LoadingBoardText.Entering, text.Title);
        }

        [Test]
        public void 하얀_빛이_걷히며_판이_드러난다()
        {
            var uss = File.ReadAllText("Assets/UI/Overlay/GameLoadingView.uss");
            StringAssert.Contains(".loading-flash--on { opacity: 1; }", uss);
            //  열리는 순간부터 하양 — 판 전체가 투명에서 차오르고(3D 위로 하얀 빛), 그다음 하양이 걷힌다.
            StringAssert.Contains(".loading-root--enter { opacity: 0; }", uss);
            var uxml = File.ReadAllText("Assets/UI/Overlay/GameLoadingView.uxml");
            StringAssert.Contains("loading-flash loading-flash--on", uxml);
            StringAssert.Contains("loading-root loading-root--enter", uxml);
            StringAssert.Contains("transition-property: opacity;", uss);
        }
    }
}
