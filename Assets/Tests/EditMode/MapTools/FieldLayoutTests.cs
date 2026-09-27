using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class FieldLayoutTests
    {
        const float StartX = 0f, Length = 612f, Spacing = 11.4f, Half = 10.92f;
        //  MasterData(TbFlappyConfig) 실측값 — 굴뚝 폭 공식이 이 값들로 검증돼야 한다.
        const float Forward = 6.8f, Up = 30f, Rise = 12f;
        static readonly FlapArc Arc = new FlapArc(18.6f, 59f, 6.8f, 0.02f);
        static CourseProfile Compose()
            => CourseProfileRule.Compose(StartX, Length, Spacing, Half, 20260919UL, Spacing * 4f, Spacing * 8f, Arc);
        static List<ShaftPiece> Shafts(CourseProfile p)
            => FieldLayout.PlaceShafts(p, StartX, Length, 3, Half, Forward, Up, Rise);

        [Test]
        public void 샤프트는_구간마다_하나이고_깊이가_구간을_따른다()
        {
            //  개수는 3으로 못박는다 — 이게 설계 의도다(2026-09-27 수정 2: need에서
            //  GateClear·GateMargin을 뺐다. 그건 GateBlocked가 이미 따로 지키므로 여기서
            //  또 셀 필요가 없었다). 실측(6.8/30/12, 이 파일의 Compose() 코스)으로 구간
            //  셋 모두 기본 깊이(15/20/25) 그대로 들어간다.
            var shafts = Shafts(Compose());
            Assert.AreEqual(3, shafts.Count);
            for (int s = 0; s < 3; s++)
            {
                Assert.AreEqual(FieldLayout.ShaftDepths[s], shafts[s].Depth);
                float lo = StartX + Length / 3f * s, hi = lo + Length / 3f;
                Assert.That(shafts[s].X0, Is.GreaterThanOrEqualTo(lo));
                Assert.That(shafts[s].X1, Is.LessThanOrEqualTo(hi));
                float chimneyWidth = FieldLayout.ChimneyWidthFor(shafts[s].Depth, Forward, Up, Rise);
                Assert.AreEqual(FieldLayout.ShaftWidth + FieldLayout.PocketFloor + chimneyWidth,
                                shafts[s].Span, 1e-4f);
            }
        }

        [Test]
        public void 샤프트_벽은_평지_한_조각_안에_통째로_들어간다()
        {
            var p = Compose();
            var shafts = Shafts(p);
            Assert.That(shafts, Is.Not.Empty);
            foreach (ShaftPiece s in shafts)
            {
                bool fitsInSomeFlat = false;
                foreach (FlatSpan f in p.Flats)
                {
                    if (s.X0 - FieldLayout.SideWall >= f.From && s.X1 + FieldLayout.SideWall <= f.To)
                    {
                        fitsInSomeFlat = true;
                        break;
                    }
                }
                Assert.IsTrue(fitsInSomeFlat, $"x=[{s.X0:F1},{s.X1:F1}] 옆벽까지 담는 평지가 없다");
            }
        }

        [Test]
        public void 굴뚝_폭은_그_깊이를_오를_시간을_담는다()
        {
            var shafts = Shafts(Compose());
            Assert.That(shafts, Is.Not.Empty);
            foreach (ShaftPiece s in shafts)
            {
                float climbTime = (s.Depth + 3f) / Rise + Rise / Up;
                float crossTime = (s.ChimneyWidth - 1f) / Forward;
                Assert.That(crossTime, Is.GreaterThanOrEqualTo(climbTime),
                            $"x={s.X0:F0} 굴뚝 폭 {s.ChimneyWidth:F1}m으로는 깊이 {s.Depth:F0}m을 못 오른다");
            }
        }

        [Test]
        public void 샤프트는_평지_안에만_선다()
        {
            var p = Compose();
            var shafts = Shafts(p);
            Assert.That(shafts, Is.Not.Empty);
            foreach (ShaftPiece s in shafts)
            {
                Assert.AreEqual(p.CenterAt(s.X0 - FieldLayout.SideWall), p.CenterAt(s.X1 + FieldLayout.SideWall), 1e-4f, "양 끝 높이가 같아야(평지)");
                Assert.AreEqual(p.CenterAt(s.X0), p.CenterAt((s.X0 + s.X1) * 0.5f), 1e-4f);
            }
        }

        [Test]
        public void 샤프트_앞뒤에는_관문이_못_선다()
        {
            var shafts = Shafts(Compose());
            Assert.That(shafts, Is.Not.Empty);
            ShaftPiece s = shafts[0];
            Assert.IsTrue(FieldLayout.GateBlocked(shafts, s.X0 - FieldLayout.GateClear + 0.1f));
            Assert.IsTrue(FieldLayout.GateBlocked(shafts, (s.X0 + s.X1) * 0.5f));
            Assert.IsFalse(FieldLayout.GateBlocked(shafts, s.X1 + FieldLayout.GateClear + 0.1f));
        }

        [Test]
        public void 기류는_계곡마다_하나_샤프트마다_둘이고_지름길_출구_뒤에서_시작한다()
        {
            var p = Compose();
            var shafts = Shafts(p);
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
            var rects = FieldLayout.Airflows(p, Shafts(p), Half);
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
            var shafts = Shafts(p);
            foreach (RampPiece q in CourseProfileRule.FloorPieces(p, new List<float>(), shafts))
            {
                float mid = (q.X0 + q.X1) * 0.5f;
                foreach (ShaftPiece s in shafts)
                {
                    Assert.IsFalse(mid > s.X0 && mid < s.X1, $"x={mid:F1} 샤프트 구멍에 바닥이 있다");
                }
            }
        }

        [Test]
        public void 홀로그램_관문이_없으면_전부_음수다()
        {
            var shafts = Shafts(Compose());
            var gates = FieldLayout.HologramGates(new List<CoursePipe>(), shafts);
            Assert.AreEqual(shafts.Count, gates.Count);
            foreach (int g in gates) { Assert.AreEqual(-1, g); }
        }

        [Test]
        public void 홀로그램_관문은_같은_관문을_두_샤프트가_같이_쓰지_않는다()
        {
            //  두 샤프트를 붙여 두고, "둘 다에게 가장 가까운" 후보 관문 하나와 그보다 먼
            //  대체 관문 하나만 준다 — 앞 샤프트가 가까운 쪽을 집으면 뒤 샤프트는 같은 걸
            //  다시 못 쓰고 대체 관문으로 밀려나야 한다.
            float w = FieldLayout.ChimneyWidthFor(15f, Forward, Up, Rise);
            var shaft0 = new ShaftPiece(0f, 0f, 15f, w);
            var shaft1 = new ShaftPiece(shaft0.X1 + 5f, 0f, 15f, w);
            var shafts = new List<ShaftPiece> { shaft0, shaft1 };

            float nearX = shaft1.X1 + FieldLayout.GateClear + 1f;
            float farX = nearX + 30f;
            var pipes = new List<CoursePipe> { new CoursePipe(nearX, 0f), new CoursePipe(farX, 0f) };

            var gates = FieldLayout.HologramGates(pipes, shafts);
            Assert.AreEqual(shafts.Count, gates.Count);
            Assert.AreEqual(0, gates[0]);
            Assert.AreEqual(1, gates[1]);
            Assert.AreNotEqual(gates[0], gates[1]);
        }
    }
}
