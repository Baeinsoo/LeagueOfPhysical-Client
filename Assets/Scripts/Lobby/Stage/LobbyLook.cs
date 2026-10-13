using System.Collections.Generic;

namespace LOP
{
    /// <summary>로비가 받은 장착 정보(슬롯 번호 → 품목 번호)를 경기 화면이 쓰는 룩(슬롯 코드 → 품목 코드)으로 바꾼다.</summary>
    public static class LobbyLook
    {
        public static PlayerLook From(IReadOnlyList<LoadoutSlotDto> loadout, CosmeticCatalog catalog, string displayName, int level)
        {
            var slots = new Dictionary<string, string>();
            if (loadout != null && catalog != null)
            {
                foreach (var entry in loadout)
                {
                    if (entry == null) continue;
                    string slotCode = catalog.SlotById(entry.slotId)?.Code;
                    string itemCode = catalog.ById(entry.cosmeticId)?.Code;
                    //  표가 바뀌어 모르는 번호가 오면 그 슬롯만 건너뛴다 — 기본 색으로 남는다.
                    if (string.IsNullOrEmpty(slotCode) || string.IsNullOrEmpty(itemCode)) continue;
                    slots[slotCode] = itemCode;
                }
            }
            return new PlayerLook(slots, displayName, level);
        }
    }
}
