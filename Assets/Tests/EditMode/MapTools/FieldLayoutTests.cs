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
            //  셋 모두 기본 깊이(ShaftDepths) 그대로 들어간다.
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
            Assert.IsTrue(FieldLayout.GateBlocked(shafts, s.X0 - FieldLayout.GateClear + 0.1f, Spacing));
            Assert.IsTrue(FieldLayout.GateBlocked(shafts, (s.X0 + s.X1) * 0.5f, Spacing));
            //  굴뚝 뒤 간격 하나까지는 전용 홀로그램 관문 몫이다.
            Assert.IsTrue(FieldLayout.GateBlocked(shafts, s.X1 + FieldLayout.GateClear + Spacing - 0.1f, Spacing));
            Assert.IsFalse(FieldLayout.GateBlocked(shafts, s.X1 + FieldLayout.GateClear + Spacing + 0.1f, Spacing));
            Assert.IsFalse(FieldLayout.GateBlocked(shafts, s.X0 - FieldLayout.GateClear - 0.1f, Spacing));
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

        const float Finish = StartX + Length + Spacing;

        [Test]
        public void 전용_홀로그램_관문은_자리가_있는_샤프트마다_하나씩_굴뚝_바로_뒤_평지에_선다()
        {
            var p = Compose();
            var shafts = Shafts(p);
            var xs = FieldLayout.HologramGateXs(p, shafts, Finish);
            Assert.That(xs, Is.Not.Empty);
            int withRoom = 0;
            foreach (ShaftPiece s in shafts)
            {
                float want = s.X1 + FieldLayout.GateClear + FieldLayout.HologramGateOffset;
                bool room = p.GateAllowedAt(want, CourseProfileRule.GateMargin) && want <= Finish - FieldLayout.GateClear;
                int mine = 0;
                foreach (float x in xs)
                {
                    if (x > s.X1 && x < s.X1 + FieldLayout.GateClear + Spacing) { mine++; }
                }
                Assert.AreEqual(room ? 1 : 0, mine, $"x={s.X0:F0} 샤프트의 전용 관문 수");
                if (room) { withRoom++; }
            }
            Assert.AreEqual(withRoom, xs.Count);
            foreach (float x in xs)
            {
                Assert.IsTrue(p.GateAllowedAt(x, CourseProfileRule.GateMargin), $"x={x:F1} 전용 관문이 평지 밖이다");
                bool strictlyAfterSome = false;
                foreach (ShaftPiece s in shafts)
                {
                    if (x > s.X1 + FieldLayout.GateClear) { strictlyAfterSome = true; }
                    Assert.IsFalse(x > s.X0 - FieldLayout.GateClear && x <= s.X1 + FieldLayout.GateClear,
                                   $"x={x:F1} 전용 관문이 샤프트 구멍 위에 섰다");
                }
                Assert.IsTrue(strictlyAfterSome);
            }
        }

        [Test]
        //  샤프트를 고를 때 그 뒤 전용 관문 자리까지 재므로, 이 코스에선 모든 샤프트 뒤에 관문이
        //  선다(2026-09-28 — 두 번째 샤프트 뒤가 2.5m 모자라 빠졌었다).
        public void 모든_샤프트_뒤에_전용_홀로그램_관문이_선다()
        {
            var p = Compose();
            var shafts = Shafts(p);
            Assert.AreEqual(3, shafts.Count);
            Assert.AreEqual(shafts.Count, FieldLayout.HologramGateXs(p, shafts, Finish).Count);
        }

        [Test]
        //  샤프트는 평지 왼쪽 끝(옆벽 + 1m)에 붙는다 — 남은 평지를 한 덩어리로 뒤에 남겨야
        //  연속 4관문짜리 도전 구간이 들어간다. 가운데에 두면 평지가 둘로 잘려 둘 다 모자란다.
        public void 샤프트는_평지_왼쪽_끝에_붙는다()
        {
            var p = Compose();
            var shafts = Shafts(p);
            for (int s = 0; s < shafts.Count; s++)
            {
                float lo = StartX + Length / 3f * s;
                bool found = false;
                foreach (FlatSpan f in p.Flats)
                {
                    if (shafts[s].X0 < f.From || shafts[s].X1 > f.To) { continue; }
                    found = true;
                    float start = System.Math.Max(f.From, lo);
                    Assert.AreEqual(start + FieldLayout.SideWall + 1f, shafts[s].X0, 1e-3f, $"샤프트 {s}");
                }
                Assert.IsTrue(found, $"샤프트 {s}를 담는 평지가 없다");
            }
        }

        [Test]
        public void 전용_홀로그램_관문은_결승선_앞에_자리가_없으면_건너뛴다()
        {
            var p = Compose();
            var shafts = Shafts(p);
            Assert.That(shafts, Is.Not.Empty);
            ShaftPiece last = shafts[shafts.Count - 1];
            float tooClose = last.X1 + FieldLayout.GateClear + FieldLayout.HologramGateOffset + FieldLayout.GateClear - 0.1f;
            var far = FieldLayout.HologramGateXs(p, shafts, Finish);
            var close = FieldLayout.HologramGateXs(p, shafts, tooClose);
            //  이 코스의 마지막 샤프트는 결승선이 멀면 전용 관문이 선다 — 그래야 "건너뛴다"가 뭔가를 지킨다.
            Assert.AreEqual(far.Count - 1, close.Count);
            foreach (float x in close)
            {
                Assert.That(x, Is.LessThan(last.X0), "결승선에 바짝 붙은 전용 관문이 섰다");
            }
        }

        [Test]
        public void 보통_관문은_샤프트와_전용_홀로그램_관문_자리를_피한다()
        {
            var p = Compose();
            var shafts = Shafts(p);
            Assert.That(shafts, Is.Not.Empty);
            const float window = 4.37f;
            System.Func<float, bool> gateAllowed =
                x => p.GateAllowedAt(x, CourseProfileRule.GateMargin) && FieldLayout.GateBlocked(shafts, x, Spacing) == false;
            var pipes = ClassicCourseRule.Layout(StartX, Length, Spacing, -Half, Half, window, 6f, 20260919UL,
                                                 p.CenterAt, 6, gateAllowed);
            Assert.That(pipes, Is.Not.Empty);
            foreach (CoursePipe pipe in pipes)
            {
                foreach (ShaftPiece s in shafts)
                {
                    Assert.IsFalse(pipe.X >= s.X0 - FieldLayout.GateClear && pipe.X <= s.X1 + FieldLayout.GateClear + Spacing,
                                   $"x={pipe.X:F1} 보통 관문이 x={s.X0:F0} 샤프트 금지대에 섰다");
                }
            }
        }
    }
}
