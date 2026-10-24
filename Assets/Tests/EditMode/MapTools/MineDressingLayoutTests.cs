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
        [TestCase(0f, 0.9f, false)]
        public void 기둥_칸_높이_합은_기둥에서_쇠테_두께를_뺀_높이고_마지막_칸만_1_미만(float bottom, float top, bool capAtTop)
        {
            var s = MineDressingLayout.GateStack(bottom, top, capAtTop);
            float cap = MineDressingLayout.CapThickness;
            Assert.AreEqual(top - bottom - cap, s.Cells.Sum(cell => cell.Height), Eps);
            //  묻힌 쪽 끝은 파이프 끝, 틈 쪽 끝은 쇠테 밑면(틈 쪽 끝 − 쇠테 두께).
            Assert.AreEqual(capAtTop ? bottom : bottom + cap, s.Cells[0].Y0, Eps);
            Assert.AreEqual(capAtTop ? top - cap : top, s.Cells[s.Cells.Count - 1].Y0 + s.Cells[s.Cells.Count - 1].Height, Eps);
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
            Assert.AreEqual(6, MineDressingLayout.GateStack(-7.28f, -1.8f, true).Cells.Count);   // 5.48 − 0.4 = 5.08 m
            Assert.AreEqual(3, MineDressingLayout.GateStack(0f, 3.4f, true).Cells.Count);        // 판자 딱 3 m — 빈 마지막 칸 없음
            Assert.AreEqual(0, MineDressingLayout.GateStack(1f, 1f, true).Cells.Count);
            Assert.IsFalse(MineDressingLayout.GateStack(1f, 1f, true).HasCap, "높이 0이면 쇠테도 없다");
            //  쇠테보다 짧은 기둥은 판자 없이 쇠테만.
            var shortStack = MineDressingLayout.GateStack(0f, 0.3f, false);
            Assert.AreEqual(0, shortStack.Cells.Count);
            Assert.IsTrue(shortStack.HasCap);
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

        //  10-15 번쩍임: 판자 끝 면이 쇠테 윗면과 한 평면이면 z-fighting(크림 ↔ 주황 띠가 깜빡, 림이 거의 흰색으로). 실제 코스의 관문 파이프마다 —
        //  틈 쪽 경계(그림 = 판정)는 쇠테 윗면 그대로, 판자는 쇠테 밑면에서 멈춰 쇠테가 덮는다(같은 방향 겹면 없음).
        [Test]
        public void 판자는_쇠테_밑면에서_멈추고_쇠테_윗면이_틈_끝이다()
        {
            var c = Course();
            Assert.Greater(c.Gates.Count, 0);
            float cap = MineDressingLayout.CapThickness;
            int checkedPipes = 0;
            foreach (MineGate g in c.Gates)
            {
                float a = g.GapCenter - g.Gap / 2f, b = g.GapCenter + g.Gap / 2f;
                float floor = Floor(c, g.X) - MineDressingLayout.GatePipeBury, ceiling = Ceiling(c, g.X) + MineDressingLayout.GatePipeBury;
                foreach (var (bottom, top, capAtTop, gapEdge) in new[] { (floor, a, true, a), (b, ceiling, false, b) })
                {
                    if (top - bottom <= cap) { continue; }
                    var s = MineDressingLayout.GateStack(bottom, top, capAtTop);
                    Assert.IsTrue(s.HasCap);
                    //  쇠테 윗면(뒤집힌 위 관문은 아랫면) = 틈 끝 = 판정 경계.
                    Assert.AreEqual(gapEdge, s.CapY, 0.01f, $"관문 x {g.X} 쇠테가 틈 끝에 없다");
                    float plankEnd = capAtTop ? s.Cells[s.Cells.Count - 1].Y0 + s.Cells[s.Cells.Count - 1].Height : s.Cells[0].Y0;
                    float capFace = s.CapY, capBack = capAtTop ? s.CapY - cap : s.CapY + cap;
                    //  판자 끝은 쇠테 면에서 떨어져(같은 평면 아님) 쇠테 두께 안 — 쇠테 밑면과 같다.
                    Assert.Greater(Math.Abs(capFace - plankEnd), 0.03f, $"관문 x {g.X} 판자 끝이 쇠테 면과 한 평면이다");
                    Assert.AreEqual(capBack, plankEnd, 1e-3f, $"관문 x {g.X} 판자가 쇠테 밑면에서 멈추지 않는다(틈이 생기거나 겹면)");
                    //  판자는 쇠테 밖(틈 쪽)으로 나가지 않는다.
                    Assert.IsTrue(s.Cells.All(cell => cell.Y0 >= bottom - Eps && cell.Y0 + cell.Height <= top + Eps), $"관문 x {g.X} 판자가 파이프 밖");
                    checkedPipes++;
                }
            }
            Assert.Greater(checkedPipes, c.Gates.Count, "아래·위 파이프 둘 다 재야 한다");
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
            //  10-09 3막: 첫 수직 갱 낙하(24.8~34.0) — 9.2 m 만에 22 m 떨어지는, 가장 가파른 경우(10-12 사람 판 레이아웃 변경: 8 → 9.2 m, 현 기울기 약 67°).
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
            { MinePartKind.GateCap, (-0.80f, 0.73f) }, { MinePartKind.EdgeRim, (-1.3f, 1.3f) },
            //  10-12 펼치기 2차 부품(Task 2가 이 크기로 만든다 — MineDressingLayout 상수 옆 주석). 벽 사다리(Ladder 메시)는 원점이 뒷면.
            { MinePartKind.ExitSign, (-0.085f, 0.07f) }, { MinePartKind.FinishFlag, (-0.1f, 0.1f) },
            { MinePartKind.CliffEdge, (-MineDressingLayout.CliffEdgeHalfDepth, MineDressingLayout.CliffEdgeHalfDepth) },
            { MinePartKind.WallLadder, (-0.1f, 0f) },
            //  10-12 fix1: 굴 바닥 레일(RailSpan 메시), 받침 기둥(ShaftTimber 메시), 결승선 체크무늬 띠(평면), 바위 판(RockMass 메시).
            { MinePartKind.CoasterRail, (-0.55f, 0.55f) }, { MinePartKind.RailPost, (-0.5f, 0.5f) },
            { MinePartKind.FinishBanner, (0f, -MineDressingLayout.FinishBannerZ) }, { MinePartKind.RockVeneer, (-1.3f, 1.3f) },
        };

        static readonly HashSet<MinePartKind> Touchable = new HashSet<MinePartKind>
        {
            MinePartKind.RockMass, MinePartKind.EdgeRim, MinePartKind.MergeSlope, MinePartKind.DividerWall,
            MinePartKind.BenchFace, MinePartKind.ShaftTimber, MinePartKind.PadGlow, MinePartKind.BoltSign,
        };

        static readonly HashSet<MinePartKind> Decoration = new HashSet<MinePartKind>
        {
            MinePartKind.EntranceFrame, MinePartKind.EntranceSign, MinePartKind.LanternString, MinePartKind.Lantern,
            MinePartKind.CageRail, MinePartKind.Cage, MinePartKind.ShortScaffold,
            MinePartKind.ExitSign, MinePartKind.FinishFlag, MinePartKind.CliffEdge,
        };

        const float LookTimberSpacing = 0.45f;   // FlappyMineLook.shaftTimberSpacing 기본값

        static List<MinePiece> AllSpreadPieces(MineCourse c, MineZones z)
        {
            var all = new List<MinePiece>();
            all.AddRange(MineDressingLayout.Entrance(c, z));
            all.AddRange(MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing));
            all.AddRange(MineDressingLayout.StairBenches(c, z));
            foreach (MineFork f in c.Forks.Where(f => f.X1 <= z.DressEnd)) { all.AddRange(MineDressingLayout.ForkPieces(c, f)); }
            all.AddRange(MineDressingLayout.Exit(c, z));
            all.AddRange(MineDressingLayout.Finish(c, z));
            all.AddRange(MineDressingLayout.ChimneyWalls(c, 7, z));
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
                case MinePartKind.RockVeneer: // RockMass 메시(두께 없음)
                case MinePartKind.EdgeRim:    // RockMass 메시
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
            //  10-12 펼치기 2차: 옷 범위 끝 = 코스 끝 + 결승선 뒤 여유(옛 값은 첫 레일 롤러코스터 151.25).
            //  10-12 사람 판 레이아웃 변경: 코스 끝 386.15 → 422.35, 마지막 갈림길 끝 362.5 → 395.7.
            //  10-13 동적 지형: 코스 끝 422.35 → 445.95, 마지막 갈림길 끝 395.7 → 419.3(레일 굴 12 m).
            //  시안 v10에서 갈라짐(10-13 검사): 첫 급반전 앞 +2.6 — 448.55, 421.9.
            //  10-15 무너진 갱도 길이 10 → 11: +1 — 449.55, 422.9.
            //  10-24 낙석 +3줄·진자 +2자리: +33.5 — 483.05, 456.4.
            Assert.AreEqual(483.05f + 20f, z.DressEnd, 0.01f, "옷 범위 끝 = 코스 끝 + 결승선 뒤 여유");
            Assert.AreEqual(Course().Length + MineDressingLayout.EndMargin, z.DressEnd, 1e-3f);
            //  출구 문틀 = 마지막 고수 갈림길 끝 ~ +5 m.
            Assert.AreEqual(456.4f, z.ExitStart, 0.01f, "출구 문틀 시작 = 마지막 고수 갈림길 끝");
            Assert.AreEqual(Course().Forks.Last().X1, z.ExitStart, 1e-3f, "출구 문틀 시작 = 마지막 고수 갈림길 끝");
            Assert.AreEqual(461.4f, z.ExitEnd, 0.01f, "출구 문틀 끝 = 시작 + 5");
        }

        [Test]
        public void 고수_갈림길이_없으면_출구_존은_코스_끝()
        {
            var c = new MineCourse { Gates = new List<MineGate>(), Tubes = new List<MineTube>(), Walls = new List<MineRect>(), Pads = new List<MinePad>(),
                                     Forks = new List<MineFork>(), Sections = new List<MineSection>(), Length = 50f };
            foreach (var (name, value) in new (string, object)[]
            {
                ("Points", new List<(double, double)> { (0, 0), (50, 0) }), ("Narrows", new List<(double, double, double)>()), ("Lows", new List<(double, double)>()),
            })
            {
                typeof(MineCourse).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(c, value);
            }
            var z = MineDressingLayout.Zones(c);
            Assert.AreEqual(50f, z.ExitStart, Eps);
            Assert.AreEqual(50f, z.ExitEnd, Eps);
            Assert.IsEmpty(MineDressingLayout.Exit(c, z), "출구가 없으면 출구 문틀도 없다");
            Assert.IsFalse(MineDressingLayout.Finish(c, z).Any(p => p.Kind == MinePartKind.CliffEdge), "출구가 없으면 절벽 끝도 없다");
        }

        [Test]
        public void 입구_문틀은_EntranceEnd_쪽_통로_중심_기준이고_간판은_상인방_앞()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var e = MineDressingLayout.Entrance(c, z);
            var frame = e.Single(p => p.Kind == MinePartKind.EntranceFrame);
            Assert.AreEqual(z.EntranceEnd, frame.X + 2.5f, Eps, "문틀 오른끝 = EntranceEnd");
            Assert.AreEqual(z.OutsideEnd, frame.X - 2.5f, 0.01f, "3막은 문틀 폭이 구간을 꼭 채운다");
            Assert.AreEqual(c.CenterAt(frame.X) - MineCourse.BaseHalf, frame.Y, Eps, "문턱 = 그 자리 통로 중심 − 7.28");

            var sign = e.Single(p => p.Kind == MinePartKind.EntranceSign);
            Assert.AreEqual(frame.X, sign.X, Eps);
            Assert.Greater(sign.Y - 0.5f, frame.Y + 14.56f, "간판은 통로(문틀 안) 위에");
        }

        [Test]
        public void 갱목_구간은_갱목이_덮는_x와_같다()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var spans = MineDressingLayout.ShaftSpans(c, z);
            //  3막: 수직 갱 좁은 구간 [a − 2, b + 4] ± 전이 3 m, 앞은 문틀 끝(24.8)에서 자른다. 깊은 갱도 ± 3 m.
            //  10-12: 굴뚝(오르막 좁은 구간)은 갱목 갱도가 아니다 — 뺀다.
            //  10-12 사람 판 레이아웃 변경: 첫 갱 8 → 9.2 m, 깊은 갱 266.25 → 299.45 — 갱 시작 x는 코스 구간에서 찾는다.
            var (drop1, drop2) = DropXs(c);
            Assert.AreEqual(2, spans.Count, string.Join(", ", spans));
            Assert.AreEqual(24.8f, spans[0].from, 1e-3f);
            Assert.AreEqual(drop1 + 9.2f + 4f + 3f, spans[0].to, 1e-3f);    // 41.0
            Assert.AreEqual(drop2 - 2f - 3f, spans[1].from, 1e-3f);          // 298.05
            Assert.AreEqual(drop2 + 10f + 4f + 3f, spans[1].to, 1e-3f);     // 320.05
            //  갱목 첫 줄(천장 쪽은 원점이 판정선 위 왼끝)이 이 구간을 양끝까지 채운다 — 굽기는 이 구간에서 바닥·천장 조각을 빼므로 틈이 생기면 안 된다.
            var timbers = MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing).Where(p => p.Kind == MinePartKind.ShaftTimber && p.X < spans[0].to + 3f).ToList();
            var ceilingRow = timbers.Where(p => Math.Abs(p.Y - Ceiling(c, p.X)) < 1e-3f).ToList();
            Assert.AreEqual(spans[0].from, ceilingRow.Min(p => p.X), 1e-3f);
            Assert.AreEqual(spans[0].to, ceilingRow.Max(p => p.X + p.Width), 1e-3f);
            //  바닥 쪽 첫 줄: 원점이 바깥으로 0.4 내려가 있고 통로 쪽 변(국소 v 0.4)의 양끝이 바닥 판정선 위다.
            var floorRow = timbers.Where(p =>
            {
                var a = World(p, 0, MineDressingLayout.ShaftTimberHeight);
                var b = World(p, p.ScaleX, MineDressingLayout.ShaftTimberHeight);
                return a.y < c.CenterAt((float)a.x) && Math.Abs(a.y - Floor(c, (float)a.x)) < 1e-3 && Math.Abs(b.y - Floor(c, (float)b.x)) < 1e-3;
            }).ToList();
            Assert.Greater(floorRow.Count, 0, "바닥 쪽 첫 줄이 없다");
            Assert.AreEqual(spans[0].from, floorRow.Min(p => World(p, 0, MineDressingLayout.ShaftTimberHeight).x), 1e-3);
            Assert.AreEqual(spans[0].to, floorRow.Max(p => World(p, p.ScaleX, MineDressingLayout.ShaftTimberHeight).x), 1e-3);
        }

        [Test]
        public void 갱_벽_갱목은_판정선_바깥에만_있고_첫_줄은_판정선에_붙는다()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var w = MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing);
            var timbers = w.Where(p => p.Kind == MinePartKind.ShaftTimber).ToList();
            Assert.Greater(timbers.Count, 80);
            //  3막: 수직 갱 좁은 구간 + 뒤 전이 3 m, 앞은 문틀 끝(24.8)부터. 깊은 갱도 ± 3 m.
            //  10-12 사람 판 레이아웃 변경: 24.8~39.8 · 261.25~283.25 → 24.8~41.0 · 294.45~316.45. 갱목 구간(값은 위 테스트가 지킨다)에서 가져온다.
            var spans = MineDressingLayout.ShaftSpans(c, z);
            Assert.AreEqual(2, spans.Count);
            var first = timbers.Where(p => p.X < 100f).ToList();
            Assert.AreEqual(spans[0].from, first.Min(p => Math.Min(World(p, 0, 0).x, World(p, p.ScaleX, 0).x)), 0.45f);
            Assert.AreEqual(spans[0].to, first.Max(p => Math.Max(World(p, 0, 0).x, World(p, p.ScaleX, 0).x)), 0.45f);
            var deep = timbers.Where(p => p.X > 100f).ToList();
            Assert.AreEqual(spans[1].from, deep.Min(p => Math.Min(World(p, 0, 0).x, World(p, p.ScaleX, 0).x)), 0.45f);
            Assert.AreEqual(spans[1].to, deep.Max(p => Math.Max(World(p, 0, 0).x, World(p, p.ScaleX, 0).x)), 0.45f);

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
            //  두 줄(Task 7 Fix 1 — 16줄은 거대한 판자 경사로로 읽혔다): 둘째 줄은 반 칸 어긋나 조각이 하나씩 더 많다.
            //  10-12: 갱이 둘(수직 갱 + 깊은 갱)이라 갱마다 여유.
            Assert.That(timbers.Count, Is.InRange((touchFloor + touchCeiling) * 2 - 8, (touchFloor + touchCeiling) * 2 + 24));
        }

        [Test]
        public void 계단_칸마다_바위_단_윗면이_바닥선_꺾임에_맞는다()
        {
            var c = Course();
            var z = MineDressingLayout.Zones(c);
            var s = MineDressingLayout.StairBenches(c, z);
            var floorBenches = s.Where(p => p.Kind == MinePartKind.BenchFace && !p.Flipped).OrderBy(p => p.X).ToList();
            var ceilBenches = s.Where(p => p.Kind == MinePartKind.BenchFace && p.Flipped).ToList();
            //  3막 계단 내리막: 관문 6개, 칸 = 갱 바닥 관문(관문 2) ~ 마지막 계단 관문(관문 8), 관문 간격씩.
            //  10-12 사람 판 레이아웃 변경: 계단 관문 사이 5.4 → 8 m(StairExtra 2.6, 갱 바닥 관문 → 첫 계단 관문만 5.4 그대로),
            //  41.225 ~ 73.625 → 42.425 ~ 87.825. 관문 x는 코스에서 찾는다.
            //  10-13 동적 지형: 계단 관문은 흔들리는 승강기라 Gates가 아니라 Lifts에 있고 간격 8 → 6.8 m. 칸 끝 = 갱 바닥 관문 + 계단 승강기 6개(쉬는 자리).
            //  아래 칸 검사가 쓰는 번호(2~8)를 그대로 두려고 앞 두 칸은 비워 둔다.
            var gates = new List<MineGate> { default, default, c.Gates[2] };
            gates.AddRange(c.Lifts.Take(6).Select(l => l.Gate));
            Assert.AreEqual(5.4f, gates[3].X - gates[2].X, 0.01f, "갱 바닥 관문 → 첫 계단 관문");
            for (int i = 3; i < 8; i++) { Assert.AreEqual(6.8f, gates[i + 1].X - gates[i].X, 0.01f, $"계단 관문 {i} → {i + 1} 간격"); }
            Assert.AreEqual(6, floorBenches.Count);
            Assert.AreEqual(6, ceilBenches.Count);
            Assert.AreEqual(gates[2].X, floorBenches[0].X, 0.01f);
            Assert.AreEqual(gates[7].X, floorBenches[5].X, 0.01f);
            foreach (var p in floorBenches.Concat(ceilBenches))
            {
                Func<float, float> line = p.Flipped ? (Func<float, float>)(x => Ceiling(c, x)) : (x => Floor(c, x));
                var a = World(p, 0, 0);
                var b = World(p, p.ScaleX, 0);
                Assert.AreEqual(line((float)a.x), (float)a.y, 0.05f, $"{p} 한끝");
                Assert.AreEqual(line((float)b.x), (float)b.y, 0.05f, $"{p} 다른 끝");
                //  칸 = 이웃한 두 관문 사이(양끝 x가 관문 2~8 중 이웃한 둘).
                double lo = Math.Min(a.x, b.x), hi = Math.Max(a.x, b.x);
                int k = Enumerable.Range(2, 6).FirstOrDefault(i => Math.Abs(gates[i].X - lo) < 0.01);
                Assert.Greater(k, 0, $"{p} 칸 왼끝이 계단 관문 x가 아니다");
                Assert.AreEqual(gates[k + 1].X, (float)hi, 0.01f, $"{p} 칸 = 이웃한 관문 사이");
                //  몸통(아래로 4.5)은 통로 밖 — 판정선에서 바깥으로.
                var inner = World(p, p.ScaleX / 2, -2.25);
                if (p.Flipped) { Assert.Greater(inner.y, Ceiling(c, (float)inner.x)); } else { Assert.Less(inner.y, Floor(c, (float)inner.x)); }
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
                var rock = pieces.Where(p => p.Kind == MinePartKind.RockMass || p.Kind == MinePartKind.EdgeRim
                                             || p.Kind == MinePartKind.MergeSlope || p.Kind == MinePartKind.PadGlow).ToList();
                //  판정면 앞(앞면 z < 0)에 그려진 것 전부 — 열린 칸·틈 위에 하나라도 있으면 "닿을 것처럼 보이는데 지나간다".
                var inFront = pieces.Where(p => Front(p) < 0f).ToList();
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
                float rockFront = pieces.Where(p => p.Kind == MinePartKind.RockMass).Min(Front);
                float slopeFront = pieces.Where(p => p.Kind == MinePartKind.MergeSlope).Min(Front);
                foreach (var cap in pieces.Where(p => p.Kind == MinePartKind.EdgeRim))
                {
                    Assert.Less(Front(cap), Math.Min(rockFront, slopeFront), "밝은 테가 바위 뒤에 묻혔다");
                    Assert.LessOrEqual(Back(cap), 0f, "밝은 테 뒷면이 판정면 뒤");
                }
            }
        }

        [Test]
        public void 밝은_테가_굴_방_틈의_닿는_바위_가장자리를_빈틈없이_덮는다()
        {
            var c = Course();
            foreach (MineFork f in c.Forks)
            {
                var caps = MineDressingLayout.ForkPieces(c, f).Where(p => p.Kind == MinePartKind.EdgeRim).ToList();
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

                //  ⚡ 판은 칸막이 앞머리(번개_판은_칸막이_앞머리에_… 테스트) — 굴 시작 앞.
                var bolt = pieces.Single(p => p.Kind == MinePartKind.BoltSign);
                Assert.LessOrEqual(bolt.X + 0.95f * bolt.ScaleX, bore.X0);

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
            //  굴뚝 벽 사다리·랜턴은 바위 면 장식(판정 고체 위, 판정면 앞 — 바위_면_장식_… 테스트들)이라 뺀다.
            var chimney = MineDressingLayout.ChimneyWalls(c, 7, z);
            var all = AllSpreadPieces(c, z).Where(p => !chimney.Contains(p)).ToList();
            Assert.IsTrue(all.Any(p => Touchable.Contains(p.Kind)) && all.Any(p => Decoration.Contains(p.Kind)));
            Assert.IsTrue(all.Any(p => p.Kind == MinePartKind.ExitSign) && all.Any(p => p.Kind == MinePartKind.CliffEdge) && all.Any(p => p.Kind == MinePartKind.FinishFlag));
            foreach (var p in all)
            {
                float front = Front(p) - p.Z, back = Back(p) - p.Z;
                //  갱목 뒤 바위(RockMass, 앞면 ≥ 0)는 판정선에서 물러난 바탕이라 뺀다(수직_갱은_… 테스트가 지킨다).
                if (Touchable.Contains(p.Kind) && p.Z + front < 0f) { Assert.LessOrEqual(p.Z + back, 0.001f, $"{p} 닿는 부품 뒷면이 판정면 뒤"); }
                else if (Decoration.Contains(p.Kind)) { Assert.Greater(p.Z + front, 0f, $"{p} 장식이 판정면 앞"); }
            }
            //  범위(옷 끝 = 코스 끝 + 여유) 밖에는 놓지 않는다.
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

        // ══ Task 7 Fix 1(10-10): 첫 굽기 캡처에서 나온 것 — "선명하고 테두리가 밝으면 닿는 것" ══
        //  판정면 앞 그림, 그리고 판정면과 안개 막 사이의 장식은 열린 통로(닿지 않는 곳)에 있으면 장애물로 읽힌다.

        //  부품 국소 발자국(원점·축척 규칙은 Task 3 실측, 굽기 Place와 같다): 직사각 (u0, u1, v0, v1)은 축척을 곱한 값, tri면 MergeSlope 삼각형.
        static (double u0, double u1, double v0, double v1, bool tri)? Footprint(MinePiece p)
        {
            double sx = p.ScaleX, sy = p.ScaleY;
            switch (p.Kind)
            {
                case MinePartKind.TrestleBay: return (-1.2 * sx, 1.2 * sx, -12 * sy, 0, false);
                case MinePartKind.CeilingRock: return (-1.0 * sx, 1.0 * sx, 0, 4 * sy, false);
                case MinePartKind.Beam: return (-0.5 * sx, 0.5 * sx, 0, 0.8 * sy, false);
                case MinePartKind.RockMass:
                case MinePartKind.RockVeneer:
                case MinePartKind.FinishBanner:
                case MinePartKind.EdgeRim: return (Math.Min(0, sx), Math.Max(0, sx), Math.Min(0, sy), Math.Max(0, sy), false);
                case MinePartKind.MergeSlope: return (Math.Min(0, sx), Math.Max(0, sx), Math.Min(0, sy), Math.Max(0, sy), true);
                case MinePartKind.GateCap: return (-0.975 * sx, 0.975 * sx, -0.4 * sy, 0, false);
                case MinePartKind.ShaftTimber: return (0, sx, 0, 0.4 * sy, false);
                case MinePartKind.BenchFace: return (0, sx, -4.5 * sy, 0, false);
                case MinePartKind.DividerWall: return (0, sx, -0.6 * sy, 0.6 * sy, false);
                case MinePartKind.PadGlow: return (-0.6, 0.6, -0.15, 0.15, false);
                case MinePartKind.BoltSign: return (-0.95 * sx, 0.95 * sx, -0.547 * sy, 0.547 * sy, false);
                case MinePartKind.EntranceSign: return (-1.5 * sx, 1.5 * sx, -0.5 * sy, 0.5 * sy, false);
                case MinePartKind.LanternString: return (-0.014 * sx, 1.014 * sx, -0.756 * sy, 0.011 * sy, false);
                case MinePartKind.Lantern: return (-0.3 * sx, 0.3 * sx, -2.2 * sy, 0, false);
                case MinePartKind.Cage: return (-0.8, 0.8, -1.0, 1.0, false);
                case MinePartKind.CageRail: return (-1.03 * sx, 1.03 * sx, 0, sy, false);
                case MinePartKind.ShortScaffold: return (-1.2, 1.2, 0, 2.5, false);
                case MinePartKind.ExitSign: return (-1.5 * sx, 1.5 * sx, -0.5 * sy, 0.5 * sy, false);
                case MinePartKind.FinishFlag: return (-0.5 * sx, 0.5 * sx, 0, 3 * sy, false);
                case MinePartKind.CliffEdge: return (0, sx, -sy, 0, false);
                case MinePartKind.WallLadder: return (-0.41 * sx, 0.41 * sx, -16 * sy, 0, false);
                case MinePartKind.CoasterRail: return (-1.2 * sx, 1.2 * sx, 0, MineDressingLayout.RailTop * sy, false);
                case MinePartKind.RailPost: return (0, sx, 0, 0.4 * sy, false);
                default: return null;
            }
        }

        //  발자국 안을 세계 0.1 m 간격으로 훑은 점.
        static IEnumerable<(double x, double y)> FootprintSamples(MinePiece p, double step = 0.1)
        {
            var f = Footprint(p);
            if (f == null) { yield break; }
            var (u0, u1, v0, v1, tri) = f.Value;
            int nu = Math.Max(1, (int)Math.Ceiling((u1 - u0) / step)), nv = Math.Max(1, (int)Math.Ceiling((v1 - v0) / step));
            for (int i = 0; i <= nu; i++)
            {
                for (int j = 0; j <= nv; j++)
                {
                    double u = u0 + (u1 - u0) * i / nu, v = v0 + (v1 - v0) * j / nv;
                    if (tri)
                    {
                        double s = u / p.ScaleX, t = v / p.ScaleY;
                        if (t < s - 1e-9) { continue; }
                    }
                    yield return World(p, u, v);
                }
            }
        }

        //  판정 바닥·천장선 = 회색 박스 조각(Breaks 사이 현).
        sealed class Judge
        {
            readonly MineCourse c;
            readonly float[] xs;

            public Judge(MineCourse c)
            {
                this.c = c;
                var l = new List<float> { -40f };
                l.AddRange(c.Breaks(MineDressingLayout.BreakStep));
                l.Add(c.Length + 40f);
                xs = l.ToArray();
            }

            double Chord(double x, Func<float, float> line)
            {
                int i = Array.BinarySearch(xs, (float)x);
                if (i < 0) { i = ~i; }
                i = Math.Max(1, Math.Min(xs.Length - 1, i));
                float a = xs[i - 1], b = xs[i];
                double ya = line(a), yb = line(b);
                return b - a < 1e-6 ? ya : ya + (yb - ya) * (x - a) / (b - a);
            }

            public double FloorAt(double x) => Chord(x, q => c.CenterAt(q) - c.HalfAt(q));
            public double CeilAt(double x) => Chord(x, q => c.CenterAt(q) + c.HalfAt(q));

            //  열린 곳 = 판정 바닥·천장선 사이, 관문 파이프·칸막이·굴 바위 밖.
            public bool Open(double x, double y)
            {
                if (y <= FloorAt(x) || y >= CeilAt(x)) { return false; }
                foreach (MineGate g in c.Gates)
                {
                    if (Math.Abs(x - g.X) > g.Width / 2.0) { continue; }
                    double gb = g.GapCenter - g.Gap / 2.0, gt = g.GapCenter + g.Gap / 2.0;
                    if ((y < gb && (float.IsNaN(g.Low) || y > g.Low)) || (y > gt && (float.IsNaN(g.High) || y < g.High))) { return false; }
                }
                foreach (MineRect w in c.Walls)
                {
                    if (x >= w.X0 && x <= w.X1 && y >= w.Y0 && y <= w.Y1) { return false; }
                }
                foreach (MineTube t in c.Tubes)
                {
                    if (x < t.X0 || x > t.X1) { continue; }
                    double e = JudgedCenter(t, x);
                    if ((y < e - t.Gap / 2.0 && (float.IsNaN(t.Low) || y > t.Low)) || (y > e + t.Gap / 2.0 && (float.IsNaN(t.High) || y < t.High))) { return false; }
                }
                return true;
            }

            //  열린 곳 안으로 0.05 넘게 들어간 점(둘레 0.05·0.03의 열여섯 방향도 다 열린 곳) — 그림 경계 = 판정 경계 ± 0.05.
            //  대각선까지 보는 것은 판정 고체의 모서리(굴 끝 · 틈 가장자리) 바로 바깥 점을 거르려고다.
            public bool DeepOpen(double x, double y)
            {
                if (!Open(x, y)) { return false; }
                foreach (double r in new[] { 0.05, 0.03 })
                {
                    for (int k = 0; k < 16; k++)
                    {
                        double a = k * Math.PI / 8;
                        if (!Open(x + r * Math.Cos(a), y + r * Math.Sin(a))) { return false; }
                    }
                }
                return true;
            }
        }

        static MineZones Zones3(MineCourse c) => MineDressingLayout.Zones(c);

        /// <summary>첫 수직 갱·깊은 갱 시작 x(코스 구간에서).</summary>
        static (float first, float deep) DropXs(MineCourse c)
        {
            var drops = c.Sections.Where(sec => sec.Name == "수직 갱 낙하").ToList();
            return (drops[0].X, drops[drops.Count - 1].X);
        }

        //  범위 안 바닥·천장 + 구간 부품 전부.
        static List<MinePiece> AllRangePieces(MineCourse c, MineZones z)
        {
            var all = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd);
            all.AddRange(AllSpreadPieces(c, z));
            return all;
        }

        static readonly Dictionary<MinePartKind, float> FrontOf = new Dictionary<MinePartKind, float>
        {
            //  부품 앞면 z(원점 기준, Task 3·Task 7 실측).
            { MinePartKind.TrestleBay, -0.65f }, { MinePartKind.CeilingRock, -1.607f }, { MinePartKind.Beam, -0.8f },
        };

        static float Front(MinePiece p) => p.Z + (FrontOf.TryGetValue(p.Kind, out float f) ? f : PartDepth[p.Kind].front * MineDressingLayout.DepthScale(p.Kind));
        static float Back(MinePiece p) => p.Z + PartDepth[p.Kind].back * MineDressingLayout.DepthScale(p.Kind);

        [Test]
        public void 판정면_앞_그림은_범위_안_열린_통로에_0_05_넘게_들어가지_않는다()
        {
            //  캡처(10-10 Task 7): 굽은 칸 비계 다리가 열린 골짜기를 가로지르고(1.90 m), 가파른 천장 바위 옆면이 V 아래로 내려왔다(1.20 m).
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var front = AllRangePieces(c, z).Where(p => Front(p) < 0f).ToList();
            Assert.Greater(front.Count, 300);
            //  10-12: 코스 전체(−20 ~ 코스 끝 + 20) — 롤러코스터·급반전·깊은 갱·굴뚝·출구까지.
            Assert.AreEqual(Course().Length + 20f, z.DressEnd, 1e-3f);
            foreach (float x in new[] { 180f, 225f, 270f, 325f, 350f, 370f, 400f })
            {
                Assert.IsTrue(front.Any(p => Math.Abs(p.X - x) < 6f), $"x {x} 근처에 판정면 앞 그림이 없다(범위가 코스 전체가 아니다)");
            }
            var bad = new List<string>();
            foreach (var p in front)
            {
                foreach (var (x, y) in FootprintSamples(p))
                {
                    if (x < From || x > z.DressEnd) { continue; }
                    if (judge.DeepOpen(x, y)) { bad.Add($"{p} ({x:F2},{y:F2})"); break; }
                }
            }
            Assert.IsEmpty(bad, $"판정면 앞 그림이 열린 통로에: {bad.Count}개\n" + string.Join("\n", bad.Take(20)));
        }

        [Test]
        public void 판정면과_안개_막_사이_장식은_열린_통로에_없다()
        {
            //  캡처: 수직 갱 통로 안 케이지, 계단 통로 안 짧은 비계가 장애물로 읽혔다. 갱 입구 문틀은 굽기가 통로 안을 잘라 낸다(아래 테스트).
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var mid = AllRangePieces(c, z).Where(p => Front(p) >= 0f && Front(p) < MineDressingLayout.HazeZ).ToList();
            Assert.IsNotEmpty(mid);
            Assert.IsTrue(mid.Any(p => p.X > z.ExitStart), "출구 쪽 판정면 뒤 장식(간판·절벽 끝)도 본다");
            var bad = new List<string>();
            foreach (var p in mid)
            {
                if (MineDressingLayout.ClipsToCorridor(p.Kind)) { continue; }
                //  결승선 줄·깃발은 예외(10-12 시안 B) — 피할 것이 아니라 지나가는 선. 자리는 결승선 테스트가 본다.
                if (IsFinishLine(p)) { continue; }
                Assert.IsNotNull(Footprint(p), $"{p.Kind} 발자국이 없다");
                foreach (var (x, y) in FootprintSamples(p))
                {
                    if (judge.DeepOpen(x, y)) { bad.Add($"{p} ({x:F2},{y:F2})"); break; }
                }
            }
            Assert.IsEmpty(bad, "판정면 뒤 장식이 열린 통로에:\n" + string.Join("\n", bad));
        }

        [Test]
        public void 비계는_완만하고_곧은_긴_바닥에만_그_밖은_다리_없는_바위_띠와_밝은_테()
        {
            var c = Course();
            var z = Zones3(c);
            var fc = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd);
            var bays = fc.Where(p => p.Kind == MinePartKind.TrestleBay).ToList();
            Assert.Greater(bays.Count, 20);
            foreach (var b in bays)
            {
                Assert.LessOrEqual(Math.Abs(b.AngleDegrees), MineDressingLayout.TrestleMaxSlopeDegrees + 1e-3, $"{b} 기운 칸에 비계");
            }
            //  굽은 바닥(입구 오르막 19.8~22.8)에는 비계가 없고 바위 띠 + 밝은 테가 바닥선을 맡는다.
            //  10-12 사람 판 레이아웃 변경: 낮은 천장 118.4 → 138.2, 롤러코스터 151.25 → 171.05 — 구간 x는 코스에서 찾는다.
            //  10-13 동적 지형: 낮은 천장 물결이 순서에서 빠져(진자 광차 자리) 그 표본을 지웠다. 대신 진자 광차의 평평한 바닥엔 비계가 선다.
            float pendulumX = c.Sections.Single(sec => sec.Name == "진자 광차").X;
            float coasterX = c.Sections.Single(sec => sec.Name == "레일 롤러코스터").X;
            Assert.IsTrue(bays.Any(p => p.X > pendulumX && p.X < coasterX), $"[{pendulumX}, {coasterX}] 평평한 진자 광차 바닥에 비계가 없다");
            foreach (var (a, b) in new[] { (19.8f, 22.8f) })
            {
                Assert.IsFalse(bays.Any(p => p.X > a + 0.01f && p.X < b - 0.01f), $"[{a}, {b}] 굽은 바닥에 비계");
                Assert.IsTrue(fc.Any(p => p.Layer == 0 && p.Kind == MinePartKind.EdgeRim && p.X > a && p.X < b), $"[{a}, {b}] 바닥 밝은 테가 없다");
                Assert.IsTrue(fc.Any(p => p.Layer == 0 && p.Kind == MinePartKind.RockMass && p.X > a - 0.01f && p.X < b), $"[{a}, {b}] 바닥 바위 띠가 없다");
            }
            //  밝은 테가 굽은 바닥선을 빈틈없이 덮는다(테 안쪽 0.03).
            //  굴 안 천장은 전부 차분한 바위 띠(천장 바위 부품 CeilingRock은 쓰지 않는다 — 사용자 10-10 "시끄럽다").
            Assert.IsFalse(fc.Any(p => p.Kind == MinePartKind.CeilingRock || p.Kind == MinePartKind.GateCap), "바닥·천장에 천장 바위·쇠테가 있다");
            var rims = fc.Where(p => p.Kind == MinePartKind.EdgeRim).ToList();
            var judge = new Judge(c);
            foreach (var (a, b, layer) in new[] { (19.85, 22.75, 0), (24.85, coasterX - 0.05, 1) })
            {
                for (double x = a; x < b; x += 0.05)
                {
                    if (layer == 1 && MineDressingLayout.PlainSpans(c, z, From, z.DressEnd).All(sp => x < sp.from || x > sp.to)) { continue; }   // 갱목·단 구간
                    double y = layer == 0 ? judge.FloorAt(x) - 0.03 : judge.CeilAt(x) + 0.03;
                    Assert.IsTrue(rims.Any(p => p.Layer == layer && Covers(p, x, y)), $"{(layer == 0 ? "바닥" : "천장")} ({x:F2},{y:F2})에 밝은 테가 없다");
                    //  테 밖(바위 쪽 0.5·3·8 m)은 바위가 채운다.
                    foreach (double d in new[] { 0.5, 3.0, 8.0 })
                    {
                        double yy = layer == 0 ? judge.FloorAt(x) - d : judge.CeilAt(x) + d;
                        Assert.IsTrue(fc.Any(p => p.Layer == layer && (p.Kind == MinePartKind.RockMass || p.Kind == MinePartKind.MergeSlope) && Covers(p, x, yy)),
                                      $"({x:F2},{yy:F2}) 판정 고체에 바위가 없다");
                    }
                }
            }
        }

        [Test]
        public void 바닥_천장_그림은_범위를_빈틈없이_딱_맞게_덮는다()
        {
            //  캡처: 범위에 걸친 회색 Floor_117(146.8~178.8)이 범위 안에 회색으로 보였다. 이제 그림 범위는 넓히지 않고 [from, to] 그대로다
            //  (걸친 회색 조각은 굽기가 범위 밖 부분만 남긴다 — 아래 테스트).
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var fc = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd);
            var spans = MineDressingLayout.PlainSpans(c, z, From, z.DressEnd);
            //  판정선 바로 바깥(0.1)을 그 층 그림이 덮나.
            bool Covered(int layer, double x)
            {
                double y = layer == 0 ? judge.FloorAt(x) - 0.1 : judge.CeilAt(x) + 0.1;
                return fc.Any(p => p.Layer == layer && InFootprint(p, x, y));
            }
            foreach (int layer in new[] { 0, 1 })
            {
                foreach (var (a, b) in spans)
                {
                    for (double x = a + 0.02; x < b - 0.02; x += 0.1)
                    {
                        //  출구 문틀 천장은 상인방이 맡는다(fix1 — 출구_문틀_… 테스트).
                        if (layer == 1 && x > z.ExitStart && x < z.ExitEnd) { continue; }
                        //  꺾이는 곳에서 이웃 조각 끝이 부채꼴로 벌어진 작은 홈(들보·비계 모서리)은 ± 0.15 안 이웃 점으로 봐준다.
                        Assert.IsTrue(Covered(layer, x) || (Covered(layer, x - 0.15) && Covered(layer, x + 0.15)), $"{(layer == 0 ? "바닥" : "천장")} x={x:F2} 그림이 없다");
                    }
                }
                Assert.IsFalse(Covered(layer, z.DressEnd + 0.1), "옷 끝 뒤까지 그렸다");
                Assert.IsTrue(Covered(layer, z.DressEnd - 0.5), "옷 끝(코스 끝 + 여유)까지 안 그렸다");
                Assert.IsFalse(Covered(layer, From - 0.1), "옷 앞까지 그렸다");
            }
        }

        static bool InFootprint(MinePiece p, double x, double y)
        {
            var f = Footprint(p);
            if (f == null) { return false; }
            var (u0, u1, v0, v1, tri) = f.Value;
            var (u, v) = Local(p, x, y);
            if (u < u0 - 1e-6 || u > u1 + 1e-6 || v < v0 - 1e-6 || v > v1 + 1e-6) { return false; }
            return !tri || v / p.ScaleY >= u / p.ScaleX - 1e-9;
        }

        [Test]
        public void 범위에_걸친_회색_조각은_범위_밖_부분만_남긴다()
        {
            //  Floor_117: x 146.83~178.75, 범위 끝 151.25.
            var quad = new[] { (146.83, -56.28), (178.75, -56.28), (178.75, -60.28), (146.83, -60.28) };
            var parts = MineDressingLayout.OutsideX(quad, -20, 151.25);
            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual(151.25, parts[0].Min(p => p.x), 1e-6);
            Assert.AreEqual(178.75, parts[0].Max(p => p.x), 1e-6);
            Assert.AreEqual(-56.28, parts[0].Max(p => p.y), 1e-6);
            Assert.AreEqual(-60.28, parts[0].Min(p => p.y), 1e-6);
            //  기운 조각은 자른 자리 y를 잇는다.
            var slant = new[] { (150.0, 0.0), (152.0, 2.0), (152.0, -2.0), (150.0, -4.0) };
            var sp = MineDressingLayout.OutsideX(slant, -20, 151.25).Single();
            Assert.AreEqual(1.25, sp.Where(p => Math.Abs(p.x - 151.25) < 1e-9).Max(p => p.y), 1e-6);
            Assert.AreEqual(-2.75, sp.Where(p => Math.Abs(p.x - 151.25) < 1e-9).Min(p => p.y), 1e-6);
            //  다 안이면 아무것도, 다 밖이면 그대로.
            Assert.AreEqual(0, MineDressingLayout.OutsideX(new[] { (0.0, 0.0), (2.0, 0.0), (2.0, -1.0) }, -20, 151.25).Count);
            Assert.AreEqual(3, MineDressingLayout.OutsideX(new[] { (160.0, 0.0), (162.0, 0.0), (162.0, -1.0) }, -20, 151.25).Single().Length);
        }

        [Test]
        public void 수직_갱은_벽에_갱목_두_줄과_그_뒤_바위만()
        {
            //  캡처: 7 m 깊이 16줄 3 m 판자가 거대한 판자 경사로처럼 읽혔고, 통로 안 승강 케이지가 장애물로 읽혔다.
            var c = Course();
            var z = Zones3(c);
            var w = MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing);
            Assert.IsFalse(w.Any(p => p.Kind == MinePartKind.Cage || p.Kind == MinePartKind.CageRail), "케이지·레일은 뺐다(사용자 10-10: 투머치)");
            var timbers = w.Where(p => p.Kind == MinePartKind.ShaftTimber).ToList();
            //  판자 길이는 실제 갱목 크기(1.5 m 근처) — 3 m로 늘이지 않는다.
            foreach (var t in timbers) { Assert.LessOrEqual(t.ScaleX, 1.6f, $"{t} 판자가 너무 길다"); }
            //  줄 수: 판자 가운데에서 판정선(꺾은선)까지 수직 거리 — 첫 줄 0.2, 둘째 줄 0.65. 그보다 깊은 판자는 없다.
            var judge = new Judge(c);
            var spans = MineDressingLayout.ShaftSpans(c, z);
            Assert.AreEqual(2, spans.Count, "수직 갱 + 깊은 갱(굴뚝은 뺀다)");
            double Dist(double px, double py, bool floorSide)
            {
                double best = double.MaxValue;
                var span0 = spans.First(s => px > s.from - 3f && px < s.to + 3f);
                for (double x = span0.from - 1; x <= span0.to + 1; x += 0.02)
                {
                    double ly = floorSide ? judge.FloorAt(x) : judge.CeilAt(x);
                    best = Math.Min(best, Math.Sqrt((px - x) * (px - x) + (py - ly) * (py - ly)));
                }
                return best;
            }
            int second = 0;
            foreach (var t in timbers)
            {
                var (mx, my) = World(t, t.ScaleX / 2, 0.2);
                double d = Dist(mx, my, my < c.CenterAt((float)mx));
                Assert.LessOrEqual(d, 0.65 + 0.06, $"{t} 판자가 둘째 줄보다 깊다(d {d:F2}) — 벽은 두 줄");
                if (d > 0.5) { second++; }
            }
            Assert.Greater(second, 10, "둘째 줄이 없다");
            //  뒤 바위(RockMass, 판정면 뒤 앞면 ≥ 0)가 갱목 뒤 판정 고체를 덮는다: 갱 구간 판정선에서 바깥 1.0~5.0 m(세로).
            var rock = w.Where(p => p.Kind == MinePartKind.RockMass || p.Kind == MinePartKind.MergeSlope).ToList();
            Assert.IsNotEmpty(rock, "갱목 뒤 바위가 없다");
            foreach (var r in rock) { Assert.GreaterOrEqual(Front(r), 0f, $"{r} 뒤 바위가 판정면 앞(갱목을 가린다)"); }
            foreach (var span in spans)
            for (double x = span.from + 0.1; x < span.to - 0.1; x += 0.25)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    double line = side < 0 ? judge.FloorAt(x) : judge.CeilAt(x);
                    //  70° 벽은 수직 0.6 m가 세로로 1.75 m라 세로 3.0~5.0에서 본다.
                    for (double d = 3.0; d <= 5.0; d += 0.5)
                    {
                        double y = line + side * d;
                        Assert.IsTrue(rock.Any(r => Covers(r, x, y)) || timbers.Any(t => InTimber(t, x, y)), $"갱 벽 ({x:F2},{y:F2}) 판정 고체에 바위·갱목 그림이 없다");
                    }
                }
            }
        }

        static bool InTimber(MinePiece p, double x, double y)
        {
            var (u, v) = Local(p, x, y);
            return u >= 0 && u <= p.ScaleX && v >= 0 && v <= 0.4 * p.ScaleY;
        }

        [Test]
        public void 계단은_바위_단만_짧은_비계는_없다()
        {
            var c = Course();
            var s = MineDressingLayout.StairBenches(c, Zones3(c));
            Assert.IsFalse(s.Any(p => p.Kind == MinePartKind.ShortScaffold), "짧은 비계는 뺐다(통로 안 장애물로 읽힘, 사용자 10-10: 투머치)");
            Assert.AreEqual(12, s.Count(p => p.Kind == MinePartKind.BenchFace));
        }

        [Test]
        public void 갱_입구는_문틀과_간판만이고_문틀은_열린_통로_안을_잘라_낸다()
        {
            var c = Course();
            var z = Zones3(c);
            var e = MineDressingLayout.Entrance(c, z);
            CollectionAssert.AreEquivalent(new[] { MinePartKind.EntranceFrame, MinePartKind.EntranceSign }, e.Select(p => p.Kind).ToArray(),
                                           "입구는 문틀(상인방) + 간판만(랜턴 줄은 뺐다 — 사용자 10-10: 투머치)");
            Assert.IsTrue(MineDressingLayout.ClipsToCorridor(MinePartKind.EntranceFrame));
            //  문틀 기둥(국소 x ±1.9~2.5, y −1.02~14.62)을 삼각형 둘로 잘라 본다 — 남은 것은 통로 밖, 통로 밖 넓이는 그대로.
            var frame = e.Single(p => p.Kind == MinePartKind.EntranceFrame);
            var judge = new Judge(c);
            foreach (double side in new[] { -1.0, 1.0 })
            {
                double u0 = side < 0 ? -2.5 : 1.9, u1 = side < 0 ? -1.9 : 2.5;
                var a = (frame.X + u0, frame.Y - 1.02); var b = (frame.X + u1, frame.Y - 1.02);
                var d = (frame.X + u1, frame.Y + 14.62); var f = (frame.X + u0, frame.Y + 14.62);
                double kept = 0;
                foreach (var tri in new[] { (a, b, d), (a, d, f) })
                {
                    foreach (var poly in MineDressingLayout.ClipOutsideCorridor(c, tri.Item1, tri.Item2, tri.Item3))
                    {
                        Assert.AreEqual(1.0, poly.Sum(q => q.w0 + q.w1 + q.w2) / poly.Count, 1e-9);
                        var pts = poly.Select(q => (x: q.w0 * tri.Item1.Item1 + q.w1 * tri.Item2.Item1 + q.w2 * tri.Item3.Item1,
                                                    y: q.w0 * tri.Item1.Item2 + q.w1 * tri.Item2.Item2 + q.w2 * tri.Item3.Item2)).ToList();
                        foreach (var (x, y) in pts)
                        {
                            bool inside = y > judge.FloorAt(x) + 1e-4 && y < judge.CeilAt(x) - 1e-4;
                            Assert.IsFalse(inside, $"기둥 {side} 남긴 꼭짓점 ({x:F2},{y:F2})이 열린 통로 안");
                        }
                        for (int i = 0; i < pts.Count; i++)
                        {
                            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
                            kept += (p.x * q.y - q.x * p.y) / 2.0;
                        }
                    }
                }
                //  통로 밖 넓이(0.01 격자).
                double expect = 0;
                for (double x = frame.X + u0 + 0.005; x < frame.X + u1; x += 0.01)
                {
                    double lo = frame.Y - 1.02, hi = frame.Y + 14.62;
                    double below = Math.Max(0, Math.Min(hi, judge.FloorAt(x)) - lo), above = Math.Max(0, hi - Math.Max(lo, judge.CeilAt(x)));
                    expect += (below + above) * 0.01;
                }
                Assert.Greater(expect, 1.0);
                Assert.AreEqual(expect, Math.Abs(kept), 0.05, $"기둥 {side} 통로 밖 넓이");
            }
        }

        [Test]
        public void 번개_판은_칸막이_앞머리에_붙어_열린_칸에_걸치지_않는다()
        {
            var c = Course();
            foreach (MineFork f in c.Forks)
            {
                var bolt = MineDressingLayout.ForkPieces(c, f).Single(p => p.Kind == MinePartKind.BoltSign);
                var wall = c.Walls.Single(w => Math.Abs(w.X0 - f.X0) < 1e-3f);
                Assert.Less(Front(bolt), -0.75f - 0.75f, "칸막이 앞면보다 앞");
                Assert.GreaterOrEqual(bolt.X - 0.95f * bolt.ScaleX, wall.X0 - Eps);
                Assert.GreaterOrEqual(bolt.Y - 0.547f * bolt.ScaleY, wall.Y0 + 0.02f, "판이 칸막이 아래로 삐져나온다");
                Assert.LessOrEqual(bolt.Y + 0.547f * bolt.ScaleY, wall.Y1 - 0.02f, "판이 칸막이 위로 삐져나온다");
            }
        }

        [Test]
        public void 먼_비계는_무리로_끊어_놓아_성기게()
        {
            var c = Course();
            var d = new MineDressingLayout.BackgroundDensity(9f, 0.5f, 16f, 2, 0.75f, 0.8f, 3, 5);
            var far = MineDressingLayout.Background(c, From, To, 7, d).Where(p => p.Kind == MinePartKind.BgTrestleBay).OrderBy(p => p.X).ToList();
            var full = MineDressingLayout.Background(c, From, To, 7, Density).Where(p => p.Kind == MinePartKind.BgTrestleBay).ToList();
            double w = MineDressingLayout.BgTrestleBayWidth * 0.75;
            Assert.That(far.Count, Is.InRange(full.Count * 3 / 8 - 2, full.Count * 3 / 8 + 2), "3칸 놓고 5칸 비운다");
            int run = 1;
            for (int i = 1; i < far.Count; i++)
            {
                double gap = far[i].X - far[i - 1].X;
                if (gap < w * 1.5) { run++; Assert.LessOrEqual(run, 3, $"x={far[i].X} 무리가 3칸보다 길다"); }
                else { Assert.AreEqual(w * 6, gap, 0.01, $"x={far[i].X} 무리 사이가 5칸이 아니다"); run = 1; }
            }
        }

        // ══ Task 7 Fix 1b(10-10): 바위 면 장식 — 사용자 v2 "차분해지긴 했는데 너무 휑하다" ══
        //  판정 고체 바위 면 위에만, 열린 통로·관문·칸막이에서 떨어져, 어둡고 납작하게, 성기게.

        //  FlappyMineLook 기본값과 같은 조절판.
        static readonly MineDressingLayout.RockDecorDensity Decor = new MineDressingLayout.RockDecorDensity(
            2.6f, 0.32f, 0.12f, 0.05f, 0.03f, 0.05f, 0.025f, 0.5f, 18f, 0.45f, 2.6f, true);

        static List<MinePiece> DecorOf(MineCourse c, MineDressingLayout.RockDecorDensity d) =>
            MineDressingLayout.RockDecor(c, Zones3(c), From, Zones3(c).DressEnd, 7, d, LookTimberSpacing);

        //  바위 그림(장식의 받침) — 바닥·천장 바위 띠, 갱 벽 바위, 계단 단, 갈림길 바위.
        static List<MinePiece> RockPieces(MineCourse c, MineZones z)
        {
            var all = AllRangePieces(c, z);
            return all.Where(p => p.Kind == MinePartKind.RockMass || p.Kind == MinePartKind.MergeSlope || p.Kind == MinePartKind.BenchFace).ToList();
        }

        static bool OnRock(List<MinePiece> rock, double x, double y) => rock.Any(p =>
        {
            if (p.Kind != MinePartKind.BenchFace) { return Covers(p, x, y); }
            var (u, v) = Local(p, x, y);
            return u >= -1e-6 && u <= p.ScaleX + 1e-6 && v >= -4.5 * p.ScaleY - 1e-6 && v <= 1e-6;
        });

        static bool InGateOrWall(MineCourse c, double x, double y)
        {
            //  관문 그림 = 회색 파이프: Low/High가 NaN이면 바닥·천장선(세 점 중 먼 쪽) 너머 1 m 묻힘까지(FlappyMineCourseBuilder.PipeBury).
            foreach (MineGate g in c.Gates)
            {
                double half = g.Width / 2.0;
                if (Math.Abs(x - g.X) > half) { continue; }
                double gb = g.GapCenter - g.Gap / 2.0, gt = g.GapCenter + g.Gap / 2.0;
                var xs3 = new[] { g.X - (float)half, g.X, g.X + (float)half };
                double low = float.IsNaN(g.Low) ? xs3.Min(q => Floor(c, q)) - 1.0 : g.Low;
                double high = float.IsNaN(g.High) ? xs3.Max(q => Ceiling(c, q)) + 1.0 : g.High;
                if ((y < gb && y > low) || (y > gt && y < high)) { return true; }
            }
            return c.Walls.Any(w => x >= w.X0 && x <= w.X1 && y >= w.Y0 && y <= w.Y1);
        }

        static IEnumerable<(double x, double y)> DecorSamples(MinePiece p, double grow, double step)
        {
            var (u0, u1, v0, v1) = MineDressingLayout.DecorFootprint(p).Value;
            u0 -= grow; u1 += grow; v0 -= grow; v1 += grow;
            int nu = Math.Max(1, (int)Math.Ceiling((u1 - u0) / step)), nv = Math.Max(1, (int)Math.Ceiling((v1 - v0) / step));
            for (int i = 0; i <= nu; i++)
            {
                for (int j = 0; j <= nv; j++) { yield return World(p, u0 + (u1 - u0) * i / nu, v0 + (v1 - v0) * j / nv); }
            }
        }

        static readonly MinePartKind[] DecorKinds =
        {
            MinePartKind.StrataLens, MinePartKind.RockSeam, MinePartKind.TimberSet, MinePartKind.Cribbing, MinePartKind.OreVein,
            MinePartKind.RailScrap, MinePartKind.DeepTimber, MinePartKind.WallLadder, MinePartKind.Lantern,
        };

        [Test]
        public void 바위_면_장식_발자국은_메시_실측을_덮는다()
        {
            //  mine_parts.py 출력(Fix 1b): 축척 1의 메시 경계(x0, x1, y0, y1). 랜턴은 사슬~유리 + 빛 원판 가운데(유리 −1.85)의 보이는 반경 0.6.
            var mesh = new Dictionary<MinePartKind, (double, double, double, double)>
            {
                { MinePartKind.StrataLens, (0, 1, -0.549, 0.484) }, { MinePartKind.RockSeam, (0, 1, -0.28, 0.2) },
                { MinePartKind.TimberSet, (-1.1, 1.1, 0, 2.46) }, { MinePartKind.Cribbing, (-0.6, 0.6, 0, 1.6) },
                { MinePartKind.OreVein, (0, 1, -0.054, 0.099) }, { MinePartKind.RailScrap, (0, 1.616, 0, 0.331) },
                { MinePartKind.DeepTimber, (0, 1, 0, 0.4) }, { MinePartKind.WallLadder, (-0.41, 0.41, -16, 0) },
                { MinePartKind.Lantern, (-0.6, 0.6, -1.85 - 0.6, 0) },
            };
            foreach (var kv in mesh)
            {
                var f = MineDressingLayout.DecorFootprint(new MinePiece(kv.Key, 0, 0, 0, 0, 1, 1, 1, 1));
                Assert.IsNotNull(f, $"{kv.Key} 발자국이 없다");
                var (x0, x1, y0, y1) = kv.Value;
                Assert.That(f.Value.u0 <= x0 + 1e-6 && f.Value.u1 >= x1 - 1e-6 && f.Value.v0 <= y0 + 1e-6 && f.Value.v1 >= y1 - 1e-6,
                            $"{kv.Key} 발자국 {f.Value}가 메시 {kv.Value}를 못 덮는다");
            }
        }

        [Test]
        public void 바위_면_장식은_같은_입력이면_같다()
        {
            var c = Course();
            var a = DecorOf(c, Decor);
            var b = DecorOf(c, Decor);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i], b[i], $"{i}번째"); }
            var other = MineDressingLayout.RockDecor(c, Zones3(c), From, Zones3(c).DressEnd, 8, Decor, LookTimberSpacing);
            Assert.IsFalse(a.SequenceEqual(other), "시드가 달라도 같다");
        }

        [Test]
        public void 바위_면_장식은_열린_통로_관문_칸막이에서_떨어진_바위_그림_위에만()
        {
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var rock = RockPieces(c, z);
            var decor = DecorOf(c, Decor);
            Assert.Greater(decor.Count, 60, "장식이 너무 적다(휑하다)");
            var bad = new List<string>();
            foreach (MinePiece p in decor)
            {
                //  여유의 반(0.25)까지 넓혀도 열린 곳(둘레 포함)·관문·칸막이가 없다 — 판정선 위 밝은 테(0.12)도 안 덮는다.
                foreach (var (x, y) in DecorSamples(p, 0.25, 0.1))
                {
                    if (x < From || x > z.DressEnd || judge.Open(x, y) || InGateOrWall(c, x, y)) { bad.Add($"{p} 막힌 곳 ({x:F2},{y:F2})"); break; }
                }
                foreach (var (x, y) in DecorSamples(p, 0, 0.2))
                {
                    if (!OnRock(rock, x, y)) { bad.Add($"{p} 바위 밖 ({x:F2},{y:F2})"); break; }
                }
            }
            Assert.IsEmpty(bad, $"{bad.Count}개\n" + string.Join("\n", bad.Take(20)));
        }

        [Test]
        public void 바위_면_장식은_바위_앞면_바로_앞에_납작하게_관문보다_뒤()
        {
            //  바위 앞면 −0.26(RockZ −0.13 + 메시 −1.3 × 0.1), 관문 앞면 −1.38(GateZ −0.73 + 판자 −0.65). 장식 원점 = 뒷면(랜턴만 가운데).
            var c = Course();
            foreach (MinePiece p in DecorOf(c, Decor))
            {
                Assert.Contains(p.Kind, DecorKinds);
                Assert.Less(p.Z, -0.26f, $"{p} 바위 앞면 뒤에 숨는다");
                Assert.Greater(p.Z, -1.0f, $"{p} 너무 앞(관문과 다툰다)");
            }
            Assert.AreEqual(MineDressingLayout.DeepTimberDepth, MineDressingLayout.DepthScale(MinePartKind.DeepTimber));
        }

        [Test]
        public void 바위_면_장식_밀도는_조절판을_따르고_성기다()
        {
            var c = Course();
            var z = Zones3(c);
            Assert.IsEmpty(DecorOf(c, MineDressingLayout.RockDecorDensity.None), "다 끄면 하나도 없어야 한다");

            var d = DecorOf(c, Decor);
            var grid = d.Where(p => p.Kind != MinePartKind.DeepTimber && p.Kind != MinePartKind.WallLadder).ToList();
            //  화면 하나(폭 ~30 m, 통로 중심 ± 9 m)에 장식 상한 — 칸 하나에 하나까지라 넓이/칸² 이하, 실제로는 성기게.
            for (double x0 = z.EntranceEnd; x0 < z.DressEnd; x0 += 10)
            {
                int n = grid.Count(p => p.X >= x0 && p.X < x0 + 30 && Math.Abs(p.Y - c.CenterAt(p.X)) < 9);
                Assert.LessOrEqual(n, 45, $"x {x0:F0}~{x0 + 30:F0} 장식 {n}개 — 너무 많다");
            }
            //  종류별 비율: 렌즈가 가장 많고 받침·쌓기·레일은 드물다.
            int lens = grid.Count(p => p.Kind == MinePartKind.StrataLens), props = grid.Count(p => p.Kind == MinePartKind.TimberSet || p.Kind == MinePartKind.Cribbing);
            Assert.Greater(lens, props * 2, $"렌즈 {lens} · 받침 {props}");
            Assert.Greater(props, 0, "박힌 갱목이 없다");
            //  랜턴은 받침에만, 서로 18 m 이상.
            var lanterns = grid.Where(p => p.Kind == MinePartKind.Lantern).OrderBy(p => p.X).ToList();
            //  10-12: 범위가 코스 전체(−20 ~ 406)라 30 m에 하나 꼴까지.
            Assert.That(lanterns.Count, Is.InRange(1, (int)((z.DressEnd - From) / 30f)), "바위 면 랜턴은 성기게");
            for (int i = 1; i < lanterns.Count; i++) { Assert.GreaterOrEqual(lanterns[i].X - lanterns[i - 1].X, 18f - 2 * 2.6f, "랜턴이 붙어 있다"); }
            foreach (MinePiece l in lanterns)
            {
                Assert.IsTrue(grid.Any(p => p.Kind == MinePartKind.TimberSet && Math.Abs(p.X - l.X) < 1e-4 && Math.Abs(p.Y + MineDressingLayout.TimberSetCapUnder * p.ScaleY - l.Y) < 1e-4),
                              $"{l} 받침 상인방 아래가 아니다");
            }

            //  렌즈 확률을 두 배로 → 렌즈가 는다(다른 칸 난수는 그대로라 받침은 줄기만 한다).
            var more = new MineDressingLayout.RockDecorDensity(2.6f, 0.64f, 0.12f, 0.05f, 0.03f, 0.05f, 0.025f, 0.5f, 18f, 0.45f, 2.6f, true);
            Assert.Greater(DecorOf(c, more).Count(p => p.Kind == MinePartKind.StrataLens), lens);
            var tooMuch = new MineDressingLayout.RockDecorDensity(2.6f, 0.9f, 0.2f, 0, 0, 0, 0, 0, 18f, 0.45f, 0, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => DecorOf(c, tooMuch));
        }

        [Test]
        public void 수직_갱_벽에_깊은_갱목_두_줄과_사다리_하나()
        {
            var c = Course();
            var z = Zones3(c);
            var d = DecorOf(c, Decor);
            var spans = MineDressingLayout.ShaftSpans(c, z);
            foreach (var (from, to) in spans)
            {
            var deep = d.Where(p => p.Kind == MinePartKind.DeepTimber && p.X > from - 3f && p.X < to + 3f).ToList();
            Assert.Greater(deep.Count, 10, $"[{from}, {to}] 깊은 갱목이 없다");
            //  양쪽 벽에, 판정선에서 ShaftDeepOffset 넘게 바깥(두 줄 갱목 0~0.85 뒤).
            foreach (int side in new[] { -1, 1 })
            {
                Assert.IsTrue(deep.Any(p => side < 0 ? p.Y < c.CenterAt(p.X) : p.Y > c.CenterAt(p.X)), $"side {side} 벽에 깊은 갱목이 없다");
            }
            foreach (MinePiece p in deep)
            {
                Assert.That(p.X, Is.InRange(from - 3f, to + 3f));
                var (mx, my) = World(p, p.ScaleX / 2, 0.2);
                double gapV = Math.Min(Math.Abs(my - Floor(c, (float)mx)), Math.Abs(my - Ceiling(c, (float)mx)));
                Assert.Greater(gapV, 2.0, $"{p} 판정선에 너무 가깝다");
            }
            var ladders = d.Where(p => p.Kind == MinePartKind.WallLadder && p.X > from - 3f && p.X < to + 3f).ToList();
            Assert.AreEqual(1, ladders.Count, $"[{from}, {to}] 갱 벽 사다리는 하나");
            Assert.Greater(ladders[0].Length, 8f);
            Assert.Less(ladders[0].Y, c.CenterAt(ladders[0].X), "사다리는 아래쪽(바닥 쪽) 벽");
            }
            //  굴뚝 벽 사다리는 RockDecor가 아니라 ChimneyWalls 몫이다.
            var chimney = MineDressingLayout.ChimneySpans(c, z).Single();
            Assert.IsFalse(d.Any(p => (p.Kind == MinePartKind.DeepTimber || p.Kind == MinePartKind.WallLadder) && p.X > chimney.from && p.X < chimney.to),
                           "굴뚝에 수직 갱 깊은 갱목·사다리");

            var off = new MineDressingLayout.RockDecorDensity(2.6f, 0.32f, 0.12f, 0.05f, 0.03f, 0.05f, 0.025f, 0.5f, 18f, 0.45f, 0f, false);
            Assert.IsFalse(DecorOf(c, off).Any(p => p.Kind == MinePartKind.DeepTimber || p.Kind == MinePartKind.WallLadder), "끄면 없어야 한다");
        }

        [Test]
        public void 발판_광차는_비율대로_발판_위에_서고_다른_소품을_밀지_않는다()
        {
            var c = Course();
            MineDressingLayout.BackgroundDensity D(float chance) => new MineDressingLayout.BackgroundDensity(9f, 0.5f, 16f, 2, 0.75f, 0.8f, 2, 5, chance);
            var none = MineDressingLayout.Background(c, From, To, 7, D(0f));
            var all = MineDressingLayout.Background(c, From, To, 7, D(1f));
            var half = MineDressingLayout.Background(c, From, To, 7, D(0.5f));
            bool MidCart(MinePiece p) => p.Kind == MinePartKind.MineCart && Math.Abs(p.Z - MineDressingLayout.MidZ) < 1e-4;
            Assert.IsFalse(none.Any(MidCart));
            var walks = all.Where(p => p.Kind == MinePartKind.Walkway).ToList();
            var carts = all.Where(MidCart).ToList();
            Assert.AreEqual(walks.Count, carts.Count, "비율 1이면 발판마다");
            foreach (MinePiece cart in carts)
            {
                Assert.IsTrue(walks.Any(w => Math.Abs(w.Y - cart.Y) < 1e-4 && Math.Abs(w.X - cart.X) < w.Width / 2), $"{cart} 발판 위가 아니다");
            }
            Assert.That(half.Count(MidCart), Is.InRange(1, walks.Count - 1));
            CollectionAssert.AreEqual(none, all.Where(p => !MidCart(p)).ToList(), "광차가 다른 소품 자리를 밀었다");
        }

        // ══ 펼치기 2차(10-12): 뒤 절반 — 롤러코스터·굴뚝·출구·결승선 ══

        //  판정 고체 바깥으로 보이는 그림이 열린 곳(둘레 0.05·0.03 포함)에 들어간 표본 하나라도 있으면 그 자리.
        static string FirstDeepOpen(Judge judge, MinePiece p)
        {
            foreach (var (x, y) in FootprintSamples(p))
            {
                if (judge.DeepOpen(x, y)) { return $"{p} ({x:F2},{y:F2})"; }
            }
            return null;
        }

        //  갈림길 밖 굴 = 롤러코스터(3막 하나).
        static MineTube Coaster(MineCourse c) =>
            c.Tubes.Single(t => c.Forks.All(f => t.X1 < f.X0 - 1e-3f || t.X0 > f.X1 + 1e-3f));

        //  게임 카메라(23 m, 세로 시야 40°, 통로 중심)가 보는 반 높이 — 화면 위·아래 끝 = 통로 중심 ± 이 값.
        static readonly double CameraHalf = 23.0 * Math.Tan(20.0 * Math.PI / 180.0);

        [Test]
        public void 출구는_입구의_거울_문틀_간판이고_상인방이_천장선에_붙어_화면에_보인다()
        {
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var e = MineDressingLayout.Exit(c, z);
            var frame = e.Single(p => p.Kind == MinePartKind.EntranceFrame);
            //  1차 입구 문틀 메시를 그대로 쓴다 — 출구 존(마지막 갈림길 끝 ~ +5 m)을 꼭 채운다.
            Assert.AreEqual(z.ExitStart, frame.X - MineDressingLayout.EntranceHalfWidth, 0.01f, "문틀 왼끝 = ExitStart");
            Assert.AreEqual(z.ExitEnd, frame.X + MineDressingLayout.EntranceHalfWidth, 0.01f, "문틀 오른끝 = ExitEnd");
            Assert.IsTrue(MineDressingLayout.ClipsToCorridor(frame.Kind), "문설주는 굽기가 통로 안을 잘라 낸다(1차 입구와 같다)");
            //  fix1(캡처 x 368: 상인방·간판이 들보에 가렸다): 출구는 통로가 평평하다 — 상인방 아랫변 = 천장선(± 0.05), 문틀 x 폭 전부에서.
            double lintel = frame.Y + MineDressingLayout.EntranceLintelBottom;
            for (double x = z.ExitStart; x <= z.ExitEnd; x += 0.25)
            {
                Assert.AreEqual(judge.CeilAt(x), lintel, 0.05, $"x {x:F2} 상인방 아랫변이 천장선에서 떨어졌다");
                Assert.GreaterOrEqual(lintel, judge.CeilAt(x) - 1e-4, $"x {x:F2} 상인방이 통로 안");
            }

            var sign = e.Single(p => p.Kind == MinePartKind.ExitSign);
            Assert.AreEqual(frame.X, sign.X, Eps);
            Assert.AreEqual(frame.Y + MineDressingLayout.EntranceSignCenter, sign.Y, Eps, "간판은 상인방 앞(입구와 같은 높이)");
            Assert.GreaterOrEqual(sign.Y - 0.5f, judge.CeilAt(sign.X) - 1e-4, "간판은 통로(문틀 안) 위에");
            Assert.GreaterOrEqual(Front(sign), 0f, "간판은 판정면 뒤 장식");
            //  게임 카메라 화면 위 끝(통로 중심 + 8.37) 안에 간판이 다 든다.
            Assert.LessOrEqual(sign.Y + 0.5, c.CenterAt(sign.X) + CameraHalf, "간판 위 끝이 화면 밖");

            //  랜턴 줄은 없다(fix1 — 들보 아래 1 m에 떠 보였다. 1차 입구 랜턴 줄을 뺀 것과 같다). 문틀과 간판뿐.
            Assert.AreEqual(2, e.Count);
            Assert.IsFalse(e.Any(p => p.Kind == MinePartKind.LanternString || p.Kind == MinePartKind.Lantern), "출구 랜턴 줄");
            foreach (var p in e.Where(p => !MineDressingLayout.ClipsToCorridor(p.Kind)))
            {
                Assert.IsNull(FirstDeepOpen(judge, p));
            }
        }

        [Test]
        public void 출구_문틀_천장은_상인방_그_뒤는_들보_굴_안만_바위()
        {
            var c = Course();
            var z = Zones3(c);
            var ceil = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd).Where(p => p.Layer == 1).ToList();
            Assert.IsTrue(ceil.Any(p => p.Kind == MinePartKind.Beam && p.X > z.ExitEnd), "노을 바깥 천장에 들보가 없다");
            foreach (var p in ceil)
            {
                //  fix1: 출구 문틀 x 폭엔 천장 그림이 없다 — 들보(앞면 −1.6, 높이 0.8)가 상인방·간판을 가렸다. 상인방이 천장이다(위 테스트).
                var f = Footprint(p).Value;
                double x0 = new[] { World(p, f.u0, f.v0).x, World(p, f.u1, f.v0).x, World(p, f.u0, f.v1).x, World(p, f.u1, f.v1).x }.Min();
                double x1 = new[] { World(p, f.u0, f.v0).x, World(p, f.u1, f.v0).x, World(p, f.u0, f.v1).x, World(p, f.u1, f.v1).x }.Max();
                Assert.IsFalse(x1 > z.ExitStart + 0.01 && x0 < z.ExitEnd - 0.01, $"{p} 출구 문틀 x 폭에 천장 그림");
                if (p.X > z.ExitEnd + 0.01f) { Assert.AreEqual(MinePartKind.Beam, p.Kind, $"{p} 출구 뒤 천장은 들보(입구 앞과 거울)"); }
                else if (p.X > z.EntranceEnd + 0.01f && p.X < z.ExitStart - 0.01f) { Assert.AreNotEqual(MinePartKind.Beam, p.Kind, $"{p} 굴 안 천장에 들보"); }
            }
            //  상인방(문틀 메시 문턱 기준 y 14.62~15.42, x ±2.5)이 문틀 x 폭 천장선 바로 위(0.03)를 덮는다.
            var frame = MineDressingLayout.Exit(c, z).Single(p => p.Kind == MinePartKind.EntranceFrame);
            var judge = new Judge(c);
            for (double x = z.ExitStart + 0.02; x < z.ExitEnd - 0.02; x += 0.1)
            {
                double y = judge.CeilAt(x) + 0.03;
                Assert.IsTrue(Math.Abs(x - frame.X) <= MineDressingLayout.EntranceHalfWidth && y >= frame.Y + 14.62 - 1e-4 && y <= frame.Y + 15.42,
                              $"출구 천장 ({x:F2},{y:F2})을 상인방이 안 덮는다");
            }
        }

        [Test]
        public void 수직_갱_규칙은_내리막_갱만_굴뚝은_따로()
        {
            var c = Course();
            var z = Zones3(c);
            var chimneys = MineDressingLayout.ChimneySpans(c, z);
            //  굴뚝 = 오르막 좁은 구간 [a − 2, b + 2](21 m) ± 전이 3 m.
            //  10-12 사람 판 레이아웃 변경: 310.35~341.35 → 343.55~374.55 — 굴뚝 x는 코스 구간에서 찾는다.
            float climbX = c.Sections.Single(sec => sec.Name == "굴뚝 오르기").X;
            Assert.AreEqual(1, chimneys.Count, string.Join(", ", chimneys));
            Assert.AreEqual(climbX - 2f - 3f, chimneys[0].from, 1e-3f);
            Assert.AreEqual(climbX + 21f + 2f + 3f, chimneys[0].to, 1e-3f);
            foreach (var (a, b) in MineDressingLayout.ShaftSpans(c, z))
            {
                Assert.IsTrue(b <= chimneys[0].from || a >= chimneys[0].to, $"갱목 구간 [{a}, {b}]이 굴뚝과 겹친다");
            }
            Assert.IsFalse(MineDressingLayout.ShaftWalls(c, z, LookTimberSpacing).Any(p => p.X > chimneys[0].from - 1f && p.X < chimneys[0].to + 1f),
                           "굴뚝에 수직 갱 갱목·바위");
            //  굴뚝 바닥·천장은 바닥·천장 그림(바위 띠 + 밝은 테)이 맡는다 — 비계는 없다(좁은 바위 굴).
            var fc = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd);
            Assert.IsTrue(MineDressingLayout.PlainSpans(c, z, From, z.DressEnd).Any(sp => sp.from <= chimneys[0].from && sp.to >= chimneys[0].to));
            Assert.IsFalse(fc.Any(p => p.Kind == MinePartKind.TrestleBay && p.X > chimneys[0].from && p.X < chimneys[0].to), "굴뚝 바닥에 비계");
            var judge = new Judge(c);
            for (double x = climbX + 5; x < climbX + 15; x += 0.5)
            {
                Assert.IsTrue(fc.Any(p => p.Layer == 0 && p.Kind == MinePartKind.EdgeRim && Covers(p, x, judge.FloorAt(x) - 0.03)), $"굴뚝 바닥 x {x} 밝은 테가 없다");
                Assert.IsTrue(fc.Any(p => p.Layer == 1 && p.Kind == MinePartKind.EdgeRim && Covers(p, x, judge.CeilAt(x) + 0.03)), $"굴뚝 천장 x {x} 밝은 테가 없다");
            }
        }

        [Test]
        public void 굴뚝_벽은_판정선_밖_바위_위에_사다리와_매달린_랜턴이_위로()
        {
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var (from, to) = MineDressingLayout.ChimneySpans(c, z).Single();
            var w = MineDressingLayout.ChimneyWalls(c, 7, z);
            var ladders = w.Where(p => p.Kind == MinePartKind.WallLadder).ToList();
            var lanterns = w.Where(p => p.Kind == MinePartKind.Lantern).OrderBy(p => p.X).ToList();
            Assert.AreEqual(w.Count, ladders.Count + lanterns.Count, "굴뚝 벽은 사다리와 랜턴만");
            Assert.GreaterOrEqual(ladders.Count, 2, "사다리가 위로 이어지지 않는다");
            Assert.Greater(ladders.Sum(p => p.Length), 20f, "사다리가 굴뚝을 따라 길게 오르지 않는다");
            Assert.GreaterOrEqual(lanterns.Count, 3);

            //  사다리는 바닥 쪽 벽, 오르는 기울기(53°)를 따라. 국소 −y(위 끝 → 아래 끝)가 세계 (sin a, −cos a).
            double slope = Math.Atan2(28.0, 21.0);
            foreach (var p in ladders)
            {
                Assert.Less(p.Y, judge.FloorAt(p.X), $"{p} 사다리 위 끝이 바닥선 위");
                double a = p.AngleDegrees * Math.PI / 180.0;
                double dot = Math.Sin(a) * -Math.Cos(slope) + -Math.Cos(a) * -Math.Sin(slope);
                Assert.Greater(dot, 0.99, $"{p} 사다리가 굴뚝 기울기를 안 따른다");
                Assert.LessOrEqual(p.ScaleY, 1f + Eps, $"{p} 사다리를 늘였다(가로대가 벌어진다)");
            }
            //  랜턴은 천장 쪽 벽에 매달려 굴뚝을 따라 위로.
            for (int i = 0; i < lanterns.Count; i++)
            {
                Assert.Greater(lanterns[i].Y, judge.CeilAt(lanterns[i].X), $"{lanterns[i]} 랜턴이 천장선 아래");
                if (i > 0) { Assert.Greater(lanterns[i].Y, lanterns[i - 1].Y, "랜턴이 위로 이어지지 않는다"); }
            }

            //  전부 굴뚝 안, 발자국(여유 0.25)이 열린 곳·관문에 안 닿고 바위 그림 위에.
            var rock = RockPieces(c, z);
            var bad = new List<string>();
            foreach (var p in w)
            {
                Assert.That(p.X, Is.InRange(from, to), $"{p} 굴뚝 밖");
                foreach (var (x, y) in DecorSamples(p, 0.25, 0.1))
                {
                    if (judge.Open(x, y) || InGateOrWall(c, x, y)) { bad.Add($"{p} 막힌 곳 ({x:F2},{y:F2})"); break; }
                }
                foreach (var (x, y) in DecorSamples(p, 0, 0.2))
                {
                    if (!OnRock(rock, x, y)) { bad.Add($"{p} 바위 밖 ({x:F2},{y:F2})"); break; }
                }
            }
            Assert.IsEmpty(bad, string.Join("\n", bad.Take(20)));

            //  같은 시드면 같고, 다른 시드면 랜턴 자리가 흔들린다.
            CollectionAssert.AreEqual(w, MineDressingLayout.ChimneyWalls(c, 7, z));
            Assert.IsFalse(w.SequenceEqual(MineDressingLayout.ChimneyWalls(c, 8, z)), "시드가 달라도 같다");
        }

        static float FinishFlagHalfWidth(MinePiece flag) => 0.5f * flag.ScaleX;

        static bool IsFinishLine(MinePiece p) =>
            p.Kind == MinePartKind.FinishFlag || (p.Kind == MinePartKind.FinishBanner && p.Layer == 2);

        [Test]
        public void 결승선_줄은_캐릭터_바로_뒤_통로를_가로지르고_깃발은_그_옆_체크무늬_띠는_통로_밖_절벽_끝은_출구_밖_레일_다리_아래()
        {
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var f = MineDressingLayout.Finish(c, z);
            var flag = f.Single(p => p.Kind == MinePartKind.FinishFlag);
            //  10-12 시안 B: 안개 막 뒤 깃발·막은 노을에 묻혀 배경으로 읽혔다 — 줄과 깃발을 캐릭터(반지름 0.45) 바로 뒤, 안개 막 앞으로.
            //  깃발은 줄 바로 오른쪽(결승선 너머) 바닥에, 줄과 겹치지 않게.
            Assert.Greater(flag.X - FinishFlagHalfWidth(flag), c.Length + MineDressingLayout.FinishBannerHalfWidth, "깃발이 줄과 겹친다");
            Assert.AreEqual(Floor(c, flag.X), flag.Y, Eps, "깃발 아래 끝 = 레일 다리 바닥선");
            Assert.AreEqual(MineDressingLayout.FinishStripeZ, flag.Z, Eps, "깃발이 줄과 다른 깊이");

            //  fix1(캡처 x 380: 게임 FinishLine 마커 기둥이 깃발을 통째로 가렸다 — 광산 굽기가 마커 렌더러를 끈다): 결승선 체크무늬 띠 둘.
            //  결승선 x를 가운데로 천장선 위·바닥선 아래에(판정 고체 위 — 열린 통로에 안 들어간다), 판정면 앞(데크·들보보다 앞)에 또렷하게,
            //  게임 카메라 화면 위·아래 끝(통로 중심 ± 8.37)까지 찬다.
            var banners = f.Where(p => p.Kind == MinePartKind.FinishBanner && p.Layer != 2).OrderBy(p => p.Y).ToList();
            Assert.AreEqual(2, banners.Count, "결승선 띠는 천장 쪽 하나, 바닥 쪽 하나");
            //  그리고 같은 폭 반투명 줄 하나가 바닥 띠부터 천장 띠까지 끊김 없이 — 캐릭터 몸(z ±0.45)보다 뒤, 안개 막(2.2)보다 앞.
            //  위·아래로 판정선 너머까지(원근으로 판정선 안쪽에 그려지는 틈 메우기).
            var stripe = f.Single(p => p.Kind == MinePartKind.FinishBanner && p.Layer == 2);
            Assert.AreEqual(c.Length, stripe.X + stripe.ScaleX / 2f, Eps, "줄 가운데가 결승선 x가 아니다");
            Assert.Greater(Front(stripe), 0.45f, "줄이 캐릭터 몸과 겹친다");
            Assert.Less(Front(stripe), MineDressingLayout.HazeZ, "줄이 안개 막 뒤(노을에 묻힌다)");
            Assert.LessOrEqual(stripe.Y, Floor(c, c.Length) - MineDressingLayout.FinishStripeOverlap + 0.05f, "줄 아래 끝이 바닥선 너머까지 안 간다");
            Assert.GreaterOrEqual(stripe.Y + stripe.ScaleY, Ceiling(c, c.Length) + MineDressingLayout.FinishStripeOverlap - 0.05f, "줄 위 끝이 천장선 너머까지 안 간다");
            Assert.AreEqual(0f, stripe.AngleDegrees, Eps);
            foreach (var b in banners)
            {
                Assert.AreEqual(c.Length, b.X + b.ScaleX / 2f, Eps, $"{b} 띠 가운데가 결승선 x가 아니다");
                Assert.Less(Front(b), -1.6f, $"{b} 띠가 들보·데크 뒤(가려진다)");
                Assert.IsNull(FirstDeepOpen(judge, b), $"{b} 띠가 열린 통로에");
                Assert.AreEqual(0f, b.AngleDegrees, Eps);
            }
            var (low, high) = (banners[0], banners[1]);
            Assert.AreEqual(Floor(c, c.Length), low.Y + low.ScaleY, 0.05f, "바닥 띠 윗변 = 바닥선");
            Assert.AreEqual(Ceiling(c, c.Length), high.Y, 0.05f, "천장 띠 아랫변 = 천장선");
            Assert.LessOrEqual(low.Y, c.CenterAt(c.Length) - CameraHalf, "바닥 띠가 화면 아래 끝까지 안 찬다");
            Assert.GreaterOrEqual(high.Y + high.ScaleY, c.CenterAt(c.Length) + CameraHalf, "천장 띠가 화면 위 끝까지 안 찬다");

            var cliff = f.Single(p => p.Kind == MinePartKind.CliffEdge);
            //  절벽 끝 = 출구 문틀 아래 산 바닥이 출구 밖에서 끝나는 곳. 원점 = 위 끝 왼쪽, 오른끝이 절벽 가장자리.
            Assert.AreEqual(z.ExitStart, cliff.X, Eps, "절벽은 출구 문틀부터");
            Assert.Greater(cliff.X + cliff.ScaleX, z.ExitEnd, "절벽 가장자리가 출구 밖에 없다");
            Assert.Less(cliff.X + cliff.ScaleX, c.Length, "절벽 가장자리가 결승선 뒤");
            Assert.GreaterOrEqual(cliff.ScaleY, 8f, "절벽이 화면 아래까지 안 찬다");
            //  레일 다리(비계 데크 = 바닥선) 아래, 판정선 밖. 비계 다리를 가리지 않게 판정면 뒤, 안개 막 앞.
            for (double x = cliff.X; x <= cliff.X + cliff.ScaleX; x += 0.25)
            {
                Assert.LessOrEqual(cliff.Y, judge.FloorAt(x) - 0.05, $"x {x:F2} 절벽 윗면이 바닥선 위");
            }
            Assert.Greater(Front(cliff), 0f, "절벽이 판정면 앞(비계 다리를 가린다)");
            Assert.Less(Front(cliff), 0.1f, "절벽 앞면이 판정면에서 떨어져 비계 다리 아래가 빈다");
            Assert.IsNull(FirstDeepOpen(judge, cliff));
            //  fix1(캡처 x 368·380: 절벽이 화면에 안 나왔다 — 바닥선 아래는 1.09 m만 보이고 데크·다리가 덮는다): 절벽을 안개 막 너머로 깊게 깔아
            //  노을빛 윗면 뒤끝이 게임 카메라(23 m, 통로 중심)에서 레일(바닥선 + 0.28) 위로 0.5 m 넘게 올라와 보인다 — 거기서 끝나는 곳이 절벽 가장자리.
            double edge = cliff.X + cliff.ScaleX;
            double cy = c.CenterAt((float)edge), seen = cy + (cliff.Y - cy) * 23.0 / (23.0 + Back(cliff));
            Assert.Greater(seen, Floor(c, (float)edge) + MineDressingLayout.RailTop + 0.5, $"절벽 윗면이 화면에서 레일 위로 안 올라온다(보이는 높이 {seen:F2})");
            //  그 가장자리가 출구 캡처(368)·노을 캡처(380) 화면 안에 든다(가로 반 폭 = 반 높이 × 1280/720).
            //  10-12 사람 판 레이아웃 변경: 출구가 367.5 → 400.7로 밀려 캡처 x를 출구 문틀 끝 기준으로 옮겼다(+0.5 · +12.5, 옛 368 · 380).
            foreach (float camX in new[] { z.ExitEnd + 0.5f, z.ExitEnd + 12.5f })
            {
                double sx = camX + (edge - camX) * 23.0 / (23.0 + Back(cliff));
                Assert.Less(Math.Abs(sx - camX), CameraHalf * 1280.0 / 720.0, $"카메라 x {camX}에서 절벽 가장자리가 화면 밖");
            }
            //  비계 다리가 절벽 위를 지나간다.
            var bays = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd).Where(p => p.Kind == MinePartKind.TrestleBay).ToList();
            Assert.IsTrue(bays.Any(p => p.X > cliff.X && p.X < cliff.X + cliff.ScaleX), "절벽 위에 레일 다리가 없다");
            Assert.IsTrue(bays.Any(p => p.X > c.Length), "결승선 뒤까지 레일 다리가 없다");
        }

        [Test]
        public void 롤러코스터_바닥은_완만한_칸만_비계고_가파른_칸은_바위_띠_다리는_통로_밖()
        {
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var t = Coaster(c);
            //  10-12 사람 판 레이아웃 변경: 151.25~206.25 → 171.05~226.05 — 구간 x에서 찾는다.
            //  10-13 동적 지형: 55 m → 30 m(물결 한 번).
            float coasterX = c.Sections.Single(sec => sec.Name == "레일 롤러코스터").X;
            Assert.AreEqual(coasterX, t.X0, 0.01f);
            Assert.AreEqual(coasterX + 30f, t.X1, 0.01f);
            var fc = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd);
            double Bottom(double x) => JudgedCenter(t, x) - t.Gap / 2.0;
            double Top(double x) => JudgedCenter(t, x) + t.Gap / 2.0;

            //  1차 규칙(완만 ≤ 15°·길이 ≥ 1.2 → 비계)이 물결의 바닥(굴 아래 가장자리)에 그대로: 마루·골마다 비계.
            var bays = fc.Where(p => p.Kind == MinePartKind.TrestleBay && p.X > t.X0 && p.X < t.X1).ToList();
            Assert.GreaterOrEqual(bays.Count, 6, "물결의 완만한 칸에 비계가 없다");
            double share = bays.Sum(p => p.Width) / (t.X1 - t.X0);
            Assert.Greater(share, 0.1, $"비계가 물결의 {share:P0}뿐 — 레일이 안 읽힌다");
            foreach (var b in bays)
            {
                Assert.LessOrEqual(Math.Abs(b.AngleDegrees), MineDressingLayout.TrestleMaxSlopeDegrees + 1e-3, $"{b} 가파른 칸에 비계");
                var (x0, y0, x1, y1) = Ends(b);
                Assert.AreEqual(Bottom(x0), y0, 0.05, $"{b} 데크 왼끝이 굴 바닥선에서 떨어졌다");
                Assert.AreEqual(Bottom(x1), y1, 0.05, $"{b} 데크 오른끝이 굴 바닥선에서 떨어졌다");
                Assert.IsNull(FirstDeepOpen(judge, b), "비계 다리가 열린 통로에");
            }

            //  굴 가장자리 = 판정선: 천장 쪽은 밝은 테 + 바위, 바닥 쪽은 테 또는 비계 데크 + 그 아래 바위·비계.
            var rims = fc.Where(p => p.Kind == MinePartKind.EdgeRim).ToList();
            var rock = fc.Where(p => p.Kind == MinePartKind.RockMass || p.Kind == MinePartKind.MergeSlope).ToList();
            for (double x = t.X0 + 0.05; x < t.X1 - 0.05; x += 0.05)
            {
                double top = Top(x), bottom = Bottom(x);
                Assert.IsTrue(rims.Any(p => p.Layer == 1 && Covers(p, x, top + 0.03)), $"롤러코스터 천장 ({x:F2},{top:F2})에 밝은 테가 없다");
                //  비계 칸 아래(다리)는 바위가 없다 — 데크가 판정선이다(± 0.05는 위에서 봤다).
                //  비계 칸과 바위 띠가 만나는 곳(테가 기운 직사각이라 경계 바로 밖 쐐기)은 0.02 봐준다.
                bool bay = bays.Any(p => x >= p.X - p.Width / 2f - 0.02f && x <= p.X + p.Width / 2f + 0.02f);
                Assert.IsTrue(bay || rims.Any(p => p.Layer == 0 && Covers(p, x, bottom - 0.03)), $"롤러코스터 바닥 ({x:F2},{bottom:F2})에 테도 비계도 없다");
                foreach (double d in new[] { 0.5, 3.0, 8.0 })
                {
                    Assert.IsTrue(rock.Any(p => p.Layer == 1 && Covers(p, x, top + d)), $"({x:F2},{top + d:F2}) 굴 위 판정 고체에 바위가 없다");
                    Assert.IsTrue(bay || rock.Any(p => p.Layer == 0 && Covers(p, x, bottom - d)), $"({x:F2},{bottom - d:F2}) 굴 아래 판정 고체에 바위가 없다");
                }
            }
            //  굴 안의 통로 바닥·천장선(−56.28·−41.72)은 판정 고체 속이다 — 거기에 밝은 테가 있으면 안 된다.
            foreach (var r in rims.Where(p => p.X > t.X0 + 0.3f && p.X < t.X1 - 0.3f))
            {
                var (mx, my) = World(r, r.ScaleX / 2, r.ScaleY / 2);
                double edge = r.Layer == 0 ? Bottom(mx) : Top(mx);
                Assert.Less(Math.Abs(my - edge), 0.25, $"{r} 밝은 테가 굴 가장자리가 아닌 곳(판정 고체 속)에");
            }
            //  굴 양끝 세로 면(통로 바닥·천장 ↔ 굴 가장자리)도 밝은 테.
            foreach (var (x, inward) in new[] { (t.X0, 1.0), (t.X1, -1.0) })
            {
                double fx = x + inward * 0.03;
                for (double y = judge.FloorAt(x) + 0.06; y < Bottom(x) - 0.06; y += 0.05)
                {
                    Assert.IsTrue(rims.Any(p => p.Layer == 0 && Covers(p, fx, y)), $"굴 끝 x {x} 바닥 쪽 세로 면 ({fx:F2},{y:F2})에 테가 없다");
                }
                for (double y = Top(x) + 0.06; y < judge.CeilAt(x) - 0.06; y += 0.05)
                {
                    Assert.IsTrue(rims.Any(p => p.Layer == 1 && Covers(p, fx, y)), $"굴 끝 x {x} 천장 쪽 세로 면 ({fx:F2},{y:F2})에 테가 없다");
                }
            }
            //  롤러코스터 x 폭은 통로 바닥·천장 그림이 맡지 않는다(굴 가장자리가 판정선이다).
            Assert.IsFalse(MineDressingLayout.PlainSpans(c, z, From, z.DressEnd).Any(sp => sp.to > t.X0 + 0.01 && sp.from < t.X1 - 0.01));
        }

        [Test]
        public void 롤러코스터_굴_바닥은_끊김_없는_레일이_판정선을_타고_받침은_통로_밖()
        {
            //  fix1(캡처 x 175: 물결 대부분이 비계 없는 바위 띠라 "레일"보다 "굴"로 읽혔다): 굴 아래 가장자리(판정선)를 따라 광차 레일이 끊김 없이,
            //  레일 윗면 = 판정선(± 0.05), 몸통·받침 기둥은 판정선 아래(판정 고체) — 통로 안에 다리·레일이 없다. 위쪽은 바위 + 밝은 테 그대로.
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var t = Coaster(c);
            var fc = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd);
            //  판정 바닥 = 굴 아래 가장자리, 통로 바닥선 아래로는 안 간다(굴 끝에서 가장자리가 바닥선 밖이면 바닥선).
            double Bottom(double x) => Math.Max(JudgedCenter(t, x) - t.Gap / 2.0, judge.FloorAt(x));
            var rails = fc.Where(p => p.Kind == MinePartKind.CoasterRail).ToList();
            var posts = fc.Where(p => p.Kind == MinePartKind.RailPost).ToList();
            Assert.Greater(rails.Count, 20, "굴 바닥 레일이 없다");
            Assert.IsTrue(rails.All(p => p.X > t.X0 && p.X < t.X1 && p.Layer == 0), "레일은 롤러코스터 굴 바닥에만");

            //  끊김 없이: 굴 x 폭 어디서나 판정선 바로 아래(0.1)를 레일이 덮는다(양끝 기운 레일은 아랫변 모서리가 굴 밖에 안 나가게 RailTop 안쪽까지 줄인다).
            for (double x = t.X0 + MineDressingLayout.RailTop; x < t.X1 - MineDressingLayout.RailTop; x += 0.05)
            {
                Assert.IsTrue(rails.Any(p => InFootprint(p, x, Bottom(x) - 0.1)), $"x {x:F2} 레일이 끊겼다");
            }
            foreach (var r in rails)
            {
                //  레일 윗변(국소 v = 0.28) 양끝·가운데가 판정선 위(± 0.05). 윗변은 늘이지 않는다(y 축척 1).
                Assert.AreEqual(1f, r.ScaleY, Eps);
                foreach (double u in new[] { -1.2 * r.ScaleX, 0, 1.2 * r.ScaleX })
                {
                    var (x, y) = World(r, u, MineDressingLayout.RailTop);
                    Assert.AreEqual(Bottom(x), y, 0.05, $"{r} 레일 윗면이 판정선에서 떨어졌다(x {x:F2})");
                }
                Assert.LessOrEqual(r.ScaleX, 1.6f, $"{r} 레일 칸을 늘였다(침목이 벌어진다)");
                Assert.IsNull(FirstDeepOpen(judge, r), "레일이 열린 통로에");
                //  굴 비계 데크(앞면 −1.3)보다 앞이라 비계 칸에서도 레일이 이어 보인다.
                Assert.Less(Front(r), -1.3f, $"{r} 레일이 비계 데크 뒤");
            }
            //  굴 안 비계 칸은 데크 위 레일(RailSpan, 판정선 + 0.28)을 얹지 않는다 — 레일 높이가 두 단이 된다.
            var bays = fc.Where(p => p.Kind == MinePartKind.TrestleBay).ToList();
            Assert.IsTrue(bays.Where(p => p.X > t.X0 && p.X < t.X1).All(p => MineDressingLayout.InCoaster(c, p.X)));
            Assert.IsTrue(bays.Where(p => p.X < t.X0 - 1f || p.X > t.X1 + 1f).All(p => !MineDressingLayout.InCoaster(c, p.X)), "굴 밖 비계가 레일을 잃는다");

            //  받침 기둥: 바위 띠 칸 밑에 성기게(비계 칸은 다리가 있다), 곧게 서서(−90°) 레일 아래로 바위에 박힌다 — 통로 안엔 없다.
            //  10-13 동적 지형: 롤러코스터 55 → 30 m라 기둥도 그만큼 준다(30 m에 8개) — 문턱 8 → 4.
            Assert.Greater(posts.Count, 4, "받침 기둥이 없다");
            foreach (var p in posts)
            {
                Assert.AreEqual(-90f, p.AngleDegrees, Eps, $"{p} 기둥이 기울었다");
                Assert.That(p.X, Is.InRange(t.X0, t.X1), $"{p} 기둥이 굴 밖");
                Assert.IsNull(FirstDeepOpen(judge, p), $"{p} 기둥이 열린 통로에");
                //  윗끝이 레일 아래(판정선 − 침목 아랫면)라 레일 위로 안 삐져나온다.
                foreach (double v in new[] { 0.0, 0.4 * p.ScaleY })
                {
                    var (x, y) = World(p, 0, v);
                    Assert.LessOrEqual(y, Bottom(x) - MineDressingLayout.RailTop + 0.03, $"{p} 기둥 윗끝이 레일 위로");
                }
                Assert.IsFalse(bays.Any(b => Math.Abs(p.X - b.X) < b.Width / 2f), $"{p} 비계 칸 밑에 기둥");
                Assert.Greater(Front(p), Front(rails[0]), "기둥이 레일 앞");
            }
            var xsPost = posts.Select(p => p.X).OrderBy(x => x).ToList();
            for (int i = 1; i < xsPost.Count; i++) { Assert.GreaterOrEqual(xsPost[i] - xsPost[i - 1], MineDressingLayout.RailPostSpacing - 0.01f, "기둥이 빽빽하다"); }

            //  위쪽(천장 쪽)은 그대로 바위 + 밝은 테 — 레일·기둥은 바닥 쪽에만.
            Assert.IsFalse(fc.Any(p => p.Layer == 1 && (p.Kind == MinePartKind.CoasterRail || p.Kind == MinePartKind.RailPost)));
        }

        [Test]
        public void 갱목_구간_끝에_맞닿은_바위_띠는_갱_쪽으로_두께_없는_바위_판이_옆면을_가린다()
        {
            //  fix1(캡처 x 270: x ≈ 283.3에 화면 전체 높이 어두운 세로선): 깊은 갱 끝(283.25)에서 천장 바위 띠(앞면 −0.26)가 갱 벽 바위(앞면 0.02)와 맞닿아
            //  카메라가 바위 띠 끝 옆면을 비스듬히 봤다. 갱 쪽으로 1 m 두께 없는 바위 판(옆면 없음)이 그 앞을 덮는다.
            var c = Course();
            var z = Zones3(c);
            var judge = new Judge(c);
            var fc = MineDressingLayout.FloorAndCeiling(c, z, From, z.DressEnd);
            var veneers = fc.Where(p => p.Kind == MinePartKind.RockVeneer).ToList();
            var band = fc.Where(p => p.Kind == MinePartKind.RockMass && Math.Abs(p.Z - MineDressingLayout.RockZ) < 1e-4f).ToList();
            var spans = MineDressingLayout.ShaftSpans(c, z);
            int checkedEnds = 0;
            foreach (var (sf, st) in spans)
            {
                foreach (var col in band.Where(p => Math.Abs(p.X - st) < 1e-3 || Math.Abs(p.X + p.ScaleX - sf) < 1e-3))
                {
                    bool atEnd = Math.Abs(col.X - st) < 1e-3;
                    double a = atEnd ? st - MineDressingLayout.VeneerCover : sf, x = a + MineDressingLayout.VeneerCover / 2;
                    //  바위 띠 기둥 높이 중 판 x 폭 통로 밖 부분 전부(그 폭 판정선 가장 바깥에서 0.1부터 기둥 끝까지)를 판이 덮는다.
                    double line = col.Layer == 1 ? double.NegativeInfinity : double.PositiveInfinity;
                    for (double q = a; q <= a + MineDressingLayout.VeneerCover + 1e-9; q += 0.05)
                    {
                        line = col.Layer == 1 ? Math.Max(line, judge.CeilAt(q)) : Math.Min(line, judge.FloorAt(q));
                    }
                    double y0 = col.Layer == 1 ? Math.Max(col.Y, line) + 0.1 : col.Y + 0.1;
                    double y1 = col.Layer == 1 ? col.Y + col.ScaleY - 0.1 : Math.Min(col.Y + col.ScaleY, line) - 0.1;
                    for (double y = y0; y <= y1; y += 0.5)
                    {
                        Assert.IsTrue(veneers.Any(v => v.Layer == col.Layer && InFootprint(v, x, y)), $"갱목 구간 [{sf}, {st}] 끝 ({x:F2},{y:F2})에 바위 판이 없다");
                    }
                    checkedEnds++;
                }
            }
            Assert.GreaterOrEqual(checkedEnds, 2, "맞닿은 바위 띠를 못 찾았다");
            //  깊은 갱 끝 천장 쪽(캡처의 세로선, 그땐 283.25)은 꼭 본다. 10-12 사람 판 레이아웃 변경으로 316.45 — 갱목 구간에서 가져온다.
            float deepEnd = spans[spans.Count - 1].to;
            Assert.IsTrue(veneers.Any(v => v.Layer == 1 && Math.Abs(v.X + v.ScaleX - deepEnd) < 0.01f), "깊은 갱 끝 천장 쪽 바위 판이 없다");
            foreach (var v in veneers)
            {
                Assert.IsNull(FirstDeepOpen(judge, v), $"{v} 바위 판이 열린 통로에");
                //  두께 없이(옆면이 새 선이 되지 않게), 바위 띠 앞면(−0.26)보다 조금 뒤, 갱 벽 바위(앞면 ≥ 0)보다 앞.
                Assert.Less(Back(v) - Front(v), 0.01f, $"{v} 판에 두께가 있다");
                Assert.That(Front(v), Is.InRange(-0.26f + 1e-4f, -0.2f), $"{v} 판 깊이");
                Assert.LessOrEqual(v.ScaleX, MineDressingLayout.VeneerCover + Eps);
            }
        }

        [Test]
        public void 노을_배경은_출구_문틀_끝_뒤에도_그_자리_통로_중심을_따른다()
        {
            var c = Course();
            var z = Zones3(c);
            float from = 300f, to = z.DressEnd;
            var bg = MineDressingLayout.Background(c, from, to, 7, Density);
            var near = bg.Where(p => (p.Kind == MinePartKind.CaveSilhouette || p.Kind == MinePartKind.CanyonSilhouette) && p.Layer == 0 && !p.Flipped).ToList();
            AssertCoversByWidth(near, from - MineDressingLayout.BackgroundMargin, to + MineDressingLayout.BackgroundMargin);
            for (float x = from; x <= to + 10f; x += 0.5f)
            {
                var strip = near.First(p => x >= p.X - p.Width / 2f - Eps && x <= p.X + p.Width / 2f + Eps);
                bool outside = x > z.ExitEnd + Eps;
                if (Math.Abs(x - z.ExitEnd) > 0.01f)
                {
                    Assert.AreEqual(outside ? MinePartKind.CanyonSilhouette : MinePartKind.CaveSilhouette, strip.Kind, $"x={x} 출구 문틀 끝 뒤는 협곡, 앞은 굴벽");
                }
                double a = strip.AngleDegrees * Math.PI / 180.0;
                double baseY = strip.Y + (x - strip.X) * Math.Tan(a);
                double offset = strip.Kind == MinePartKind.CaveSilhouette ? -8.2 : -11.6;
                Assert.AreEqual(c.CenterAt(x), baseY - offset, 0.05, $"x={x} 실루엣 기준선이 통로 중심을 안 따른다");
            }
            //  출구 밖 결승선 근처(결승선 6 m 앞, 옛 380) 기준선 = 통로 중심 −49 근처. 10-12 사람 판 레이아웃 변경으로 코스 끝에서 잰다.
            float nearFinish = c.Length - 6f;
            Assert.Greater(nearFinish, z.ExitEnd, "결승선 6 m 앞이 출구 밖이어야 한다");
            var at = near.First(p => nearFinish >= p.X - p.Width / 2f && nearFinish <= p.X + p.Width / 2f);
            Assert.AreEqual(MinePartKind.CanyonSilhouette, at.Kind);
            Assert.AreEqual(-49f - 11.6f, at.Y + (nearFinish - at.X) * Math.Tan(at.AngleDegrees * Math.PI / 180.0), 0.05);
            //  천장 쪽(굴벽) 실루엣·가운데 층(틀·발판·사다리·랜턴)은 굴(EntranceEnd ~ ExitEnd)에만.
            Assert.IsTrue(bg.Where(p => p.Flipped).All(p => p.X - p.Width / 2f < z.ExitEnd + Eps));
            var mid = bg.Where(p => p.Kind == MinePartKind.BgFrame || p.Kind == MinePartKind.Walkway || p.Kind == MinePartKind.Ladder
                                    || (p.Kind == MinePartKind.Lantern && Math.Abs(p.Z - MineDressingLayout.LanternZ) < Eps)
                                    || (p.Kind == MinePartKind.MineCart && Math.Abs(p.Z - MineDressingLayout.MidZ) < Eps)).ToList();
            Assert.IsTrue(mid.Any(p => p.X > 330f), "굴 끝 쪽 가운데 층이 없다");
            var outsideMid = mid.Where(p => p.X > z.ExitEnd + Eps).Select(p => $"{p.Kind}@{p.X:F2}").ToList();
            Assert.IsEmpty(outsideMid, $"가운데 층이 출구 문틀 끝({z.ExitEnd:F2}) 뒤(노을 바깥)에");
            //  먼 비계(협곡 다리)는 노을 바깥에도.
            Assert.IsTrue(bg.Any(p => p.Kind == MinePartKind.BgTrestleBay && p.X > z.ExitEnd + 5f));
        }

        // ══ 펼치기 3차(10-13): 움직이는 지형 — 그림 = 판정 ± 0.01 ══
        //  부품 크기(블렌더 SPREAD3_PARTS, mine_parts.py 출력으로 확인): LiftPlank 1.95 × 1(아래 끝 가운데)·LiftLash ±0.985·LiftCap 0.4(윗면 원점), HangingCart 1.6 × 1.2(가운데),
        //  FallingRock 1.2(가운데), CollapsedProp 0.6 × 0.8(왼끝 붙은 쪽), GateCap 두께 0.4(윗면 원점).
        const float PartTol = 0.01f;

        [Test]
        public void 받침목_그림은_받침목_사각형_그대로()
        {
            var c = Course();
            var pieces = MineDressingLayout.CollapsedProps(c);
            Assert.AreEqual(c.Props.Count, pieces.Count);
            Assert.Greater(pieces.Count, 0);
            for (int i = 0; i < pieces.Count; i++)
            {
                MineRect r = c.Props[i];
                MinePiece p = pieces[i];
                Assert.AreEqual(MinePartKind.CollapsedProp, p.Kind);
                Assert.AreEqual(r.X1 - r.X0, p.ScaleX * 0.6f, PartTol, "폭");
                Assert.AreEqual(r.Y1 - r.Y0, p.ScaleY * 0.8f, PartTol, "높이");
                //  원점 + 회전(뒤집으면 180°)으로 펼친 그림 사각형이 받침목 사각형과 겹친다.
                float x0 = p.Flipped ? p.X - p.ScaleX * 0.6f : p.X, x1 = p.Flipped ? p.X : p.X + p.ScaleX * 0.6f;
                float y0 = p.Flipped ? p.Y - p.ScaleY * 0.8f : p.Y, y1 = p.Flipped ? p.Y : p.Y + p.ScaleY * 0.8f;
                Assert.AreEqual(r.X0, x0, PartTol, $"{i} 왼끝"); Assert.AreEqual(r.X1, x1, PartTol, $"{i} 오른끝");
                Assert.AreEqual(r.Y0, y0, PartTol, $"{i} 아래"); Assert.AreEqual(r.Y1, y1, PartTol, $"{i} 위");
                //  붙은 쪽 = 굴 벽: 뒤집힌 것은 굴 위 가장자리에, 아닌 것은 아래 가장자리에 붙는다(MineCourseRule: 짝수 = 위).
                MineTube t = c.Tubes.First(q => (r.X0 + r.X1) / 2f > q.X0 && (r.X0 + r.X1) / 2f < q.X1);
                float xm = (r.X0 + r.X1) / 2f;
                Assert.AreEqual(i % 2 == 0, p.Flipped, $"{i} 위에서 튀어나온 받침목만 뒤집는다");
                Assert.AreEqual(p.Flipped ? t.Center(xm) + t.Gap / 2f : t.Center(xm) - t.Gap / 2f, p.Y, PartTol, $"{i} 원점이 굴 벽에 안 붙었다");
            }
        }

        [Test]
        public void 매단_광차_그림은_광차_상자와_같은_자리()
        {
            var c = Course();
            Assert.Greater(c.Pendulums.Count, 0);
            foreach (MinePendulum pen in c.Pendulums)
            {
                //  광차 그림은 Upright(줄 끝 = 광차 가운데, 진자 루트 기준 (0, −Length))의 자식 — 그래서 (0, 0).
                MinePiece cart = MineDressingLayout.HangingCartLocal(pen);
                Assert.AreEqual(MinePartKind.HangingCart, cart.Kind);
                Assert.AreEqual(0f, cart.X, 1e-4f);
                Assert.AreEqual(0f, cart.Y, 1e-4f, "Upright(줄 끝) 자식 기준이라 제자리");
                Assert.AreEqual(pen.CartWidth / 1.6f, cart.ScaleX, 1e-4f, "폭 = 광차 상자 폭");
                Assert.AreEqual(pen.CartHeight / 1.2f, cart.ScaleY, 1e-4f, "높이 = 광차 상자 높이");
                //  쉬는 자세의 그림 사각형(월드) = CartBox(축 x ± 폭/2, 축 − 줄 길이 ± 높이/2).
                float cx = pen.PivotX + cart.X, cy = pen.PivotY - pen.Length + cart.Y;
                Assert.AreEqual(pen.PivotX - pen.CartWidth / 2f, cx - 0.8f * cart.ScaleX, PartTol);
                Assert.AreEqual(pen.PivotX + pen.CartWidth / 2f, cx + 0.8f * cart.ScaleX, PartTol);
                Assert.AreEqual(pen.PivotY - pen.Length - pen.CartHeight / 2f, cy - 0.6f * cart.ScaleY, PartTol);
                Assert.AreEqual(pen.PivotY - pen.Length + pen.CartHeight / 2f, cy + 0.6f * cart.ScaleY, PartTol);
                Assert.Less(cart.Z + 0.6f, 0f, "광차는 판정면 앞");

                //  줄은 진자 루트(축)의 자식 — 축에서 광차 윗면까지, 판정면 뒤(메시 z 0.2~0.3).
                MinePiece chain = MineDressingLayout.CartChainLocal(pen);
                Assert.AreEqual(MinePartKind.CartChain, chain.Kind);
                Assert.AreEqual(0f, chain.X, 1e-4f);
                Assert.AreEqual(0f, chain.Y, 1e-4f, "축이 원점");
                Assert.Greater(chain.Z + 0.2f, 0f, "사슬은 판정면 뒤");
                Assert.AreEqual(-pen.Length + pen.CartHeight / 2f, chain.Y - chain.ScaleY, PartTol, "줄 아래 끝 = 광차 윗면");
            }
        }

        [Test]
        public void 떨어지는_바위_그림은_바위_상자와_같은_크기()
        {
            var c = Course();
            Assert.Greater(c.Rockfalls.Count, 0);
            foreach (MineRockfall r in c.Rockfalls)
            {
                MinePiece rock = MineDressingLayout.FallingRockLocal(r);
                Assert.AreEqual(MinePartKind.FallingRock, rock.Kind);
                Assert.AreEqual(0f, rock.X, 1e-4f);
                Assert.AreEqual(0f, rock.Y, 1e-4f, "Rock(바위 가운데)의 자식");
                Assert.AreEqual(r.Size / 1.2f, rock.ScaleX, 1e-4f);
                Assert.AreEqual(r.Size / 1.2f, rock.ScaleY, 1e-4f);
                Assert.Less(rock.Z + 0.6f, 0f, "바위는 판정면 앞");
            }
        }

        [Test]
        public void 승강기_기둥은_파이프_사각형_그대로_테는_틈_쪽_끝()
        {
            var c = Course();
            Assert.Greater(c.Lifts.Count, 0);
            float cap = MineDressingLayout.CapThickness;
            foreach (MineLift l in c.Lifts)
            {
                MineGate g = l.Gate;
                float half = g.Width / 2f;
                //  FlappyMineMoverBake와 같은 셈을 따로: 관문 폭 양끝·가운데의 가장 낮은 바닥 − 묻기 1 − 진폭 ~ 틈 아래, 틈 위 ~ 가장 높은 천장 + 1 + 진폭.
                float floor = new[] { g.X - half, g.X, g.X + half }.Min(x => Floor(c, x));
                float ceiling = new[] { g.X - half, g.X, g.X + half }.Max(x => Ceiling(c, x));
                var (rf, rc) = MineDressingLayout.LiftReach(c, l);
                Assert.AreEqual(floor, rf, 1e-4f); Assert.AreEqual(ceiling, rc, 1e-4f);
                float a = g.GapCenter - g.Gap / 2f, b = g.GapCenter + g.Gap / 2f;
                var pipes = new[] { (lo: floor - 1f - l.Amplitude, hi: a), (lo: b, hi: ceiling + 1f + l.Amplitude) };

                var pieces = MineDressingLayout.LiftPillarLocal(l, rf, rc);
                Assert.IsFalse(pieces.Any(p => p.Kind == MinePartKind.GateCap), "옛 쇠테(GateCap)는 쓰지 않는다 — 크림색 LiftCap");
                Assert.IsTrue(pieces.All(p => Math.Abs(p.Z - pieces[0].Z) < 1e-4f && p.Z + 0.65f < 0f), "관문 판자처럼 판정면 앞 한 깊이");
                //  폭: 모든 조각이 관문 폭(± 0.975 × 축척 — 감기·쇠띠는 메시가 ±0.985라 판자 옆면과 한 평면이 아니고, ± 0.01 안).
                foreach (MinePiece p in pieces)
                {
                    Assert.AreEqual(-half, p.X - 0.975f * p.ScaleX, PartTol, $"{p.Kind} 왼끝");
                    Assert.AreEqual(half, p.X + 0.975f * p.ScaleX, PartTol, $"{p.Kind} 오른끝");
                }

                var caps = pieces.Where(p => p.Kind == MinePartKind.LiftCap).OrderBy(p => p.Y).ToList();
                Assert.AreEqual(2, caps.Count);
                Assert.AreEqual(a, caps[0].Y, PartTol); Assert.IsFalse(caps[0].Flipped, "아래 파이프 테는 윗면이 틈 아래 끝");
                Assert.AreEqual(b, caps[1].Y, PartTol); Assert.IsTrue(caps[1].Flipped, "위 파이프 테는 뒤집어 아랫면이 틈 위 끝");

                var planks = pieces.Where(p => p.Kind == MinePartKind.LiftPlank).OrderBy(p => p.Y).ToList();
                var lashes = pieces.Where(p => p.Kind == MinePartKind.LiftLash).ToList();
                for (int i = 0; i < 2; i++)
                {
                    var (lo, hi) = pipes[i];
                    var mine = planks.Where(p => p.Y >= lo - Eps && p.Y < hi).ToList();
                    Assert.Greater(mine.Count, 1, $"{i} 파이프에 판자 칸이 없다");
                    //  묻힌 끝 = 파이프 끝, 틈 쪽 끝 = 테 밑면(테가 덮는다 — 테 윗면과 한 평면이 아니다), 칸 사이 빈틈 없음.
                    float first = mine[0].Y, last = mine[mine.Count - 1].Y + mine[mine.Count - 1].ScaleY;
                    Assert.AreEqual(i == 0 ? lo : b + cap, first, PartTol, $"{i} 판자 아래 끝");
                    Assert.AreEqual(i == 0 ? a - cap : hi, last, PartTol, $"{i} 판자 위 끝");
                    for (int k = 0; k + 1 < mine.Count; k++)
                    {
                        Assert.AreEqual(mine[k].Y + mine[k].ScaleY, mine[k + 1].Y, 1e-3f, $"{i} 칸 {k} 사이가 벌어졌다");
                        Assert.LessOrEqual(mine[k].ScaleY, 1f + Eps);
                    }
                    //  그림 사각형 = 파이프 사각형: 판자 묻힌 끝 ~ 테 바깥 면.
                    float artLo = i == 0 ? first : caps[1].Y, artHi = i == 0 ? caps[0].Y : last;
                    Assert.AreEqual(lo, artLo, PartTol, $"{i} 그림 아래 끝 = 파이프 아래 끝");
                    Assert.AreEqual(hi, artHi, PartTol, $"{i} 그림 위 끝 = 파이프 위 끝");

                    //  밧줄 감기: 틈 쪽 끝에서 2 m마다, 파이프 안. 승강기 파이프는 늘 2 m보다 길어 하나 이상.
                    float gapEnd = i == 0 ? a : b, dir = i == 0 ? -1f : 1f;
                    var ys = lashes.Where(p => p.Y > lo && p.Y < hi).Select(p => p.Y).OrderBy(y => dir * y).ToList();
                    Assert.Greater(ys.Count, 0, $"{i} 밧줄 감기가 없다");
                    for (int k = 0; k < ys.Count; k++)
                    {
                        Assert.AreEqual(gapEnd + dir * 2f * (k + 1), ys[k], 1e-3f, $"{i} 감기 {k} 자리");
                    }
                }
                Assert.AreEqual(planks.Count, pieces.Count(p => p.Kind == MinePartKind.LiftPlank));
                Assert.AreEqual(pieces.Count, planks.Count + lashes.Count + caps.Count, "판자·감기·테 말고 다른 조각은 없다");
            }
        }

        [Test]
        public void 승강기_안내_틀은_판정면_뒤에서_오르내리는_폭을_덮는다()
        {
            var c = Course();
            var frames = MineDressingLayout.LiftFrames(c);
            Assert.AreEqual(c.Lifts.Count, frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                MineLift l = c.Lifts[i];
                MinePiece f = frames[i];
                Assert.AreEqual(MinePartKind.LiftFrame, f.Kind);
                Assert.AreEqual(l.Gate.X, f.X, 1e-4f);
                Assert.Greater(f.Z + 0.3f, 0f, "틀 앞면(메시 z 0.3)이 판정면 뒤");
                Assert.AreEqual(Floor(c, l.Gate.X) - l.Amplitude, f.Y, PartTol);
                Assert.AreEqual(Ceiling(c, l.Gate.X) + l.Amplitude, f.Y + f.ScaleY, PartTol);
            }
        }

        [Test]
        public void 천장_금은_낙석_줄마다_같은_모양으로_천장_바위_속에()
        {
            var c = Course();
            var cracks = MineDressingLayout.CeilingCracks(c);
            Assert.AreEqual(c.Rockfalls.Count, cracks.Count);
            for (int i = 0; i < cracks.Count; i++)
            {
                MineRockfall r = c.Rockfalls[i];
                MinePiece k = cracks[i];
                Assert.AreEqual(MinePartKind.CeilingCrack, k.Kind);
                Assert.AreEqual(r.X, k.X, 1e-4f);
                Assert.AreEqual(Ceiling(c, r.X), k.Y, PartTol, "원점 = 천장선");
                //  메시 y 0.13~0.6(천장 밝은 테 위) — 통로(천장선 아래)에 안 들어가고, 앞면 z −0.3은 판정면 앞 −0.3 이내.
                Assert.Greater(k.Y + 0.13f * k.ScaleY, Ceiling(c, r.X - 1f) + 0.12f - PartTol);
                Assert.GreaterOrEqual(k.Z - 0.3f, -0.3f - 1e-4f);
                Assert.AreEqual(cracks[0].ScaleX, k.ScaleX, 1e-6f, "모든 줄 같은 크기 — 박자를 알리지 않는다");
                Assert.AreEqual(cracks[0].ScaleY, k.ScaleY, 1e-6f);
                Assert.IsFalse(k.Flipped);
            }
        }

        [Test]
        public void 레일_굴에만_바닥_레일이_깔린다()
        {
            var c = Course();
            MineFork rail = c.Forks.Single(f => f.Shape == ForkShape.Rail);
            var rails = MineDressingLayout.ExpertRails(c);
            Assert.Greater(rails.Count, 0);
            Assert.IsTrue(rails.All(p => p.Kind == MinePartKind.CoasterRail && p.X > rail.X0 && p.X < rail.X1));
            //  레일 윗면 가운데 = 굴 바닥 가장자리(롤러코스터 레일과 같은 규칙, 합친 현 오차 0.04 안).
            MineTube t = c.Tubes.Where(q => q.X0 >= rail.X0 - 1e-3f && q.X1 <= rail.X1 + 1e-3f).OrderBy(q => q.X0).First();
            foreach (MinePiece p in rails)
            {
                double a = p.AngleDegrees * Math.PI / 180.0;
                double tx = p.X - MineDressingLayout.RailTop * Math.Sin(a), ty = p.Y + MineDressingLayout.RailTop * Math.Cos(a);
                Assert.That(tx, Is.InRange(t.X0, t.X1), "레일 굴 x 폭 안");
                Assert.AreEqual(t.Center((float)tx) - t.Gap / 2f, ty, 0.05, $"x={tx:F2} 레일 윗면이 굴 바닥선에서 벗어났다");
            }
            //  굴 처음부터 끝까지 이어진다(칸 폭 2.4 근처 — 비지 않는다).
            Assert.Less(rails.Min(p => p.X - p.Width / 2f), t.X0 + 0.1f);
            Assert.Greater(rails.Max(p => p.X + p.Width / 2f), t.X1 - 0.1f);
        }

        sealed class FloatComparer : System.Collections.IComparer
        {
            readonly float eps;
            public FloatComparer(float eps) { this.eps = eps; }
            public int Compare(object a, object b) => Math.Abs((float)a - (float)b) <= eps ? 0 : ((float)a).CompareTo((float)b);
        }
    }
}
