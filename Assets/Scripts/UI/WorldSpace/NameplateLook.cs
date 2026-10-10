using LOP.MasterData;

namespace LOP
{
    /// <summary>이름표에 그릴 텍스트 4종. 순수 데이터 — 화면에 어떻게 그리는지는 모른다.</summary>
    public readonly struct NameplateText
    {
        public readonly string Name;
        public readonly string Title;
        public readonly int Level;
        public readonly string BannerStyle;

        public NameplateText(string name, string title, int level, string bannerStyle)
        {
            Name = name;
            Title = title;
            Level = level;
            BannerStyle = bannerStyle;
        }
    }

    /// <summary>
    /// <see cref="PlayerLook"/>(슬롯 코드→품목 코드)과 <see cref="CosmeticCatalog"/>(품목 코드→실제 모양)를
    /// 합쳐 이름표 텍스트를 만든다. Unity 없이 검사되는 순수 함수.
    /// </summary>
    public static class NameplateLook
    {
        private const string DefaultName = "플레이어";
        private const int DefaultLevel = 1;
        private const string DefaultBannerStyle = "plain";

        private const string TitleSlotCode = "title";
        private const string BannerSlotCode = "banner";
        private const string TextAssetKind = "text";
        private const string BannerAssetKind = "banner";

        public static NameplateText Resolve(PlayerLook look, CosmeticCatalog catalog)
        {
            if (look == null)
            {
                return new NameplateText(DefaultName, string.Empty, DefaultLevel, DefaultBannerStyle);
            }

            string name = string.IsNullOrEmpty(look.DisplayName) ? DefaultName : look.DisplayName;
            string title = ResolveAssetKey(look.SlotOrNull(TitleSlotCode), TextAssetKind, catalog, string.Empty);
            string bannerStyle = ResolveAssetKey(look.SlotOrNull(BannerSlotCode), BannerAssetKind, catalog, DefaultBannerStyle);

            return new NameplateText(name, title, look.AccountLevel, bannerStyle);
        }

        // 모르는 코드(마스터데이터에 없음)·다른 슬롯 종류의 코드는 조용히 기본값으로 — 그 슬롯만
        // 기본으로 그리고 나머지(이름·레벨·다른 슬롯)는 그대로 둔다(한 슬롯 오류가 전체로 안 번짐).
        private static string ResolveAssetKey(string itemCode, string expectedAssetKind, CosmeticCatalog catalog, string fallback)
        {
            Cosmetic item = catalog?.ByCode(itemCode);
            if (item == null || item.AssetKind != expectedAssetKind)
            {
                return fallback;
            }

            return item.AssetKey ?? fallback;
        }
    }
}
