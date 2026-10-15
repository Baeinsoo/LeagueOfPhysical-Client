using System.Globalization;

namespace LOP.UI
{
    /// <summary>상점 카드 상태 줄과 구매 확인 문구. 값은 전부 <see cref="ShopItem"/>·지갑에서 그대로 읽는다.</summary>
    public static class ShopFormat
    {
        /// <summary>카드 아래 한 줄. dim = 미보유인데 지갑이 모자라 흐리게 그릴 때.</summary>
        public static (string text, bool dim) StateLine(ShopItem item, long coins) => item.State switch
        {
            ItemState.Equipped => ("★ 장착 중", false),
            ItemState.Default => ("기본", false),
            ItemState.Owned => ("✓ 보유", false),
            _ => item.CoinPrice is long price ? ($"🪙 {Coins(price)}", coins < price) : ("비매품", false),
        };

        public static string ConfirmQuestion(string name, long price) => $"{name}을(를) 🪙 {Coins(price)}에 살까요?";

        public static string RemainingCoins(long coins, long price) => $"남는 코인 🪙 {Coins(coins - price)}";

        private static string Coins(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
