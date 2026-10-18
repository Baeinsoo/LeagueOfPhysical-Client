using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 생성 데이터 프로토의 룩(슬롯 코드→품목 코드)·이름·레벨을 <see cref="PlayerLook"/>으로 바꾼다.
    /// 룩·이름·레벨이 전부 비어 있으면 몬스터·심판처럼 사람이 아닌 몸이거나 구버전 서버다 — null을
    /// 돌려서 이름표·치비 꾸밈이 안 붙게 한다.
    /// </summary>
    public static class PlayerLookFromProto
    {
        public static PlayerLook Convert(global::CharacterCreationData proto) =>
            From(proto.Look, proto.DisplayName, proto.AccountLevel, proto.CharacterAppearance);

        /// <summary>룩·이름·레벨·외형이 전부 비면 null — 사람이 아닌 몸이거나 옛 서버다. 외형 칸이 없으면 0(모름).</summary>
        public static PlayerLook From(IEnumerable<KeyValuePair<string, string>> look, string displayName, int accountLevel,
            CharacterAppearanceInfo appearanceInfo = null)
        {
            var appearance = appearanceInfo != null
                ? new CharacterAppearance(appearanceInfo.SkinColorId, appearanceInfo.HairColorId)
                : default;
            var slots = new Dictionary<string, string>();
            if (look != null)
            {
                foreach (var kv in look) slots[kv.Key] = kv.Value;
            }
            if (slots.Count == 0 && string.IsNullOrEmpty(displayName) && accountLevel == 0 && appearance == default)
            {
                return null;
            }
            return new PlayerLook(slots, displayName, accountLevel, appearance);
        }
    }
}
