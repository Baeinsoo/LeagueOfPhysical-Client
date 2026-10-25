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

        //  사용자 결정(10-25): 게임이 선수에게 색을 배정하지 않는다 — 새 모습은 장차 입는 옷이 정한다.
        //  그래서 상대는 칸별로 나누지 않고 전원 이 중립색 하나를 쓴다. 채도를 낮춰 두면 어두운 막대
        //  위에서도 읽히면서 내 점(노랑)과는 확실히 구분된다.
        /// <summary>상대 진행 점의 유일한 색. 누가 상대든 같다.</summary>
        public static readonly Color OpponentNeutral = new Color32(0xa9, 0xa2, 0x9a, 0xff);

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

        //  막 색(시제품 ACT_COL) — 1막 호박 · 2막 하늘 · 3막 분홍. 진행 막대 띠와 구간 배너 띠가 같이 쓴다(spec §5).
        private static readonly Color Act1 = new Color32(0xff, 0xb3, 0x47, 0xff);
        private static readonly Color Act2 = new Color32(0x5f, 0xd3, 0xff, 0xff);
        private static readonly Color Act3 = new Color32(0xff, 0x8f, 0xb1, 0xff);

        /// <summary>막 색. 모르는 막(0이나 4 이상)은 1막 색 — 맵이 막을 더 나눠도 화면이 깨지지 않는다.</summary>
        public static Color ActColor(int act)
        {
            switch (act)
            {
                case 2: return Act2;
                case 3: return Act3;
                default: return Act1;
            }
        }

        /// <summary>추월 "+1"(시제품 #7be08a — 카운트다운 GO!와 같은 초록).</summary>
        public static readonly Color RankUp = new Color32(0x7b, 0xe0, 0x8a, 0xff);

        /// <summary>밀림 "-1"(시제품 #ff5a4e).</summary>
        public static readonly Color RankDown = new Color32(0xff, 0x5a, 0x4e, 0xff);
    }
}
