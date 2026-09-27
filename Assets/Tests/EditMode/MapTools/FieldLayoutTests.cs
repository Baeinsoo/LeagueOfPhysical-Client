using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class FieldLayoutTests
    {
        const float StartX = 0f, Length = 612f, Spacing = 11.4f, Half = 10.92f;
        static readonly FlapArc Arc = new FlapArc(18.6f, 59f, 6.8f, 0.02f);
        static CourseProfile Compose()
            => CourseProfileRule.Compose(StartX, Length, Spacing, Half, 20260919UL, Spacing * 4f, Spacing * 8f, Arc);

        [Test]
        public void 샤프트는_구간마다_하나이고_깊이가_구간을_따른다()
        {
            var shafts = FieldLayout.PlaceShafts(Compose(), StartX, Length, 3, Half);
            Assert.AreEqual(3, shafts.Count);
            for (int s = 0; s < 3; s++)
            {
                Assert.AreEqual(FieldLayout.ShaftDepths[s], shafts[s].Depth);
                float lo = StartX + Length / 3f * s, hi = lo + Length / 3f;
                Assert.That(shafts[s].X0, Is.GreaterThanOrEqualTo(lo));
                Assert.That(shafts[s].X1, Is.LessThanOrEqualTo(hi));
                Assert.AreEqual(FieldLayout.ShaftWidth + FieldLayout.PocketFloor + FieldLayout.ChimneyWidth, shafts[s].Span, 1e-4f);
            }
        }

        [Test]
        public void 샤프트는_평지_안에만_선다()
        {
            var p = Compose();
            foreach (ShaftPiece s in FieldLayout.PlaceShafts(p, StartX, Length, 3, Half))
            {
                Assert.AreEqual(p.CenterAt(s.X0 - FieldLayout.SideWall), p.CenterAt(s.X1 + FieldLayout.SideWall), 1e-4f, "양 끝 높이가 같아야(평지)");
                Assert.AreEqual(p.CenterAt(s.X0), p.CenterAt((s.X0 + s.X1) * 0.5f), 1e-4f);
            }
        }

        [Test]
        public void 샤프트_앞뒤에는_관문이_못_선다()
        {
            var shafts = FieldLayout.PlaceShafts(Compose(), StartX, Length, 3, Half);
            ShaftPiece s = shafts[1];
            Assert.IsTrue(FieldLayout.GateBlocked(shafts, s.X0 - FieldLayout.GateClear + 0.1f));
            Assert.IsTrue(FieldLayout.GateBlocked(shafts, (s.X0 + s.X1) * 0.5f));
            Assert.IsFalse(FieldLayout.GateBlocked(shafts, s.X1 + FieldLayout.GateClear + 0.1f));
        }

        [Test]
        public void 기류는_계곡마다_하나_샤프트마다_둘이고_지름길_출구_뒤에서_시작한다()
        {
            var p = Compose();
            var shafts = FieldLayout.PlaceShafts(p, StartX, Length, 3, Half);
            var rects = FieldLayout.Airflows(p, shafts, Half);
            int ups = 0, downs = 0;
            foreach (var r in rects) { if (r.Kind == LOP.FlappyAirflowKind.Up) { ups++; } else { downs++; } }
            Assert.AreEqual(p.Valleys.Count + shafts.Count, ups);
            Assert.AreEqual(shafts.Count, downs);
            foreach (ShortcutRect sc in p.Shortcuts)
            {
                foreach (var r in rects)
                {
                    bool overlapsX = r.X1 > sc.X0 && r.X0 < sc.X1;
                    Assert.IsFalse(overlapsX && r.Kind == LOP.FlappyAirflowKind.Up && r.Y1 > sc.Y0,
                                   $"x={r.X0:F0} 상승기류가 지름길 지붕 밑까지 닿는다");
                }
            }
        }

        [Test]
        public void 기류_사각형끼리_안_겹친다()
        {
            var p = Compose();
            var rects = FieldLayout.Airflows(p, FieldLayout.PlaceShafts(p, StartX, Length, 3, Half), Half);
            for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                bool overlap = rects[i].X0 < rects[j].X1 && rects[j].X0 < rects[i].X1
                            && rects[i].Y0 < rects[j].Y1 && rects[j].Y0 < rects[i].Y1;
                Assert.IsFalse(overlap, $"{i}·{j} 겹침");
            }
        }

        [Test]
        public void 바닥_조각은_샤프트_구멍을_비운다()
        {
            var p = Compose();
            var shafts = FieldLayout.PlaceShafts(p, StartX, Length, 3, Half);
            foreach (RampPiece q in CourseProfileRule.FloorPieces(p, new List<float>(), shafts))
            {
                float mid = (q.X0 + q.X1) * 0.5f;
                foreach (ShaftPiece s in shafts)
                {
                    Assert.IsFalse(mid > s.X0 && mid < s.X1, $"x={mid:F1} 샤프트 구멍에 바닥이 있다");
                }
            }
        }
    }
}
