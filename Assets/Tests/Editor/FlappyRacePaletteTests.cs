using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>색표(시제품 MUTED·rankColor)와 상대 색 칸. 막대의 점 색과 화면 속 새 색이 같아야 "저 점이 저 새"로 읽힌다.</summary>
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
        public void 상대_색은_시제품_MUTED_다섯()
        {
            Assert.AreEqual(5, FlappyRacePalette.Opponents.Length);
            AreClose(Hex(0x8a, 0xa7, 0xb8), FlappyRacePalette.Opponents[0]);
            AreClose(Hex(0xbc, 0x9c, 0x83), FlappyRacePalette.Opponents[4]);
            AreClose(Hex(0xff, 0xd2, 0x57), FlappyRacePalette.Mine);
        }

        [Test]
        public void 같은_상대는_늘_같은_색()
        {
            var colors = new FlappyOpponentColors();
            Color first = colors.ColorOf("a");
            colors.ColorOf("b");

            AreClose(first, colors.ColorOf("a"));
        }

        [Test]
        public void 다섯_상대는_서로_다른_색_여섯째는_되돌아_쓴다()
        {
            var colors = new FlappyOpponentColors();
            string[] ids = { "a", "b", "c", "d", "e" };
            for (int i = 0; i < ids.Length; i++)
            {
                AreClose(FlappyRacePalette.Opponents[i], colors.ColorOf(ids[i]));
            }

            AreClose(FlappyRacePalette.Opponents[0], colors.ColorOf("f"));
        }
    }
}
