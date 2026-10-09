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
            var bays = MineDressingLayout.TrestleBays(Course(), From, To);
            AssertTiles(bays, From, To, MineDressingLayout.TrestleBayWidth);
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
            var pieces = MineDressingLayout.CeilingPieces(Course(), From, To);
            Assert.IsTrue(pieces.Any(p => p.Kind == MinePartKind.Beam));
            Assert.IsTrue(pieces.Any(p => p.Kind == MinePartKind.CeilingRock));
            foreach (var p in pieces)
            {
                var expect = p.X < MineDressingLayout.OutsideEnd ? MinePartKind.Beam : MinePartKind.CeilingRock;
                Assert.AreEqual(expect, p.Kind, $"x={p.X}");
                //  축척은 부품 단위 길이 기준(들보 1 m, 바위 2 m).
                float unit = p.Kind == MinePartKind.Beam ? MineDressingLayout.BeamUnit : MineDressingLayout.CeilingRockUnit;
                Assert.AreEqual(p.Length / unit, p.ScaleX, Eps);
            }
        }

        // ── 굴 입구 ──

        [Test]
        public void 굴_입구는_x29_통로_가운데()
        {
            var c = Course();
            var (x, y) = MineDressingLayout.CaveMouthAt(c);
            Assert.AreEqual(29f, x, Eps);
            Assert.AreEqual(c.CenterAt(29f), y, Eps);
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
            var bg = MineDressingLayout.Background(Course(), From, To, 7, Density);
            var sil = bg.Where(p => p.Kind == MinePartKind.CanyonSilhouette || p.Kind == MinePartKind.CaveSilhouette).ToList();
            foreach (var p in sil)
            {
                bool outside = p.X < MineDressingLayout.OutsideEnd;
                Assert.AreEqual(outside ? MinePartKind.CanyonSilhouette : MinePartKind.CaveSilhouette, p.Kind, $"x={p.X}");
                Assert.That(p.Z, Is.InRange(MineDressingLayout.SilhouetteNearZ, MineDressingLayout.SilhouetteFarZ));
                //  먼 비계보다 뒤 — 앞이면 비계 가운데를 실루엣 띠가 가린다.
                Assert.Greater(p.Z, MineDressingLayout.FarZ, $"x={p.X} 실루엣이 먼 비계 앞에 있다");
                Assert.AreEqual(MineDressingLayout.SilhouetteScaleX, p.ScaleX, Eps);
            }
            //  층마다(먼·가까운, 바닥 쪽) 빈틈없이.
            foreach (int layer in new[] { 0, 1 })
            {
                var floorSide = sil.Where(p => p.Layer == layer && !p.Flipped).ToList();
                AssertCovers(floorSide, From - MineDressingLayout.BackgroundMargin, To + MineDressingLayout.BackgroundMargin,
                             MineDressingLayout.SilhouetteWidth);
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
            var bg = MineDressingLayout.Background(Course(), From, To, 7, Density);
            var frames = bg.Where(p => p.Kind == MinePartKind.BgFrame).OrderBy(p => p.X).ToList();
            var ladders = bg.Where(p => p.Kind == MinePartKind.Ladder).ToList();
            var walks = bg.Where(p => p.Kind == MinePartKind.Walkway).ToList();
            var lanterns = bg.Where(p => p.Kind == MinePartKind.Lantern).OrderBy(p => p.X).ToList();

            Assert.Greater(frames.Count, 5);
            for (int i = 1; i < frames.Count; i++) { Assert.AreEqual(Density.FrameSpacing, frames[i].X - frames[i - 1].X, Eps); }
            Assert.IsTrue(frames.Concat(ladders).Concat(walks).Concat(lanterns).All(p => p.X >= MineDressingLayout.OutsideEnd));
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

        //  가운데 정렬 조각들이 [from, to]를 빈틈·겹침 없이 덮고, 밖으로 나가지 않는다. 칸 폭은 단위 폭 근처(±10%).
        static void AssertTiles(List<MinePiece> pieces, float from, float to, float unit)
        {
            var s = pieces.OrderBy(p => p.X).ToList();
            Assert.AreEqual(from, s[0].X - s[0].Width / 2f, Eps, "첫 조각이 from에서 시작하지 않는다");
            Assert.AreEqual(to, s[s.Count - 1].X + s[s.Count - 1].Width / 2f, Eps, "마지막 조각이 to에서 끝나지 않는다");
            for (int i = 0; i < s.Count; i++)
            {
                Assert.That(s[i].Width, Is.InRange(unit * 0.9f, unit * 1.1f));
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

        sealed class FloatComparer : System.Collections.IComparer
        {
            readonly float eps;
            public FloatComparer(float eps) { this.eps = eps; }
            public int Compare(object a, object b) => Math.Abs((float)a - (float)b) <= eps ? 0 : ((float)a).CompareTo((float)b);
        }
    }
}
