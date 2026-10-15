using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>상점 카드 상태 줄·구매 확인 문구·거절 문구(라커룸과 공용) — 순수 함수.</summary>
    public class ShopFormatTests
    {
        private static ShopItem Item(ItemState state, long? price) => new ShopItem(1, "x", price, state, canBuy: false);

        [Test] public void 미보유는_가격() => Assert.AreEqual(("🪙 300", false), ShopFormat.StateLine(Item(ItemState.NotOwned, 300), 1000));
        [Test] public void 잔액이_모자라면_흐리게() => Assert.AreEqual(("🪙 300", true), ShopFormat.StateLine(Item(ItemState.NotOwned, 300), 100));
        [Test] public void 보유() => Assert.AreEqual(("✓ 보유", false), ShopFormat.StateLine(Item(ItemState.Owned, 300), 0));
        [Test] public void 장착_중() => Assert.AreEqual(("★ 장착 중", false), ShopFormat.StateLine(Item(ItemState.Equipped, null), 0));
        [Test] public void 기본() => Assert.AreEqual(("기본", false), ShopFormat.StateLine(Item(ItemState.Default, null), 0));
        [Test] public void 가격_없는_미보유는_비매품() => Assert.AreEqual(("비매품", false), ShopFormat.StateLine(Item(ItemState.NotOwned, null), 0));

        [Test]
        public void 확인_문구()
        {
            Assert.AreEqual("빨간 모자을(를) 🪙 1,200에 살까요?", ShopFormat.ConfirmQuestion("빨간 모자", 1200));
            Assert.AreEqual("남는 코인 🪙 40", ShopFormat.RemainingCoins(1240, 1200));
        }

        [Test] public void 거절_문구_공용() => Assert.AreEqual("코인이 부족합니다", EconomyMessages.For(ResponseCode.INSUFFICIENT_FUNDS));
    }
}
