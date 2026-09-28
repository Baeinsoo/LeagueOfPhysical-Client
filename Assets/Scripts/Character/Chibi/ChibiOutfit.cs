using UnityEngine;

namespace LOP
{
    public readonly struct ChibiColors
    {
        public readonly Color Top, Skin, Hair, Bottom, Shoe, Outline;

        public ChibiColors(Color top, Color skin, Color hair, Color bottom, Color shoe)
        {
            Top = top; Skin = skin; Hair = hair; Bottom = bottom; Shoe = shoe;
            Outline = new Color(top.r * 0.4f, top.g * 0.4f, top.b * 0.4f, 1f);
        }
    }

    /// <summary>팀 저지(임시 옷) — 윗옷·소매 = 파티 색, 바지 짙은 남보라, 신발 흰색. 사람마다 엔티티 id로 고른다.</summary>
    public static class ChibiOutfit
    {
        private static readonly Color[] Jerseys = Hexes("#FF4F5E", "#2EC4A6", "#6C63FF", "#F59E0B", "#3B82F6", "#10B981", "#EC4899", "#F97316");
        private static readonly Color[] Skins = Hexes("#F7D2AE", "#FFE0BD", "#F3C9A0", "#E3B089", "#C68642");
        private static readonly Color[] Hairs = Hexes("#4A3222", "#2A2A30", "#8A5A3B", "#D9A441");
        private static readonly Color Bottom = Hexes("#3A3450")[0];

        public static ChibiColors ColorsFor(string entityId)
        {
            uint h = Fnv1a(entityId ?? "");
            return new ChibiColors(Jerseys[h % (uint)Jerseys.Length], Skins[(h >> 8) % (uint)Skins.Length],
                                   Hairs[(h >> 16) % (uint)Hairs.Length], Bottom, Color.white);
        }

        public static void Apply(GameObject visual, ChibiColors c)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.GetPropertyBlock(block);
                block.SetColor("_TopColor", c.Top);
                block.SetColor("_SleeveColor", c.Top);
                block.SetColor("_BottomColor", c.Bottom);
                block.SetColor("_ShoeColor", c.Shoe);
                block.SetColor("_SkinColor", c.Skin);
                block.SetColor("_HairColor", c.Hair);
                block.SetColor("_OutlineColor", c.Outline);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>FNV-1a 32비트. string.GetHashCode와 달리 어느 기기에서나 같은 값이다.</summary>
        public static uint Fnv1a(string s)
        {
            uint h = 0x811C9DC5u;
            foreach (char ch in s)
            {
                h ^= ch;
                h *= 0x01000193u;
            }
            return h;
        }

        private static Color[] Hexes(params string[] hexes)
        {
            var colors = new Color[hexes.Length];
            for (int i = 0; i < hexes.Length; i++)
            {
                ColorUtility.TryParseHtmlString(hexes[i], out colors[i]);
            }
            return colors;
        }
    }
}
