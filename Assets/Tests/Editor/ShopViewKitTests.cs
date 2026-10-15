using System.IO;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>키트 상점 — 셸 틀, 카드·고른 품목 막대·구매 확인 겹창 요소. 장착 버튼은 없다(입기는 라커룸).</summary>
    public class ShopViewKitTests
    {
        private const string Uxml = "Assets/UI/Shop/ShopView.uxml";
        private const string Uss = "Assets/UI/Shop/ShopView.uss";

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            ShellFrameTests.AssertShellFrame(x);
            foreach (var n in new[] { "shop-tabs", "shop-grid", "shop-bar", "shop-bar-name", "shop-bar-state", "buy-button", "shop-to-locker", "shop-owned-hint", "shop-message", "shop-confirm", "confirm-question", "confirm-remaining", "confirm-equip", "confirm-no", "confirm-yes" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("<ui:Toggle name=\"confirm-equip\"", x);
            StringAssert.DoesNotContain("equip-button", x);
        }

        [Test]
        public void 흐린_상태_줄과_고른_카드_규칙()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains(".shop-card__state--dim", s);
            StringAssert.Contains(".shop-card--picked", s);
            StringAssert.DoesNotContain("#000", s);
        }
    }
}
