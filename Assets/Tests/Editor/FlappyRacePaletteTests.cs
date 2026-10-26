using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>색표(시제품 MUTED·rankColor). 게임은 더 이상 선수에게 색을 배정하지 않는다(사용자 결정 10-25) —
    /// 내 점만 노랑으로 남고, 상대는 모두 같은 중립색을 쓴다.</summary>
    public class FlappyRacePaletteTests
    {
        private static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        private static void AreClose(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f);
            Assert.AreEqual(expected.g, actual.g, 1e-3f);
            Assert.AreEqual(expected.b, actual.b, 1e-3f);
        }

        [Test]
        public void 순위색은_금은동_그다음_흰색()
        {
            AreClose(Hex(0xff, 0xd2, 0x57), FlappyRacePalette.RankColor(1));
            AreClose(Hex(0xdf, 0xe6, 0xee), FlappyRacePalette.RankColor(2));
            AreClose(Hex(0xe7, 0xa4, 0x6a), FlappyRacePalette.RankColor(3));
            AreClose(Hex(0xf3, 0xeb, 0xe2), FlappyRacePalette.RankColor(4));
            AreClose(Hex(0xf3, 0xeb, 0xe2), FlappyRacePalette.RankColor(6));
        }

        [Test]
        public void 내_색은_시제품_MUTED_첫칸()
        {
            AreClose(Hex(0xff, 0xd2, 0x57), FlappyRacePalette.Mine);
        }

        //  사용자 결정(10-25): 게임이 색을 배정하지 않는다 — 새 모습은 옷이 정한다. 그래서 상대는 전원
        //  칸별 색이 아니라 어두운 막대 위에서 읽히는 차분한 중립색 하나를 같이 쓴다.
        [Test]
        public void 상대_색은_중립_한_칸뿐이다()
        {
            AreClose(Hex(0xa9, 0xa2, 0x9a), FlappyRacePalette.OpponentNeutral);
        }

        [Test]
        public void 막_색은_호박_하늘_분홍()
        {
            //  시제품 ACT_COL — 진행 막대 띠와 구간 배너 띠가 같이 쓴다.
            AreClose(Hex(0xff, 0xb3, 0x47), FlappyRacePalette.ActColor(1));
            AreClose(Hex(0x5f, 0xd3, 0xff), FlappyRacePalette.ActColor(2));
            AreClose(Hex(0xff, 0x8f, 0xb1), FlappyRacePalette.ActColor(3));
            AreClose(FlappyRacePalette.ActColor(1), FlappyRacePalette.ActColor(0));
        }

        [Test]
        public void 추월은_초록_밀림은_빨강()
        {
            AreClose(Hex(0x7b, 0xe0, 0x8a), FlappyRacePalette.RankUp);
            AreClose(Hex(0xff, 0x5a, 0x4e), FlappyRacePalette.RankDown);
        }
    }
}
