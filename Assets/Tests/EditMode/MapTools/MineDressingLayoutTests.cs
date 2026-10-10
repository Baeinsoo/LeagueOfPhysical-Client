using System;
using System.Collections.Generic;
using System.Linq;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 광산 옷의 놓을 자리. 요점은 "그림 경계 = 판정 경계" — 데크 윗면이 바닥선에, 천장 조각 아랫면이 천장선에,
    /// 기둥 칸이 파이프 높이에 정확히 맞는가. 실제 코스(MineCourseRule.Layout)로 잰다.
    /// </summary>
    public class MineDressingLayoutTests
    {
        static readonly MinePhysics P = new MinePhysics(4.5f, 10.125f, 33.75f, 11.25f, 0.02f);
        static MineCourse Course() => MineCourseRule.Layout(P);

        //  임의 범위(스펙 §3 예시 값) — 순수 함수(범위를 받기만 하는 레이아웃 계산)를 재는 테스트에서만 쓴다.
        //  실제 굽기 범위는 코스에 달렸다(10-09 3막: [-20, c.FlatEnd()] — 입구 바로 뒤가 수직 갱이라 짧다). 그 범위를
        //  재는 테스트는 따로 c.FlatEnd()를 쓴다(아래 FlatEnd-의존 테스트들).
        const float From = -20f, To = 94.25f;
        const float Eps = 0.001f;

        static readonly MineDressingLayout.BackgroundDensity Density = new MineDressingLayout.BackgroundDensity(9f, 0.5f, 16f, 2, 0.75f, 0.8f);

        static float Floor(MineCourse c, float x) => c.CenterAt(x) - c.HalfAt(x);
        static float Ceiling(MineCourse c, float x) => c.CenterAt(x) + c.HalfAt(x);

        //  조각의 양끝 — 원점을 중심으로 각도만큼 돌린 길이 Length의 선분.
        static (float x0, float y0, float x1, float y1) Ends(MinePiece p)
        {
            double a = p.AngleDegrees * Math.PI / 180.0, h = p.Length / 2.0;
            return ((float)(p.X - h * Math.Cos(a)), (float)(p.Y - h * Math.Sin(a)),
                    (float)(p.X + h * Math.Cos(a)), (float)(p.Y + h * Math.Sin(a)));
        }

        // ── 관문 기둥 ──

        [TestCase(-7.28f, -1.2f, true)]
        [TestCase(2.55f, 7.28f, false)]
        [TestCase(0f, 3f, true)]
        [TestCase(0f, 0.4f, false)]
        public void 기둥_칸_높이_합은_기둥_높이고_마지막_칸만_1_미만(float bottom, float top, bool capAtTop)
        {
            var s = MineDressingLayout.GateStack(bottom, top, capAtTop);
            Assert.AreEqual(top - bottom, s.Cells.Sum(cell => cell.Height), Eps);
            Assert.AreEqual(bottom, s.Cells[0].Y0, Eps);
            for (int i = 0; i < s.Cells.Count; i++)
            {
                var cell = s.Cells[i];
                Assert.Greater(cell.Height, 0f);
                Assert.LessOrEqual(cell.Height, 1f + Eps);
                if (i < s.Cells.Count - 1)
                {
                    Assert.AreEqual(1f, cell.Height, Eps, $"{i}번째 칸은 마지막이 아닌데 1 m가 아니다");
                    Assert.AreEqual(cell.Y0 + cell.Height, s.Cells[i + 1].Y0, Eps, "칸 사이가 벌어졌다");
                }
            }
        }

        [Test]
        public void 기둥_칸_수는_올림()
        {
            Assert.AreEqual(6, MineDressingLayout.GateStack(-7.28f, -1.8f, true).Cells.Count);   // 5.48 m
            Assert.AreEqual(3, MineDressingLayout.GateStack(0f, 3f, true).Cells.Count);          // 딱 3 m — 빈 마지막 칸 없음
            Assert.AreEqual(0, MineDressingLayout.GateStack(1f, 1f, true).Cells.Count);
        }

        [Test]
        public void 쇠테는_틈_쪽_끝에()
        {
            //  아래 관문: 틈이 위 — 쇠테 원점(윗면) = top, 뒤집지 않는다.
            var low = MineDressingLayout.GateStack(-7.28f, -1.2f, true);
            Assert.AreEqual(-1.2f, low.CapY, Eps);
            Assert.IsFalse(low.CapFlipped);
            //  위 관문: 틈이 아래 — Z축 180°로 뒤집어 원점이 아랫면 = bottom.
            var high = MineDressingLayout.GateStack(2.55f, 7.28f, false);
            Assert.AreEqual(2.55f, high.CapY, Eps);
            Assert.IsTrue(high.CapFlipped);
        }

        [Test]
        public void 쇠띠는_틈_쪽_끝에서_2m마다_기둥_안에()
        {
            var low = MineDressingLayout.GateStack(-7.28f, -1.2f, true);
            CollectionAssert.AreEqual(new[] { -3.2f, -5.2f }, low.StrapYs.ToArray(), new FloatComparer(Eps));
            var high = MineDressingLayout.GateStack(-5f, 4f, false);
            CollectionAssert.AreEqual(new[] { -3f, -1f, 1f, 3f }, high.StrapYs.ToArray(), new FloatComparer(Eps));
            //  짧은 기둥엔 쇠띠가 없다(쇠테만).
            Assert.AreEqual(0, MineDressingLayout.GateStack(0f, 1.5f, true).StrapYs.Count);
        }

        // ── 관문 가로 칸(긴 관문) ──

        [Test]
        public void 보통_관문은_한_칸이고_늘이지_않는다()
        {
            var cols = MineDressingLayout.GateColumns(10f, MineDressingLayout.GateUnitWidth);
            Assert.AreEqual(1, cols.Count);
            Assert.AreEqual(10f, cols[0].X, Eps);
            Assert.AreEqual(1f, cols[0].ScaleX, Eps);
        }

        [Test]
        public void 긴_관문은_늘이지_않고_같은_폭_칸을_빈틈없이_잇는다()
        {
            //  긴 통로 관문(폭 12) — 한 칸을 6.15배로 늘이면 볼트가 찌그러진다. 1.95 이하 칸 7개로 나눈다.
            const float x = 41.6f, width = 12f;
            var cols = MineDressingLayout.GateColumns(x, width);
            Assert.AreEqual(7, cols.Count);
            float unit = MineDressingLayout.GateUnitWidth;
            Assert.AreEqual(x - width / 2f, cols[0].X - cols[0].ScaleX * unit / 2f, Eps, "왼끝이 파이프 왼끝과 다르다");
            Assert.AreEqual(x + width / 2f, cols[6].X + cols[6].ScaleX * unit / 2f, Eps, "오른끝이 파이프 오른끝과 다르다");
            for (int i = 0; i < cols.Count; i++)
            {
                Assert.LessOrEqual(cols[i].ScaleX, 1f + Eps, $"{i}번째 칸을 늘였다");
                Assert.Greater(cols[i].ScaleX, 0.8f, $"{i}번째 칸이 너무 좁다(가는 자투리)");
                if (i > 0)
                {
                    Assert.AreEqual(cols[i - 1].X + cols[i - 1].ScaleX * unit / 2f, cols[i].X - cols[i].ScaleX * unit / 2f, Eps, $"{i}번째 이음매");
                }
            }
        }

        [Test]
        public void 관문_폭의_float_오차로_자투리_칸이_생기지_않는다()
        {
            var cols = MineDressingLayout.GateColumns(0f, 3.9000002f);
            Assert.AreEqual(2, cols.Count);
            Assert.AreEqual(1f, cols[1].ScaleX, Eps);
        }

        // ── 바닥 비계 ──

        [Test]
        public void 비계_데크_윗면이_바닥선에_붙는다()
        {
            //  10-09 3막: 보기 구간이 94.25까지 평평하다는 가정이 깨졌다(입구 바로 뒤가 수직 갱) —
            //  실제 평평한 끝(FlatEnd)까지로 좁혀서 같은 걸 확인한다.
            var c = Course();
            float to = c.FlatEnd();
            var bays = MineDressingLayout.TrestleBays(c, From, to);
            Assert.Greater(bays.Count, 10);
            foreach (var b in bays)
            {
                Assert.AreEqual(MinePartKind.TrestleBay, b.Kind);
                Assert.AreEqual(Floor(c, b.X), b.Y, Eps, $"x={b.X} 데크 가운데");
                var (x0, y0, x1, y1) = Ends(b);
                Assert.AreEqual(Floor(c, x0), y0, Eps, $"x={b.X} 데크 왼끝");
                Assert.AreEqual(Floor(c, x1), y1, Eps, $"x={b.X} 데크 오른끝");
            }
        }

        [Test]
        public void 비틀린_바닥에서도_데크_양끝이_바닥선에()
        {
            //  10-09 3막: 첫 수직 갱 낙하(24.8~32.8) — 8 m 만에 22 m 떨어지는, 가장 가파른 경우(현 기울기 약 70°).
            //  (옛 주석의 156.65~는 지금 순서에선 낮은 천장 꼬리·롤러코스터 앞이라 완만한 물결일 뿐 — 가장 가파른 경우를 안 지켰다.)
            var c = Course();
            var bays = MineDressingLayout.TrestleBays(c, 20f, 40f);
            Assert.IsTrue(bays.Any(b => Math.Abs(b.AngleDegrees) > 45f), "가장 가파른 수직 갱 낙하 칸이 없다");
            foreach (var b in bays)
            {
                var (x0, y0, x1, y1) = Ends(b);
                Assert.AreEqual(Floor(c, x0), y0, 0.01f, $"x={b.X} 왼끝");
                Assert.AreEqual(Floor(c, x1), y1, 0.01f, $"x={b.X} 오른끝");
            }
            //  천장도 — 여기선 수직 갱이 좁아져 HalfAt이 7.28이 아니다.
            foreach (var p in MineDressingLayout.CeilingPieces(c, 20f, 40f))
            {
                var (x0, y0, x1, y1) = Ends(p);
                Assert.AreEqual(Ceiling(c, x0), y0, 0.01f, $"x={p.X} 천장 왼끝");
                Assert.AreEqual(Ceiling(c, x1), y1, 0.01f, $"x={p.X} 천장 오른끝");
            }
        }

        [Test]
        public void 비계는_범위를_빈틈없이_덮고_밖으로_안_나간다()
        {
            //  10-10: 칸이 꺾이는 곳(Breaks)에서 끊기므로 수직 갱 전이·꺾은선 구간 칸은 0.5~3 m로 들쭉날쭉하다 —
            //  "2.4 근처 폭"은 평평한 앞부분(FlatEnd 앞)에서만 본다. 빈틈·겹침·범위 검사는 범위 전체 그대로.
            var c = Course();
            var bays = MineDressingLayout.TrestleBays(c, From, To);
            AssertTiles(bays, From, To, MineDressingLayout.TrestleBayWidth, c.FlatEnd());
        }

        // ── 천장 ──

        [Test]
        public void 천장_조각_아랫면이_천장선에_붙는다()
        {
            //  10-09 3막: 위와 같은 이유로 평평한 끝(FlatEnd)까지로 좁힌다.
            var c = Course();
            float to = c.FlatEnd();
            var pieces = MineDressingLayout.CeilingPieces(c, From, to);
            foreach (var p in pieces)
            {
                Assert.AreEqual(Ceiling(c, p.X), p.Y, Eps, $"x={p.X} 가운데");
                var (x0, y0, x1, y1) = Ends(p);
                Assert.AreEqual(Ceiling(c, x0), y0, Eps, $"x={p.X} 왼끝");
                Assert.AreEqual(Ceiling(c, x1), y1, Eps, $"x={p.X} 오른끝");
            }
            AssertTiles(pieces, From, to, MineDressingLayout.CeilingPieceWidth);
        }

        [Test]
        public void 들보는_노을_바깥에만_바위는_굴에만()
        {
            var c = Course();
            float outsideEnd = MineDressingLayout.Zones(c).OutsideEnd;
            var pieces = MineDressingLayout.CeilingPieces(c, From, To);
            Assert.IsTrue(pieces.Any(p => p.Kind == MinePartKind.Beam));
            Assert.IsTrue(pieces.Any(p => p.Kind == MinePartKind.CeilingRock));
            foreach (var p in pieces)
            {
                var expect = p.X < outsideEnd ? MinePartKind.Beam : MinePartKind.CeilingRock;
                Assert.AreEqual(expect, p.Kind, $"x={p.X}");
                //  축척은 부품 단위 길이 기준(들보 1 m, 바위 2 m).
                float unit = p.Kind == MinePartKind.Beam ? MineDressingLayout.BeamUnit : MineDressingLayout.CeilingRockUnit;
                Assert.AreEqual(p.Length / unit, p.ScaleX, Eps);
            }
        }

        [Test]
        public void 꺾이는_곳에서도_바닥_천장_칸_양끝이_판정선_위이고_칸_안에서_꺾이지_않는다()
        {
            var c = MineCourseRule.Layout(P);
            float to = c.Sections.First(s => s.Name == "레일 롤러코스터").X;
            var breaks = c.Breaks(MineDressingLayout.BreakStep);
            foreach (var (kind, pieces, line) in new[]
            {
                ("바닥", MineDressingLayout.TrestleBays(c, -20f, to), (Func<float, float>)(x => c.CenterAt(x) - c.HalfAt(x))),
                ("천장", MineDressingLayout.CeilingPieces(c, -20f, to), (Func<float, float>)(x => c.CenterAt(x) + c.HalfAt(x))),
            })
            {
                foreach (MinePiece p in pieces)
                {
                    float x0 = p.X - p.Width / 2f, x1 = p.X + p.Width / 2f;
                    double rad = p.AngleDegrees * Math.PI / 180.0, half = p.Length / 2.0;
                    float ya = (float)(p.Y - Math.Sin(rad) * half), yb = (float)(p.Y + Math.Sin(rad) * half);
                    Assert.AreEqual(line(x0), ya, 0.01f, $"{kind} x={x0} 왼끝");
                    Assert.AreEqual(line(x1), yb, 0.01f, $"{kind} x={x1} 오른끝");
                    //  칸 안쪽에 끊는 점이 있으면 그 점도 현 위에 있어야 한다(= 칸 안에서 꺾이지 않는다). 곡선(낮은 천장)도 0.5 m 간격이라 0.05 안.
                    foreach (float bx in breaks.Where(bx => bx > x0 + 1e-3f && bx < x1 - 1e-3f))
                    {
                        float chordY = ya + (yb - ya) * (bx - x0) / (x1 - x0);
                        Assert.AreEqual(line(bx), chordY, 0.05f, $"{kind} 칸 [{x0},{x1}] 안 x={bx}");
                    }
                }
            }
        }

        // ── 배경 ──

        [Test]
        public void 배경은_같은_시드면_같고_다른_시드면_다르다()
        {
            var c = Course();
            var a = MineDressingLayout.Background(c, From, To, 7, Density);
            var b = MineDressingLayout.Background(c, From, To, 7, Density);
            var d = MineDressingLayout.Background(c, From, To, 8, Density);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i], b[i], $"{i}번째"); }
            Assert.IsFalse(a.Count == d.Count && a.Zip(d, (p, q) => p.Equals(q)).All(eq => eq), "시드를 바꿨는데 똑같다");
        }

        [Test]
        public void 배경_층은_안개_가운데_먼_비계_실루엣_순으로_멀어진다()
        {
            Assert.Less(MineDressingLayout.HazeZ, MineDressingLayout.LanternZ);
            Assert.Less(MineDressingLayout.LanternZ, MineDressingLayout.MidZ);
            Assert.Less(MineDressingLayout.MidZ, MineDressingLayout.FarHazeZ, "먼 안개 막이 가운데 층 앞에 있다");
            Assert.Less(MineDressingLayout.FarHazeZ, MineDressingLayout.FarZ, "먼 안개 막이 먼 비계 뒤에 있다");
            Assert.Less(MineDressingLayout.FarZ, MineDressingLayout.SilhouetteNearZ);
            Assert.Less(MineDressingLayout.SilhouetteNearZ, MineDressingLayout.SilhouetteFarZ);
            //  먼 비계는 예전 z 11보다 확실히 뒤(관문과 다투지 않게 — 10-08 캡처).
            Assert.GreaterOrEqual(MineDressingLayout.FarZ, 18f);
        }

        [Test]
        public void 배경은_판정면_뒤에만()
        {
            foreach (var p in MineDressingLayout.Background(Course(), From, To, 7, Density))
            {
                Assert.Greater(p.Z, 2.2f, $"{p.Kind} x={p.X} z={p.Z} — 안개 막 앞에 나왔다");
            }
        }

        [Test]
        public void 실루엣은_바깥이_협곡_굴이_굴벽이고_범위_앞뒤_15m를_덮는다()
        {
            var c = Course();
            float seam = MineDressingLayout.Zones(c).EntranceEnd;
            var bg = MineDressingLayout.Background(c, From, To, 7, Density);
            var sil = bg.Where(p => p.Kind == MinePartKind.CanyonSilhouette || p.Kind == MinePartKind.CaveSilhouette).ToList();
            foreach (var p in sil)
            {
                //  10-10: 협곡/굴벽 이음매는 갱 입구 문틀 끝(EntranceEnd) — 노을 하늘이 거기까지다.
                bool outside = p.X < seam;
                Assert.AreEqual(outside ? MinePartKind.CanyonSilhouette : MinePartKind.CaveSilhouette, p.Kind, $"x={p.X}");
                Assert.That(p.Z, Is.InRange(MineDressingLayout.SilhouetteNearZ, MineDressingLayout.SilhouetteFarZ));
                //  먼 비계보다 뒤 — 앞이면 비계 가운데를 실루엣 띠가 가린다.
                Assert.Greater(p.Z, MineDressingLayout.FarZ, $"x={p.X} 실루엣이 먼 비계 앞에 있다");
                //  띠(20 m)를 현 길이에 맞춰 늘인다 — 평평한 곳은 27.2 m 근처(1.36배 근처).
                Assert.AreEqual(p.Length / 20f, p.ScaleX, Eps, $"x={p.X} 폭 축척");
                Assert.LessOrEqual(p.Width, MineDressingLayout.SilhouetteWidth * 1.5f + Eps, $"x={p.X} 띠가 너무 길다");
            }
            //  층마다(먼·가까운, 바닥 쪽) 빈틈없이.
            foreach (int layer in new[] { 0, 1 })
            {
                var floorSide = sil.Where(p => p.Layer == layer && !p.Flipped).ToList();
                AssertCoversByWidth(floorSide, From - MineDressingLayout.BackgroundMargin, To + MineDressingLayout.BackgroundMargin);
            }
            //  천장 쪽 실루엣은 굴에만.
            Assert.IsTrue(sil.Where(p => p.Flipped).All(p => p.Kind == MinePartKind.CaveSilhouette));
            Assert.IsTrue(sil.Any(p => p.Flipped));
        }

        [Test]
        public void 먼_비계는_사인_높이로_범위_앞뒤_15m를_덮는다()
        {
            //  10-09 3막: 이 테스트의 주장은 "평평한 구간 안에서 현(chord)이 사인 곡선과 맞는다"다 —
            //  margin(15 m)까지 포함해 꺾이지 않아야 하므로 to = FlatEnd − margin(4.8)로 좁힌다.
            //  배경이 수직 갱 꺾임을 가로지르는 문제(실제 옷 입히기 범위)는 "코스 전체에 펼치기" 슬라이스의 몫 — 여기서 늘리지 않는다.
            var c = Course();
            float to = c.FlatEnd() - MineDressingLayout.BackgroundMargin;
            var far = MineDressingLayout.Background(c, From, to, 7, Density).Where(p => p.Kind == MinePartKind.BgTrestleBay).ToList();
            //  멀리 보이게 줄인 칸 폭(3 m × FarScale)으로 빈틈없이.
            AssertCovers(far, From - MineDressingLayout.BackgroundMargin, to + MineDressingLayout.BackgroundMargin,
                         MineDressingLayout.BgTrestleBayWidth * Density.FarScale);
            //  범위가 좁아져도(−35~19.8, 실측 24칸) 여러 칸을 뜻있게 검사하도록 최소치를 둔다.
            Assert.Greater(far.Count, 15);
            foreach (var p in far)
            {
                Assert.AreEqual(MineDressingLayout.FarZ, p.Z, Eps);
                Assert.AreEqual(Density.FarScale, p.ScaleY, Eps, $"x={p.X} 높이 축척");
                //  폭 축척 = 현 길이 / 3 — 기운 칸은 현이 칸 폭보다 길어 FarScale보다 조금 크다.
                Assert.AreEqual(p.Length / MineDressingLayout.BgTrestleBayWidth, p.ScaleX, Eps, $"x={p.X} 폭 축척");
                Assert.AreEqual(Density.FarScale * MineDressingLayout.BgTrestleBayWidth, p.Width, Eps, $"x={p.X} 칸 폭");
                //  시안: −1.0 + 2.5·sin(i·0.45), i = 3 m 칸 번호 — 칸 가운데에서 이어진 곡선과 같다.
                float expect = c.CenterAt(p.X) - 1.0f + 2.5f * (float)Math.Sin(p.X / 3.0 * 0.45);
                //  칸은 양끝을 잇는 현이라 가운데가 곡선보다 최대 2.5·0.15²·1.5²/2 ≈ 0.063 낮다.
                Assert.AreEqual(expect, p.Y, 0.08f, $"x={p.X}");
            }
        }

        [Test]
        public void 광차는_정한_수만큼_먼_비계_레일_위에()
        {
            var c = Course();
            var bg = MineDressingLayout.Background(c, From, To, 7, Density);
            var carts = bg.Where(p => p.Kind == MinePartKind.MineCart).ToList();
            Assert.AreEqual(2, carts.Count);
            var far = bg.Where(p => p.Kind == MinePartKind.BgTrestleBay).ToList();
            foreach (var cart in carts)
            {
                Assert.That(cart.X, Is.InRange(From, To));
                //  비계를 줄인 만큼 레일 높이도 줄고, 광차도 같은 축척.
                Assert.IsTrue(far.Any(f => Math.Abs(f.X - cart.X) < Eps && Math.Abs(f.Y + MineDressingLayout.RailTop * Density.FarScale - cart.Y) < Eps
                                           && Math.Abs(f.Z - cart.Z) < Eps), $"x={cart.X} 광차가 비계 위에 없다");
                Assert.AreEqual(Density.FarScale, cart.ScaleX, Eps);
                Assert.AreEqual(Density.FarScale, cart.ScaleY, Eps);
            }
            Assert.AreEqual(carts.Count, carts.Select(p => p.X).Distinct().Count(), "광차 둘이 한 칸에");
        }

        [Test]
        public void 가운데_층_틀_사다리_발판_랜턴은_굴에만_간격대로()
        {
            var c = Course();
            float seam = MineDressingLayout.Zones(c).EntranceEnd;
            var bg = MineDressingLayout.Background(c, From, To, 7, Density);
            var frames = bg.Where(p => p.Kind == MinePartKind.BgFrame).OrderBy(p => p.X).ToList();
            var ladders = bg.Where(p => p.Kind == MinePartKind.Ladder).ToList();
            var walks = bg.Where(p => p.Kind == MinePartKind.Walkway).ToList();
            var lanterns = bg.Where(p => p.Kind == MinePartKind.Lantern).OrderBy(p => p.X).ToList();

            Assert.Greater(frames.Count, 5);
            for (int i = 1; i < frames.Count; i++) { Assert.AreEqual(Density.FrameSpacing, frames[i].X - frames[i - 1].X, Eps); }
            Assert.IsTrue(frames.Concat(ladders).Concat(walks).Concat(lanterns).All(p => p.X >= seam), "굴 배경은 갱 입구 문틀 끝(EntranceEnd) 뒤부터");
            Assert.IsTrue(frames.All(p => Math.Abs(p.Z - MineDressingLayout.MidZ) < Eps));
            //  가운데 층은 MidScale로 줄인다(틀·발판·사다리 모두).
            Assert.IsTrue(frames.Concat(walks).Concat(ladders).All(p => Math.Abs(p.ScaleX - Density.MidScale) < Eps && Math.Abs(p.ScaleY - Density.MidScale) < Eps));
            Assert.AreEqual(frames.Count, walks.Count);
            //  사다리 비율 0.5 — 전부도 아니고 하나도 없지도 않다.
            Assert.That(ladders.Count, Is.InRange(1, frames.Count - 1));

            Assert.Greater(lanterns.Count, 2);
            for (int i = 1; i < lanterns.Count; i++)
            {
                Assert.AreEqual(Density.LanternSpacing, lanterns[i].X - lanterns[i - 1].X, 2.0f + Eps);
            }
        }

        [Test]
        public void 사다리_비율_0이면_없고_1이면_틀마다()
        {
            var c = Course();
            var none = MineDressingLayout.Background(c, From, To, 7, new MineDressingLayout.BackgroundDensity(9f, 0f, 16f, 2, 0.75f, 0.8f));
            Assert.AreEqual(0, none.Count(p => p.Kind == MinePartKind.Ladder));
            var all = MineDressingLayout.Background(c, From, To, 7, new MineDressingLayout.BackgroundDensity(9f, 1f, 16f, 2, 0.75f, 0.8f));
            Assert.AreEqual(all.Count(p => p.Kind == MinePartKind.BgFrame), all.Count(p => p.Kind == MinePartKind.Ladder));
        }

        // ── 도우미 ──

        //  가운데 정렬 조각들이 [from, to]를 빈틈·겹침 없이 덮고, 밖으로 나가지 않는다. 칸 폭은 단위 폭 근처(±10%) —
        //  오른끝이 widthCheckEnd 이하인 칸만(꺾이는 곳에서 끊긴 칸은 짧거나 길 수 있다).
        static void AssertTiles(List<MinePiece> pieces, float from, float to, float unit, float widthCheckEnd = float.PositiveInfinity)
        {
            var s = pieces.OrderBy(p => p.X).ToList();
            Assert.AreEqual(from, s[0].X - s[0].Width / 2f, Eps, "첫 조각이 from에서 시작하지 않는다");
            Assert.AreEqual(to, s[s.Count - 1].X + s[s.Count - 1].Width / 2f, Eps, "마지막 조각이 to에서 끝나지 않는다");
            for (int i = 0; i < s.Count; i++)
            {
                if (s[i].X + s[i].Width / 2f <= widthCheckEnd + Eps) { Assert.That(s[i].Width, Is.InRange(unit * 0.9f, unit * 1.1f)); }
                Assert.That(s[i].X - s[i].Width / 2f, Is.GreaterThanOrEqualTo(from - Eps));
                Assert.That(s[i].X + s[i].Width / 2f, Is.LessThanOrEqualTo(to + Eps));
                if (i > 0) { Assert.AreEqual(s[i - 1].X + s[i - 1].Width / 2f, s[i].X - s[i].Width / 2f, Eps, $"x={s[i].X} 이음매"); }
            }
        }

        //  가운데 정렬 조각들이 [from, to]를 빈틈없이 덮는다(밖으로 넘쳐도 된다 — 배경).
        static void AssertCovers(List<MinePiece> pieces, float from, float to, float width)
        {
            var s = pieces.OrderBy(p => p.X).ToList();
            Assert.IsNotEmpty(s);
            Assert.LessOrEqual(s[0].X - width / 2f, from + Eps);
            Assert.GreaterOrEqual(s[s.Count - 1].X + width / 2f, to - Eps);
            for (int i = 1; i < s.Count; i++)
            {
                Assert.LessOrEqual(s[i].X - width / 2f, s[i - 1].X + width / 2f + Eps, $"x={s[i].X} 앞에 빈틈");
            }
        }

        //  폭이 제각각인 가운데 정렬 조각들이 [from, to]를 빈틈없이 덮는다(각 조각의 Width).
        static void AssertCoversByWidth(List<MinePiece> pieces, float from, float to)
        {
            var s = pieces.OrderBy(p => p.X).ToList();
            Assert.IsNotEmpty(s);
            Assert.LessOrEqual(s[0].X - s[0].Width / 2f, from + Eps);
            Assert.GreaterOrEqual(s[s.Count - 1].X + s[s.Count - 1].Width / 2f, to - Eps);
            for (int i = 1; i < s.Count; i++)
            {
                Assert.LessOrEqual(s[i].X - s[i].Width / 2f, s[i - 1].X + s[i - 1].Width / 2f + Eps, $"x={s[i].X} 앞에 빈틈");
            }
        }

        // ── 회색 박스 렌더러 끄기 / 그림 범위 넓히기 ──

        [TestCase(-20f, 0f, true)]        // 범위 앞끝에 딱 맞은 조각
        [TestCase(90f, 94.25f, true)]     // 범위 뒤끝에 딱 맞은 조각
        [TestCase(10f, 10.5f, true)]
        [TestCase(-20.5f, -19.5f, false)] // 앞끝에 걸침
        [TestCase(94f, 94.5f, false)]     // 뒤끝에 걸침
        [TestCase(100f, 101f, false)]     // 범위 밖
        [TestCase(-21f, -20f, false)]     // 끝만 맞닿음(밖)
        public void 범위_안에_완전히_든_조각만_안이다(float x0, float x1, bool inside)
        {
            Assert.AreEqual(inside, MineDressingLayout.SpanInside(x0, x1, From, To));
        }

        [Test]
        public void 범위_끝의_float_오차는_안으로_친다()
        {
            Assert.IsTrue(MineDressingLayout.SpanInside(From - 0.0004f, 0f, From, To));
            Assert.IsTrue(MineDressingLayout.SpanInside(90f, To + 0.0004f, From, To));
        }

        [TestCase(94f, 94.5f, true)]      // 걸침
        [TestCase(-21f, -19f, true)]
        [TestCase(10f, 12f, true)]        // 안
        [TestCase(94.25f, 95f, false)]    // 끝만 맞닿음
        [TestCase(-25f, -20f, false)]
        [TestCase(100f, 112f, false)]     // 밖
        public void 관문은_조금이라도_겹치면_입힌다(float x0, float x1, bool overlaps)
        {
            Assert.AreEqual(overlaps, MineDressingLayout.SpanOverlaps(x0, x1, From, To));
        }

        [Test]
        public void 걸친_조각이_없으면_범위_그대로()
        {
            var spans = new List<(float, float)> { (-40f, -20f), (-20f, 0f), (0f, 50f), (50f, 94.25f), (94.25f, 120f) };
            var (from, to) = MineDressingLayout.CoverRange(spans, From, To);
            Assert.AreEqual(From, from);
            Assert.AreEqual(To, to);
        }

        [Test]
        public void 걸친_조각_끝까지_그림_범위를_넓힌다()
        {
            var spans = new List<(float, float)> { (-60f, -21f), (-21f, 0f), (0f, 94f), (94f, 96.5f), (96.5f, 200f) };
            var (from, to) = MineDressingLayout.CoverRange(spans, From, To);
            Assert.AreEqual(-21f, from);
            Assert.AreEqual(96.5f, to);
        }

        [Test]
        public void 실제_코스의_회색_바닥_조각은_평평한_앞부분에서_걸치지_않거나_덮인다()
        {
            //  굽기와 같은 조각 경계(−20, Breaks(0.5), 길이+20). 넓힌 범위 안의 조각은 모두 안이고, 밖의 것은 모두 안 겹친다
            //  — 그래서 "켜 둔 회색 조각"과 "그림"이 겹치는 자리가 없다.
            //  10-09 3막: 실제 굽기 범위는 [From, c.FlatEnd()]다(위 To=94.25는 옛 보기 구간 — 이제 입구 바로 뒤가
            //  수직 갱이라 94.25까지 평평하지 않다). FlatEnd가 Breaks의 꺾임 경계가 아닌 값으로 바뀌는 날,
            //  CoverRange가 그 꺾이는 자리까지 옷을 넓히면 여기서 잡힌다.
            var c = Course();
            float to = c.FlatEnd();
            var xs = new List<float> { -20f };
            xs.AddRange(c.Breaks(0.5f));
            xs.Add(c.Length + 20f);
            var spans = new List<(float, float)>();
            for (int i = 0; i + 1 < xs.Count; i++) { spans.Add((xs[i], xs[i + 1])); }

            var (from, resultTo) = MineDressingLayout.CoverRange(spans, From, to);
            Assert.LessOrEqual(from, From);
            Assert.GreaterOrEqual(resultTo, to);
            foreach (var (x0, x1) in spans)
            {
                bool inside = MineDressingLayout.SpanInside(x0, x1, from, resultTo);
                Assert.IsTrue(inside || !MineDressingLayout.SpanOverlaps(x0, x1, from, resultTo), $"[{x0}, {x1}]가 넓힌 범위에 걸친다");
            }
        }

        // ══ 펼치기 1차(10-10): 구간 배치 규칙 ══

        //  부품 z 범위(Task 3 보고 표, 원점 기준) — 닿는 부품은 뒷면 ≤ 0(판정면 앞), 장식은 앞면 > 0(판정면 뒤).
        static readonly Dictionary<MinePartKind, (float front, float back)> PartDepth = new Dictionary<MinePartKind, (float, float)>
        {
            { MinePartKind.EntranceFrame, (0.03f, 0.68f) }, { MinePartKind.EntranceSign, (-0.085f, 0.07f) },
            { MinePartKind.LanternString, (-0.1f, 0.1f) }, { MinePartKind.Lantern, (-0.3f, 0.3f) },
            { MinePartKind.ShaftTimber, (-0.5f, 0.5f) }, { MinePartKind.CageRail, (-0.11f, 0.11f) }, { MinePartKind.Cage, (-0.605f, 0.605f) },
            { MinePartKind.BenchFace, (-1.35f, 1.3f) }, { MinePartKind.ShortScaffold, (-0.35f, 0.35f) },
            { MinePartKind.RockMass, (-1.3f, 1.3f) }, { MinePartKind.BoreFrame, (-1.65f, 0.52f) }, { MinePartKind.BoltSign, (-0.11f, 0.06f) },
            { MinePartKind.PadGlow, (-0.67f, 0.65f) }, { MinePartKind.DividerWall, (-0.75f, 0.75f) }, { MinePartKind.MergeSlope, (-1.35f, 1.3f) },
            { MinePartKind.GateCap, (-0.80f, 0.73f) },
        };

        static readonly HashSet<MinePartKind> Touchable = new HashSet<MinePartKind>
        {
            MinePartKind.RockMass, MinePartKind.GateCap, MinePartKind.MergeSlope, MinePartKind.DividerWall,
            MinePartKind.BenchFace, MinePartKind.ShaftTimber, MinePartKind.PadGlow,
        };

        static readonly HashSet<MinePartKind> Decoration = new HashSet<MinePartKind>
        {
            MinePartKind.EntranceFrame, MinePartKind.EntranceSign, MinePartKind.LanternString, MinePartKind.Lantern,
            MinePartKind.CageRail, MinePartKind.Cage, MinePartKind.ShortScaffold, MinePartKind.BoltSign,
        };

        const float LookTimberSpacing = 0.45f;   // FlappyMineLook.shaftTimberSpacing 기본값

        static List<MinePiece> AllSpreadPieces(MineCourse c, MineZones z)
        {
            var all = new List<MinePiece>();
            all.AddRange(MineDressingLayout.Entrance(c, z));
            all.AddRange(MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing));
            all.AddRange(MineDressingLayout.StairBenches(c, z));
            foreach (MineFork f in c.Forks.Where(f => f.X1 <= z.DressEnd)) { all.AddRange(MineDressingLayout.ForkPieces(c, f)); }
            return all;
        }

        //  회전한 조각의 국소 좌표(원점 기준, 각도만큼 되돌림). Flipped면 180°를 더한다.
        static (double u, double v) Local(MinePiece p, double x, double y)
        {
            double a = (p.AngleDegrees + (p.Flipped ? 180.0 : 0.0)) * Math.PI / 180.0;
            double dx = x - p.X, dy = y - p.Y;
            return (dx * Math.Cos(a) + dy * Math.Sin(a), -dx * Math.Sin(a) + dy * Math.Cos(a));
        }

        //  국소 (u, v) → 세계.
        static (double x, double y) World(MinePiece p, double u, double v)
        {
            double a = (p.AngleDegrees + (p.Flipped ? 180.0 : 0.0)) * Math.PI / 180.0;
            return (p.X + u * Math.Cos(a) - v * Math.Sin(a), p.Y + u * Math.Sin(a) + v * Math.Cos(a));
        }

        //  바위 그림(RockMass·밝은 테 GateCap·MergeSlope·PadGlow)이 점 (x, y)를 덮나.
        static bool Covers(MinePiece p, double x, double y)
        {
            var (u, v) = Local(p, x, y);
            switch (p.Kind)
            {
                case MinePartKind.RockMass:   // 왼끝 아래, x·y 축척
                    return u >= 0 && u <= p.ScaleX && v >= 0 && v <= p.ScaleY;
                case MinePartKind.GateCap:    // 가운데, 윗면 원점, 두께 0.4 × y 축척 아래로
                    return Math.Abs(u) <= 0.975 * p.ScaleX && v >= -0.4 * p.ScaleY && v <= 0;
                case MinePartKind.BoreFrame:  // 굴 입구 중심, x −0.3~1.0, y ±1.755(상인방·문턱 바깥 끝)
                    return u >= -0.3 && u <= 1.0 && Math.Abs(v) <= 1.755;
                case MinePartKind.BoltSign:
                    return Math.Abs(u) <= 0.95 && Math.Abs(v) <= 0.547;
                case MinePartKind.DividerWall: // 왼끝 가운데, x 축척, y ±0.6 × y 축척
                    return u >= 0 && u <= p.ScaleX && Math.Abs(v) <= 0.6 * p.ScaleY;
                case MinePartKind.MergeSlope: // 왼끝 아래, 빗변 위쪽 삼각형(축척 음수면 뒤집힘)
                {
                    double s = u / p.ScaleX, t = v / p.ScaleY;
                    return s >= 0 && s <= 1 && t >= 0 && t <= 1 && t >= s;
                }
                case MinePartKind.PadGlow:
                    return Math.Abs(u) <= 0.6 && Math.Abs(v) <= 0.15;
                default:
                    return false;
            }
        }

        //  판정 굴 중심 = 회색 박스 조각(FlappyMineGeometry.Slices, 0.25 m, float 셈)의 현. 두 번째 날갯짓의 V가 든 조각은
        //  현이 포물선과 0.1 m 가까이 다르다 — 판정은 현이다.
        static double JudgedCenter(MineTube t, double x)
        {
            const float step = 0.25f;
            int n = (int)Math.Ceiling((t.X1 - t.X0) / step - 1e-4f);
            for (int i = 0; i < n; i++)
            {
                float a = t.X0 + i * step, b = i == n - 1 ? t.X1 : t.X0 + (i + 1) * step;
                if (x <= b || i == n - 1)
                {
                    float ca = t.Center(a), cb = t.Center(b);
                    return ca + (cb - ca) * (x - a) / (b - a);
                }
            }
            return t.Center((float)x);
        }

        [Test]
        public void 구간_x는_코스에서_찾는다_3막()
        {
            var z = MineDressingLayout.Zones(Course());
            Assert.AreEqual(19.8f, z.OutsideEnd, 0.01f, "노을 바깥 끝 = FlatEnd");
            Assert.AreEqual(24.8f, z.EntranceEnd, 0.01f, "갱 입구 문틀 끝 = 첫 수직 갱 낙하");
            Assert.AreEqual(151.25f, z.DressEnd, 0.01f, "옷 범위 끝 = 첫 레일 롤러코스터");
        }

        [TestCase(4)]
        [TestCase(3)]
        public void 입구_문틀은_EntranceEnd_쪽_통로_중심_기준이고_랜턴은_줄을_따라(int lanterns)
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var e = MineDressingLayout.Entrance(c, z, lanterns);
            var frame = e.Single(p => p.Kind == MinePartKind.EntranceFrame);
            Assert.AreEqual(z.EntranceEnd, frame.X + 2.5f, Eps, "문틀 오른끝 = EntranceEnd");
            Assert.AreEqual(z.OutsideEnd, frame.X - 2.5f, 0.01f, "3막은 문틀 폭이 구간을 꼭 채운다");
            Assert.AreEqual(c.CenterAt(frame.X) - MineCourse.BaseHalf, frame.Y, Eps, "문턱 = 그 자리 통로 중심 − 7.28");

            var sign = e.Single(p => p.Kind == MinePartKind.EntranceSign);
            Assert.AreEqual(frame.X, sign.X, Eps);
            Assert.Greater(sign.Y - 0.5f, frame.Y + 14.56f, "간판은 통로(문틀 안) 위에");

            var rope = e.Single(p => p.Kind == MinePartKind.LanternString);
            Assert.AreEqual(frame.X - 1.9f, rope.X, Eps, "줄 왼끝 = 왼 기둥 안쪽 면");
            Assert.AreEqual(3.8f, rope.ScaleX, Eps, "줄 = 기둥 사이");
            var ls = e.Where(p => p.Kind == MinePartKind.Lantern).OrderBy(p => p.X).ToList();
            Assert.AreEqual(lanterns, ls.Count);
            foreach (var l in ls)
            {
                Assert.That(l.X, Is.InRange(rope.X, rope.X + rope.ScaleX));
                float t = (l.X - rope.X) / rope.ScaleX;
                Assert.AreEqual(rope.Y - 0.35f * 4f * t * (1f - t), l.Y, Eps, $"x={l.X} 랜턴이 처진 줄에 안 걸렸다");
                Assert.Less(l.ScaleX, 1f, "줄에 다는 랜턴은 작게");
            }
        }

        [Test]
        public void 갱_벽_갱목은_판정선_바깥에만_있고_첫_줄은_판정선에_붙는다()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var w = MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing);
            var timbers = w.Where(p => p.Kind == MinePartKind.ShaftTimber).ToList();
            Assert.Greater(timbers.Count, 40);
            //  3막: 수직 갱 좁은 구간(22.8~36.8) + 뒤 전이 3 m, 앞은 문틀 끝(24.8)부터.
            Assert.AreEqual(24.8f, timbers.Min(p => Math.Min(World(p, 0, 0).x, World(p, p.ScaleX, 0).x)), 0.45f);
            Assert.AreEqual(39.8f, timbers.Max(p => Math.Max(World(p, 0, 0).x, World(p, p.ScaleX, 0).x)), 0.45f);

            int touchFloor = 0, touchCeiling = 0;
            foreach (var p in timbers)
            {
                var corners = new[] { World(p, 0, 0), World(p, p.ScaleX, 0), World(p, 0, 0.4), World(p, p.ScaleX, 0.4) };
                double midX = corners.Average(q => q.x), midY = corners.Average(q => q.y);
                bool floorSide = midY < c.CenterAt((float)midX);
                foreach (var (x, y) in corners)
                {
                    if (floorSide) { Assert.LessOrEqual(y, Floor(c, (float)x) + 0.01f, $"바닥 쪽 갱목 꼭짓점 ({x:F2},{y:F2})이 통로 안"); }
                    else { Assert.GreaterOrEqual(y, Ceiling(c, (float)x) - 0.01f, $"천장 쪽 갱목 꼭짓점 ({x:F2},{y:F2})이 통로 안"); }
                }
                //  첫 줄: 통로 쪽 변의 양끝이 판정선 위(± 0.05).
                var a = floorSide ? World(p, 0, 0.4) : World(p, 0, 0);
                var b = floorSide ? World(p, p.ScaleX, 0.4) : World(p, p.ScaleX, 0);
                Func<float, float> line = floorSide ? (Func<float, float>)(x => Floor(c, x)) : (x => Ceiling(c, x));
                if (Math.Abs(line((float)a.x) - a.y) < 0.05 && Math.Abs(line((float)b.x) - b.y) < 0.05)
                {
                    if (floorSide) { touchFloor++; } else { touchCeiling++; }
                }
            }
            Assert.Greater(touchFloor, 5, "판정선에 붙은 바닥 쪽 첫 줄이 없다");
            Assert.Greater(touchCeiling, 5, "판정선에 붙은 천장 쪽 첫 줄이 없다");
            //  층층이: 같은 자리에 간격 0.45로 여러 줄.
            Assert.Greater(timbers.Count, (touchFloor + touchCeiling) * 5);
        }

        [Test]
        public void 케이지_레일과_케이지는_수직_갱마다_하나씩_통로_뒤에()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var w = MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing, 0.3f);
            var cage = w.Single(p => p.Kind == MinePartKind.Cage);
            var rail = w.Single(p => p.Kind == MinePartKind.CageRail);
            //  낙하 24.8 → 32.8의 30% 자리, 통로 가운데.
            Assert.AreEqual(24.8f + 0.3f * 8f, cage.X, 0.01f);
            Assert.AreEqual(c.CenterAt(cage.X), cage.Y, Eps);
            Assert.AreEqual(cage.X, rail.X, Eps);
            Assert.Less(rail.Y, Floor(c, rail.X) - 3f, "레일 아래 끝은 바닥 벽 속");
            Assert.Greater(rail.Y + rail.ScaleY, Ceiling(c, rail.X) + 3f, "레일 위 끝은 천장 벽 속");
            //  갱목(z ±0.5 + 원점) 뒤 — 통로 밖 레일은 갱목이 가린다.
            float timberBack = w.Where(p => p.Kind == MinePartKind.ShaftTimber).Max(p => p.Z) + 0.5f;
            Assert.Greater(rail.Z - 0.11f, timberBack);
            Assert.Greater(cage.Z - 0.605f, timberBack);
        }

        [Test]
        public void 계단_칸마다_바위_단_윗면이_바닥선_꺾임에_맞는다()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var s = MineDressingLayout.StairBenches(c, z);
            var floorBenches = s.Where(p => p.Kind == MinePartKind.BenchFace && !p.Flipped).OrderBy(p => p.X).ToList();
            var ceilBenches = s.Where(p => p.Kind == MinePartKind.BenchFace && p.Flipped).ToList();
            //  3막 계단 내리막: 관문 6개, 칸 = 갱 바닥 관문(41.225) ~ 마지막 계단 관문(73.625), 5.4 m씩.
            Assert.AreEqual(6, floorBenches.Count);
            Assert.AreEqual(6, ceilBenches.Count);
            Assert.AreEqual(41.225f, floorBenches[0].X, 0.01f);
            Assert.AreEqual(68.225f, floorBenches[5].X, 0.01f);
            foreach (var p in floorBenches.Concat(ceilBenches))
            {
                Func<float, float> line = p.Flipped ? (Func<float, float>)(x => Ceiling(c, x)) : (x => Floor(c, x));
                var a = World(p, 0, 0);
                var b = World(p, p.ScaleX, 0);
                Assert.AreEqual(line((float)a.x), (float)a.y, 0.05f, $"{p} 한끝");
                Assert.AreEqual(line((float)b.x), (float)b.y, 0.05f, $"{p} 다른 끝");
                Assert.AreEqual(5.4f, (float)Math.Abs(b.x - a.x), 0.01f, "칸 = 관문 간격 5.4");
                //  몸통(아래로 4.5)은 통로 밖 — 판정선에서 바깥으로.
                var inner = World(p, p.ScaleX / 2, -2.25);
                if (p.Flipped) { Assert.Greater(inner.y, Ceiling(c, (float)inner.x)); } else { Assert.Less(inner.y, Floor(c, (float)inner.x)); }
            }
            var scaffolds = s.Where(p => p.Kind == MinePartKind.ShortScaffold).ToList();
            Assert.AreEqual(6, scaffolds.Count);
            foreach (var p in scaffolds)
            {
                //  내리막 쪽 다리가 바닥에 닿고(뜨지 않고), 오르막 쪽 다리는 단에 묻힌다.
                float lowLeg = Math.Min(Floor(c, p.X - 1.05f), Floor(c, p.X + 1.05f));
                Assert.AreEqual(lowLeg, p.Y, Eps, $"x={p.X} 비계가 바닥에 안 섰다");
            }
        }

        [Test]
        public void 칸막이는_판정_사각형_그대로()
        {
            var c = Course();
            foreach (MineFork f in c.Forks)
            {
                var wall = c.Walls.Single(w => Math.Abs(w.X0 - f.X0) < 1e-3f);
                var d = MineDressingLayout.ForkPieces(c, f).Single(p => p.Kind == MinePartKind.DividerWall);
                //  원점 = 왼끝 가운데, x 축척 = 폭(1 m 단위), 높이 1.2 단위.
                Assert.AreEqual(wall.X0, d.X, 0.05f);
                Assert.AreEqual(wall.X1, d.X + d.ScaleX, 0.05f);
                Assert.AreEqual(wall.Y0, d.Y - 0.6f * d.ScaleY, 0.05f);
                Assert.AreEqual(wall.Y1, d.Y + 0.6f * d.ScaleY, 0.05f);
            }
        }

        [Test]
        public void 고수_칸_바위는_굴_방_경계와_맞는다()
        {
            var c = Course();
            foreach (MineFork f in c.Forks)
            {
                var pieces = MineDressingLayout.ForkPieces(c, f);
                var rock = pieces.Where(p => p.Kind == MinePartKind.RockMass || p.Kind == MinePartKind.GateCap
                                             || p.Kind == MinePartKind.MergeSlope || p.Kind == MinePartKind.PadGlow).ToList();
                //  판정면 앞(앞면 z < 0)에 그려진 것 전부 — 열린 칸·틈 위에 하나라도 있으면 "닿을 것처럼 보이는데 지나간다".
                var inFront = pieces.Where(p => p.Z + PartDepth[p.Kind].front < 0f).ToList();
                var tubes = c.Tubes.Where(t => t.X0 >= f.X0 - 1e-3f && t.X1 <= f.X1 + 1e-3f).OrderBy(t => t.X0).ToList();
                Assert.AreEqual(2, tubes.Count, "갈림길 = 굴 + 패드 방");
                MineRect lane = f.TunnelLane;
                int solid = 0, open = 0;
                //  격자를 경계에 딱 맞지 않게 조금 비켜 둔다.
                //  칸막이 시작(굴 앞 열린 칸)부터 — 굴 앞 3 m는 판정이 열려 있으니 그림도 없어야 한다.
                for (double x = f.X0 + 0.013; x < f.X1 - 0.01; x += 0.1)
                {
                    var t = tubes.FirstOrDefault(q => x >= q.X0 && x <= q.X1);
                    double lo = Math.Max(lane.Y0, Floor(c, (float)x)), hi = Math.Min(lane.Y1, Ceiling(c, (float)x));
                    for (double y = lo + 0.017; y < hi; y += 0.1)
                    {
                        //  > 0: 판정 바위, < 0: 틈(굴 앞은 칸 전체가 틈). 굴 끝 세로 면에서 0.05 안은 경계라 보지 않는다.
                        if (tubes.Any(q => Math.Abs(x - q.X0) < 0.05 || Math.Abs(x - q.X1) < 0.05)) { continue; }
                        double outside = t == null ? -Math.Min(y - lo, hi - y) : Math.Abs(y - JudgedCenter(t, x)) - t.Gap / 2.0;
                        //  그림 경계 = 판정 경계 ± 0.05.
                        if (outside > 0.05)
                        {
                            solid++;
                            Assert.IsTrue(rock.Any(p => Covers(p, x, y)), $"갈림길 {f.X0}: 판정 바위 ({x:F2},{y:F2})를 그림이 안 덮는다");
                        }
                        else if (outside < -0.05)
                        {
                            open++;
                            var hit = inFront.FirstOrDefault(p => Covers(p, x, y));
                            Assert.IsFalse(hit.Length > 0, $"갈림길 {f.X0}: 열린 칸·틈 ({x:F2},{y:F2})에 판정면 앞 그림 {hit}");
                        }
                    }
                }
                Assert.Greater(solid, 500);
                Assert.Greater(open, 500);
                //  밝은 테가 바위 앞면보다 앞(바위가 테를 가리지 않게).
                float rockFront = pieces.Where(p => p.Kind == MinePartKind.RockMass).Min(p => p.Z) + PartDepth[MinePartKind.RockMass].front;
                foreach (var cap in pieces.Where(p => p.Kind == MinePartKind.GateCap))
                {
                    Assert.Less(cap.Z + PartDepth[MinePartKind.GateCap].front, rockFront, "밝은 테가 바위 뒤에 묻혔다");
                }
            }
        }

        [Test]
        public void 밝은_테가_굴_방_틈의_닿는_바위_가장자리를_빈틈없이_덮는다()
        {
            var c = Course();
            foreach (MineFork f in c.Forks)
            {
                var caps = MineDressingLayout.ForkPieces(c, f).Where(p => p.Kind == MinePartKind.GateCap).ToList();
                var tubes = c.Tubes.Where(t => t.X0 >= f.X0 - 1e-3f && t.X1 <= f.X1 + 1e-3f).OrderBy(t => t.X0).ToList();
                MineRect lane = f.TunnelLane;
                int samples = 0;
                foreach (MineTube t in tubes)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (double x = t.X0 + 0.02; x < t.X1 - 0.02; x += 0.05)
                        {
                            //  판정 가장자리(회색 박스 현). 칸 끝(칸막이·통로 바닥·천장) 너머면 그 쪽 바위가 없다.
                            double e = JudgedCenter(t, x) + side * t.Gap / 2.0;
                            double outer = side < 0 ? Math.Max(lane.Y0, Floor(c, (float)x)) : Math.Min(lane.Y1, Ceiling(c, (float)x));
                            if (side < 0 ? e <= outer + 0.02 : e >= outer - 0.02) { continue; }
                            samples++;
                            //  가장자리에서 바위 쪽으로 0.03 — 테(가장 얇은 곳 0.04) 안이어야 한다.
                            double y = e + side * 0.03;
                            Assert.IsTrue(caps.Any(p => Covers(p, x, y)), $"갈림길 {f.X0}: 닿는 바위 가장자리 ({x:F2},{e:F2})에 밝은 테가 없다");
                        }
                    }
                }
                Assert.Greater(samples, 300);
            }
        }

        [Test]
        public void 굴_문틀_번개_판_발판_합류_경사의_자리()
        {
            var c = Course();
            foreach (MineFork f in c.Forks)
            {
                var pieces = MineDressingLayout.ForkPieces(c, f);
                var tubes = c.Tubes.Where(t => t.X0 >= f.X0 - 1e-3f && t.X1 <= f.X1 + 1e-3f).OrderBy(t => t.X0).ToList();
                MineTube bore = tubes[0], room = tubes[1];
                MineRect lane = f.TunnelLane;

                //  굴 문틀(BoreFrame)은 지금 메시로는 놓지 않는다(Fix round 1): 상인방·문턱이 곧은 1.3 m라 어디 두든 굴 앞 열린 칸이나
                //  휘는 틈 위 판정면 앞에 걸친다. 메시를 고친 뒤 다시 넣는다.
                Assert.IsFalse(pieces.Any(p => p.Kind == MinePartKind.BoreFrame), "굴 문틀은 메시를 고치기 전까지 놓지 않는다");

                var bolt = pieces.Single(p => p.Kind == MinePartKind.BoltSign);
                //  굴 입구 위(입구 앞 열린 칸, 판정면 뒤) — 굴 틈 위 끝보다 위, 칸 위 끝 아래, 굴 시작 앞.
                Assert.Greater(bolt.Y - 0.547f, bore.Center(bore.X0) + bore.Gap / 2f);
                Assert.Less(bolt.Y + 0.547f, Math.Min(lane.Y1, Ceiling(c, bolt.X)));
                Assert.LessOrEqual(bolt.X + 0.95f, bore.X0);
                Assert.GreaterOrEqual(bolt.X - 0.95f, f.X0);

                var pad = c.Pads.Single(q => q.Rect.X0 > f.X0 && q.Rect.X1 < f.X1);
                var glow = pieces.Single(p => p.Kind == MinePartKind.PadGlow);
                Assert.AreEqual((pad.Rect.X0 + pad.Rect.X1) / 2f, glow.X, Eps);
                //  윗면 = 방 바닥(굴 위 칸이면 칸막이 윗면이 바닥).
                float roomFloor = Math.Max(room.Center(glow.X) - room.Gap / 2f, Math.Max(lane.Y0, Floor(c, glow.X)));
                Assert.AreEqual(roomFloor, glow.Y + 0.15f, 0.05f, "발판 윗면이 방 바닥이 아니다");

                //  합류 경사(방 위 MergeSlope — 굴 가장자리 삼각형도 MergeSlope다)는 방 끝 칸막이 쪽 판정 바위 안에만(열린 통로에 걸치지 않는다).
                foreach (var m in pieces.Where(p => p.Kind == MinePartKind.MergeSlope && p.X + Math.Min(0f, p.ScaleX) >= room.X0 - Eps))
                {
                    foreach (var (u, v) in new[] { (0.0, 0.0), (1.0, 1.0), (0.0, 1.0) })
                    {
                        var (x, y) = World(m, u * m.ScaleX, v * m.ScaleY);
                        Assert.That(x, Is.InRange(room.X0 - Eps, room.X1 + Eps));
                        double outside = Math.Abs(y - room.Center((float)x)) - room.Gap / 2.0;
                        Assert.GreaterOrEqual(outside, -0.05, $"합류 경사 꼭짓점 ({x:F2},{y:F2})이 방 틈 안");
                        Assert.That(y, Is.InRange(lane.Y0 - 0.05, lane.Y1 + 0.05));
                    }
                }
            }
            //  3막 첫 갈림길(⬇굴)은 방 위가 칸막이까지 1.7 m 바위라 합류 경사가 있다.
            var room0 = c.Tubes.Single(t => t.X0 >= c.Forks[0].X0 && t.X1 <= c.Forks[0].X1 + 1e-3f && t.Gap > 4f);
            var merge = MineDressingLayout.ForkPieces(c, c.Forks[0]).Where(p => p.Kind == MinePartKind.MergeSlope && p.X + Math.Min(0f, p.ScaleX) >= room0.X0 - Eps).ToList();
            Assert.AreEqual(1, merge.Count);
            Assert.AreEqual(room0.X1, merge[0].X + merge[0].ScaleX, Eps, "합류 경사는 방 끝까지");
        }

        [Test]
        public void 닿는_부품은_판정면_앞_장식은_판정면_뒤()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var all = AllSpreadPieces(c, z);
            Assert.IsTrue(all.Any(p => Touchable.Contains(p.Kind)) && all.Any(p => Decoration.Contains(p.Kind)));
            foreach (var p in all)
            {
                var (front, back) = PartDepth[p.Kind];
                if (Touchable.Contains(p.Kind)) { Assert.LessOrEqual(p.Z + back, 0.001f, $"{p} 닿는 부품 뒷면이 판정면 뒤"); }
                else if (Decoration.Contains(p.Kind)) { Assert.Greater(p.Z + front, 0f, $"{p} 장식이 판정면 앞"); }
            }
            //  범위(옷 끝 = 롤러코스터 앞) 밖에는 놓지 않는다.
            Assert.IsTrue(all.All(p => p.X >= z.OutsideEnd - 0.5f && p.X <= z.DressEnd), "범위 밖에 부품");
        }

        [Test]
        public void 배경_실루엣은_통로_중심을_따라_내려간다()
        {
            var c = Course();
            var bg = MineDressingLayout.Background(c, From, 100f, 7, Density);
            //  가까운 층(0) 바닥 쪽 기준선 = 통로 중심 − 8.2.
            var near = bg.Where(p => (p.Kind == MinePartKind.CaveSilhouette || p.Kind == MinePartKind.CanyonSilhouette)
                                     && p.Layer == 0 && !p.Flipped).ToList();
            float NearBase(MinePiece p) => p.Kind == MinePartKind.CaveSilhouette ? -8.2f : -11.6f;
            for (float x = 20f; x <= 100f; x += 0.5f)
            {
                var strip = near.First(p => x >= p.X - p.Width / 2f - Eps && x <= p.X + p.Width / 2f + Eps);
                //  띠 기준선(국소 y = 0)의 x 자리 높이.
                double a = strip.AngleDegrees * Math.PI / 180.0;
                double baseY = strip.Y + (x - strip.X) * Math.Tan(a);
                Assert.AreEqual(c.CenterAt(x), baseY - NearBase(strip), 0.05f, $"x={x} 실루엣 기준선이 통로 중심을 안 따른다");
            }
            //  수직 갱 아래(x 36)에서 기준선 = −22 근처.
            var under = near.First(p => 36f >= p.X - p.Width / 2f && 36f <= p.X + p.Width / 2f);
            double ua = under.AngleDegrees * Math.PI / 180.0;
            Assert.AreEqual(-22f, under.Y + (36f - under.X) * Math.Tan(ua) - NearBase(under), 0.05f);
            //  먼 비계·가운데 틀도 수직 갱 아래에서 −22 근처를 기준으로.
            foreach (var p in bg.Where(p => p.Kind == MinePartKind.BgFrame && p.X > 33f && p.X < 41f))
            {
                Assert.AreEqual(-22f + 6.5f, p.Y, 0.01f);
            }
            foreach (var p in bg.Where(p => p.Kind == MinePartKind.BgTrestleBay && p.X > 33f && p.X < 41f))
            {
                Assert.AreEqual(-22f - 1f, p.Y, 2.6f);
            }
        }

        [Test]
        public void 같은_코스면_같은_자리()
        {
            var c1 = Course();
            var c2 = Course();
            var a = AllSpreadPieces(c1, MineDressingLayout.Zones(c1));
            var b = AllSpreadPieces(c2, MineDressingLayout.Zones(c2));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i], b[i], $"{i}번째"); }
            var ga = MineDressingLayout.Background(c1, From, 151.25f, 7, Density);
            var gb = MineDressingLayout.Background(c2, From, 151.25f, 7, Density);
            Assert.AreEqual(ga.Count, gb.Count);
            for (int i = 0; i < ga.Count; i++) { Assert.AreEqual(ga[i], gb[i], $"배경 {i}번째"); }
        }

        sealed class FloatComparer : System.Collections.IComparer
        {
            readonly float eps;
            public FloatComparer(float eps) { this.eps = eps; }
            public int Compare(object a, object b) => Math.Abs((float)a - (float)b) <= eps ? 0 : ((float)a).CompareTo((float)b);
        }
    }
}
