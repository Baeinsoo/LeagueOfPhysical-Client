using UnityEngine;

namespace LOP
{
    /// <summary>티어 색·이름 — 키트 USS --tier-color와 같은 값. 티어가 없으면 리그 색.</summary>
    public static class TierPalette
    {
        public static readonly Color League = new Color32(0x3D, 0x3A, 0x86, 0xFF);

        public static Color ColorOf(string tierCode) => tierCode switch
        {
            "iron" => new Color32(0x7a, 0x6a, 0x62, 0xff),
            "bronze" => new Color32(0xb0, 0x70, 0x3a, 0xff),
            "silver" => new Color32(0x8a, 0x94, 0x9e, 0xff),
            "gold" => new Color32(0xd9, 0xa5, 0x20, 0xff),
            "platinum" => new Color32(0x3f, 0xb8, 0xa8, 0xff),
            "emerald" => new Color32(0x2f, 0xae, 0x5f, 0xff),
            "diamond" => new Color32(0x5b, 0x8d, 0xee, 0xff),
            "master" => new Color32(0xb2, 0x5a, 0xd6, 0xff),
            _ => League,
        };

        public static string KoreanName(string tierCode) => tierCode switch
        {
            "iron" => "아이언", "bronze" => "브론즈", "silver" => "실버", "gold" => "골드",
            "platinum" => "플래티넘", "emerald" => "에메랄드", "diamond" => "다이아몬드", "master" => "마스터",
            _ => null,
        };
    }
}
