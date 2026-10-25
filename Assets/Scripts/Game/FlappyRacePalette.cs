using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 플래피 레이스 포장의 색. 값의 출처는 시제품 "광산 레이스 적용판"(`MUTED`·`rankColor`).
    /// <para>상대 색이 차분한 이유: 내 새(노랑)만 채도가 높아야 여러 새 사이에서 한눈에 찾는다(intent 성공 판정 2).</para>
    /// </summary>
    public static class FlappyRacePalette
    {
        /// <summary>내 새·내 점. 시제품 `MUTED[0]` — 상대에겐 주지 않는다.</summary>
        public static readonly Color Mine = new Color32(0xff, 0xd2, 0x57, 0xff);

        /// <summary>상대 다섯 칸(시제품 `MUTED[1..5]`). 6인 판이면 상대가 다섯이라 겹치지 않는다.</summary>
        public static readonly Color[] Opponents =
        {
            new Color32(0x8a, 0xa7, 0xb8, 0xff),
            new Color32(0xb8, 0x90, 0x9c, 0xff),
            new Color32(0x97, 0xad, 0x86, 0xff),
            new Color32(0xa3, 0x99, 0xb8, 0xff),
            new Color32(0xbc, 0x9c, 0x83, 0xff),
        };

        private static readonly Color Gold = new Color32(0xff, 0xd2, 0x57, 0xff);
        private static readonly Color Silver = new Color32(0xdf, 0xe6, 0xee, 0xff);
        private static readonly Color Bronze = new Color32(0xe7, 0xa4, 0x6a, 0xff);
        private static readonly Color Plain = new Color32(0xf3, 0xeb, 0xe2, 0xff);

        /// <summary>순위 숫자 색. 1·2·3위 금·은·동, 그 밖은 따뜻한 흰색.</summary>
        public static Color RankColor(int rank)
        {
            switch (rank)
            {
                case 1: return Gold;
                case 2: return Silver;
                case 3: return Bronze;
                default: return Plain;
            }
        }
    }
}
