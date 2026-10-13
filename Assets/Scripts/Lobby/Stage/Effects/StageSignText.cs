using LOP.UI;
using UnityEngine;

namespace LOP
{
    /// <summary>현수막·티포에 쓸 티어 글자·색. 티어가 없으면(배치 전·못 받음·아직 안 옴) 리그 문양과 "배치 중".</summary>
    public readonly struct StageSignText
    {
        public readonly string TierCode;
        public readonly string TierLabel;
        public readonly string TierEnglish;
        public readonly Color TierColor;

        private StageSignText(string code, string label, string english, Color color)
        {
            TierCode = code; TierLabel = label; TierEnglish = english; TierColor = color;
        }

        public static StageSignText From(RankCard card)
        {
            string name = TierPalette.KoreanName(card.TierCode);
            return name == null
                ? new StageSignText(null, "배치 중", "LOP LEAGUE", TierPalette.League)
                : new StageSignText(card.TierCode, name, card.TierCode.ToUpperInvariant(), TierPalette.ColorOf(card.TierCode));
        }
    }
}
