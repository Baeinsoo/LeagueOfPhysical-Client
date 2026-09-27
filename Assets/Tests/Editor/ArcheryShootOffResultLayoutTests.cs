using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShootOffResultLayoutTests
    {
        private static ArcheryRoundPlacement P(string id, bool hit, float x, float y, int rank, int points = 0)
            => new ArcheryRoundPlacement(id, hit, new Vector2(x, y), new Vector2(x, y).magnitude, rank, points);

        private static string Name(string id) => id;

        [Test]
        public void 색은_id_문자열_순서로_정해지고_누구_화면에서나_같다()
        {
            var a = ArcheryShootOffResultLayout.Roster(new[] { "b", "a", "c" });
            var b = ArcheryShootOffResultLayout.Roster(new[] { "c", "b", "a" });
            Assert.AreEqual(ArcheryShootOffResultLayout.ColorOf(a, "a"), ArcheryShootOffResultLayout.ColorOf(b, "a"));
            Assert.AreEqual(new Color(1f, 0x4F / 255f, 0x5E / 255f), ArcheryShootOffResultLayout.ColorOf(a, "a"));
        }

        [Test]
        public void 다섯_번째_사람은_첫_색으로_돈다()
        {
            var roster = ArcheryShootOffResultLayout.Roster(new[] { "1", "2", "3", "4", "5" });
            Assert.AreEqual(ArcheryShootOffResultLayout.ColorOf(roster, "1"), ArcheryShootOffResultLayout.ColorOf(roster, "5"));
        }

        [Test]
        public void 명단에_없는_사람은_흰색()
        {
            var roster = ArcheryShootOffResultLayout.Roster(new[] { "a" });
            Assert.AreEqual(Color.white, ArcheryShootOffResultLayout.ColorOf(roster, "gone"));
        }

        [Test]
        public void 닫는_틱은_라운드_마감_더하기_틈_빼기_60()
        {
            Assert.AreEqual(1000L + 200L - 60L, ArcheryShootOffResultLayout.CloseTick(1000L, 200));
        }

        [Test]
        public void 과녁_가운데는_패널_가운데_오른쪽_위는_오른쪽_위()
        {
            var center = new Vector2(150f, 150f);
            Assert.AreEqual(center, ArcheryShootOffResultLayout.ToPanel(Vector2.zero, 0.5f, center, 100f));
            var p = ArcheryShootOffResultLayout.ToPanel(new Vector2(0.25f, 0.25f), 0.5f, center, 100f);
            Assert.AreEqual(200f, p.x, 1e-4f);
            Assert.AreEqual(100f, p.y, 1e-4f);   // 화면 y는 아래로 는다
        }

        [Test]
        public void 반지름의_1_25배_밖은_안_그린다()
        {
            Assert.IsTrue(ArcheryShootOffResultLayout.IsDrawn(new Vector2(0.6f, 0f), 0.5f));
            Assert.IsFalse(ArcheryShootOffResultLayout.IsDrawn(new Vector2(0.7f, 0f), 0.5f));
            Assert.IsFalse(ArcheryShootOffResultLayout.IsDrawn(Vector2.zero, 0f));   // 반지름을 모르면 안 그린다
        }

        [Test]
        public void 차이가_3cm면_접전_제목()
        {
            var r = new[] { P("a", true, 0.01f, 0f, 0), P("b", true, 0.04f, 0f, 1) };
            Assert.AreEqual("단 3cm 차이!", ArcheryShootOffResultLayout.Headline(r, Name));
        }

        [Test]
        public void 차이가_3cm를_넘으면_승_제목()
        {
            var r = new[] { P("a", true, 0.01f, 0f, 0), P("b", true, 0.042f, 0f, 1) };
            Assert.AreEqual("a 승!", ArcheryShootOffResultLayout.Headline(r, Name));
        }

        [Test]
        public void 차이가_1cm_미만이어도_1cm로_말한다()
        {
            var r = new[] { P("a", true, 0.010f, 0f, 0), P("b", true, 0.014f, 0f, 1) };
            Assert.AreEqual("단 1cm 차이!", ArcheryShootOffResultLayout.Headline(r, Name));
        }

        [Test]
        public void 둘째가_빗나갔으면_승_제목()
        {
            var r = new[] { P("a", true, 0.01f, 0f, 0), P("b", false, 0f, 0f, 1) };
            Assert.AreEqual("a 승!", ArcheryShootOffResultLayout.Headline(r, Name));
        }

        [Test]
        public void 전원_빗나가면_아무도_못_맞혔다()
        {
            var r = new[] { P("a", false, 0f, 0f, 1), P("b", false, 0f, 0f, 1) };
            Assert.AreEqual("아무도 못 맞혔다!", ArcheryShootOffResultLayout.Headline(r, Name));
            Assert.AreEqual(0, ArcheryShootOffResultLayout.PinOrder(r, 0.5f).Count);
        }

        [Test]
        public void 공동_1등이면_공동_1위()
        {
            var r = new[] { P("a", true, 0.02f, 0f, 0), P("b", true, 0.02f, 0f, 0) };
            Assert.AreEqual("공동 1위!", ArcheryShootOffResultLayout.Headline(r, Name));
        }

        [Test]
        public void 점은_꼴찌부터_찍고_빗나간_사람과_멀리_벗어난_점은_뺀다()
        {
            var r = new[]
            {
                P("a", true, 0.01f, 0f, 0), P("b", true, 0.1f, 0f, 1),
                P("c", true, 5f, 0f, 2), P("d", false, 0f, 0f, 3),
            };
            CollectionAssert.AreEqual(new[] { 1, 0 }, ArcheryShootOffResultLayout.PinOrder(r, 0.5f));
        }

        [Test]
        public void 점은_0_15초마다_하나씩_늘어난다()
        {
            Assert.AreEqual(1, ArcheryShootOffResultLayout.PinsShown(0f, 3));
            Assert.AreEqual(1, ArcheryShootOffResultLayout.PinsShown(0.14f, 3));
            Assert.AreEqual(2, ArcheryShootOffResultLayout.PinsShown(0.15f, 3));
            Assert.AreEqual(3, ArcheryShootOffResultLayout.PinsShown(9f, 3));
            Assert.AreEqual(0, ArcheryShootOffResultLayout.PinsShown(1f, 0));
        }

        [Test]
        public void 목록_문구()
        {
            Assert.AreEqual("1위", ArcheryShootOffResultLayout.RankLabel(0));
            Assert.AreEqual("+3", ArcheryShootOffResultLayout.PointsText(3, 1));
            Assert.AreEqual("+6 (2배)", ArcheryShootOffResultLayout.PointsText(6, 2));
            Assert.AreEqual("4cm", ArcheryShootOffResultLayout.DistanceText(true, 0.04f));
            Assert.AreEqual("빗나감", ArcheryShootOffResultLayout.DistanceText(false, 9f));
        }
    }
}
