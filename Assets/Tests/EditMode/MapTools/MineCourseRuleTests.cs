using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 광산 코스 배치. 기준값은 전부 웹 프로토타입(v31 "0 · 광산 3막", 슬라럼 자리 = 계단 내리막)에서 뽑은 것이다 — 같은 난수·같은 소비 순서로
    /// 옮겼는지가 이 테스트의 요점이다. 숫자 하나가 어긋나면 "프로토타입에서 해 본 그 코스"가 아니다.
    /// </summary>
    public class MineCourseRuleTests
    {
        static readonly MinePhysics P = new MinePhysics(4.5f, 10.125f, 33.75f, 11.25f, 0.02f);

        //  MasterData FlappyConfig의 대시 값(DashMult 2.2 · DashDuration 0.48) — 부스트 거리를 게임 곡선으로 셀 때 쓴다.
        const float DashMult = 2.2f;
        const float DashDuration = 0.48f;
        //  새 코끝까지의 반 길이(m) — 발밑 x에서 이만큼 앞이 칸막이에 먼저 닿는다.
        const float NoseHalfLength = 0.64f;

        static MineCourse Layout() => MineCourseRule.Layout(P);

        //  굴 조각이 아닌 관문 — 프로토타입 덤프의 gates와 같은 목록.
        static List<MineGate> Gates(MineCourse c) => c.Gates.ToList();

        [Test]
        public void 길이는_프로토타입과_같다() => Assert.AreEqual(386.15f, Layout().Length, 0.01f);

        [Test]
        public void 구간_순서와_시작점()
        {
            //  3막: 1막 하강(갱 → 계단 내리막 → 갈림길⬇) · 2막 갱 바닥(낮은 천장 → 롤러코스터 → 급반전 → 갈림길⬆ → 깊은 갱) · 3막 탈출(긴 통로 → 굴뚝 → 갈림길⬆).
            var expect = new (string name, float x)[]
            {
                ("수직 갱 낙하", 24.8f), ("계단 내리막", 45.65f), ("고수 갈림길 ⬇굴", 83.45f),
                ("낮은 천장", 118.4f), ("레일 롤러코스터", 151.25f), ("급반전", 215.1f), ("고수 갈림길 ⬆굴", 242.1f), ("수직 갱 낙하", 266.25f),
                ("긴 통로", 294.5f), ("굴뚝 오르기", 315.35f), ("고수 갈림길 ⬆굴", 341.8f),
            };
            var secs = Layout().Sections;
            Assert.AreEqual(expect.Length, secs.Count);
            for (int i = 0; i < expect.Length; i++)
            {
                Assert.AreEqual(expect[i].name, secs[i].Name, $"구간 {i}");
                Assert.AreEqual(expect[i].x, secs[i].X, 0.01f, $"구간 {i} 시작 x");
            }
        }

        [Test]
        public void 첫_관문들이_같은_난수로_선다()
        {
            var gates = Gates(Layout());
            Assert.AreEqual(35, gates.Count);
            //  출발 관문 2 → 갱 바닥(−22)의 사이 관문 1 → 계단 내리막 첫 관문(−22 − 4.5).
            var expect = new (float x, float c, float w)[]
            {
                (14.975f, -1.364f, 1.95f), (20.375f, 1.699f, 1.95f), (41.225f, -22.214f, 1.95f), (46.625f, -26.5f, 1.95f),
            };
            for (int i = 0; i < expect.Length; i++)
            {
                Assert.AreEqual(expect[i].x, gates[i].X, 0.002f, $"관문 {i} x");
                Assert.AreEqual(expect[i].c, gates[i].GapCenter, 0.002f, $"관문 {i} 틈 중심");
                Assert.AreEqual(expect[i].w, gates[i].Width, 0.002f, $"관문 {i} 두께");
                Assert.AreEqual(3.75f, gates[i].Gap, 1e-4f, $"관문 {i} 틈");
            }
            //  관문 27 = 긴 통로(12 m, 틈 3.1).
            Assert.AreEqual(300.5f, gates[27].X, 0.002f);
            Assert.AreEqual(12f, gates[27].Width, 1e-4f);
            Assert.AreEqual(3.1f, gates[27].Gap, 1e-4f);
            Assert.AreEqual(-76.895f, gates[27].GapCenter, 0.002f);
            var last = gates[gates.Count - 1];
            Assert.AreEqual(377.725f, last.X, 0.002f);
            Assert.AreEqual(-51.607f, last.GapCenter, 0.002f);
        }

        [Test]
        public void 난이도_값이_프로토타입과_같다()
        {
            var c = Layout();
            var gates = Gates(c);

            //  계단 내리막 6개(관문 3~8): 갱 바닥(−22)에서 관문마다 4.5 m씩 내려 −26.5 … −49. 틈은 보통(3.75).
            //  통로도 계단을 따라 내려간다 — 그 관문 x의 통로 중심 = 틈 중심.
            for (int i = 0; i < 6; i++)
            {
                float x = 46.625f + 5.4f * i, step = -22f - 4.5f * (i + 1);
                Assert.AreEqual(x, gates[3 + i].X, 0.002f, $"계단 {i} x");
                Assert.AreEqual(step, gates[3 + i].GapCenter, 1e-4f, $"계단 {i} 틈 중심");
                Assert.AreEqual(3.75f, gates[3 + i].Gap, 1e-4f, $"계단 {i} 틈");
                Assert.AreEqual(step, c.CenterAt(x), 1e-3f, $"계단 {i} 통로 중심");
            }
            //  급반전(관문 17~20): −49에서 +5.5 / −5.5 번갈아.
            var flip = new[] { -43.5f, -49f, -43.5f, -49f };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(216.075f + 5.4f * i, gates[17 + i].X, 0.002f, $"급반전 {i} x");
                Assert.AreEqual(flip[i], gates[17 + i].GapCenter, 1e-4f, $"급반전 {i} 틈 중심");
            }
            //  갈림길 보통 관문 — 틈 3.3, 폭 3 m 난수 띠(첫 갈림길 ⬇굴이라 보통 길은 위, 관문 10~12).
            var fork = new[] { -44.458f, -45.601f, -44.586f };
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(fork[i], gates[10 + i].GapCenter, 0.002f, $"갈림길 관문 {i}");
                Assert.AreEqual(3.3f, gates[10 + i].Gap, 1e-4f, $"갈림길 관문 {i} 틈");
            }

            //  물결 터널은 빠졌다 — 굴은 롤러코스터 1 + 갈림길 굴 3.
            Assert.AreEqual(4, c.Tubes.Count);
            MineTube coaster = c.Tubes.Single(t => Math.Abs(t.X0 - 151.25f) < 0.01f);
            Assert.AreEqual(4.8f, coaster.Gap, 1e-5f);
            Assert.AreEqual(151.25f + 55f, coaster.X1, 0.01f, "롤러코스터 55 m");

            //  낮은 천장(118.4 ~ 142.4, 24 m): 반 높이 2.2, 중심선 물결 진폭 3.5 · 주기 17(¼ 주기에서 꼭대기). 계단 끝 바닥 −49.
            Assert.AreEqual(2.2f, c.HalfAt(118.4f + 10f), 1e-4f);
            Assert.AreEqual(-49f + 3.5f, c.CenterAt(118.4f + 17f / 4f), 0.01f);
            Assert.AreEqual(MineCourse.BaseHalf, c.HalfAt(142.4f + 3.5f), 1e-4f, "24 m 뒤 3 m 전이 밖은 넓은 통로");
        }

        [Test]
        public void 갈림길은_아래_위_위이고_패드는_굴_뒤에()
        {
            var c = Layout();
            var expectForks = new (float x0, float x1, float b, bool up)[] { (83.45f, 104.15f, -49f, false), (242.1f, 262.8f, -49f, true), (341.8f, 362.5f, -49f, true) };
            var expectPads = new (float x0, float y0, float y1)[] { (92.15f, -55.0f, -52.2f), (250.8f, -47.8f, -45.0f), (350.5f, -47.8f, -45.0f) };
            Assert.AreEqual(3, c.Forks.Count);
            Assert.AreEqual(3, c.Pads.Count);
            Assert.AreEqual(3, c.Walls.Count);
            for (int i = 0; i < 3; i++)
            {
                MineFork f = c.Forks[i];
                Assert.AreEqual(expectForks[i].x0, f.X0, 0.01f, $"갈림길 {i} X0");
                Assert.AreEqual(expectForks[i].x1, f.X1, 0.01f, $"갈림길 {i} X1");
                Assert.AreEqual(expectForks[i].b, f.Base, 0.01f, $"갈림길 {i} Base");
                Assert.AreEqual(expectForks[i].up, f.TunnelUp, $"갈림길 {i} 굴 방향");

                MinePad pad = c.Pads[i];
                Assert.AreEqual(expectPads[i].x0, pad.Rect.X0, 0.01f, $"패드 {i} x0");
                Assert.AreEqual(expectPads[i].y0, pad.Rect.Y0, 0.01f, $"패드 {i} y0");
                Assert.AreEqual(expectPads[i].y1, pad.Rect.Y1, 0.01f, $"패드 {i} y1");
                Assert.AreEqual(1.1f, pad.Duration, 1e-5f);

                //  패드는 굴을 빠져나온 뒤에, 부스트 직선(게임과 같은 대시 곡선으로 적분) + 코끝은 칸막이 안에서 끝난다.
                MineTube tube = c.Tubes.Single(t => Math.Abs(t.X0 - (f.X0 + 3f)) < 0.01f);
                Assert.Greater(pad.Rect.X0, tube.X1, $"패드 {i}가 굴 뒤");
                float boost = LOP.FlappyDashCurve.Distance(P.Forward, pad.Duration, DashDuration, DashMult, P.Tick);
                Assert.Less(pad.Rect.X0 + boost + NoseHalfLength, f.X1, $"패드 {i} 부스트가 칸막이 안");
            }
        }

        [Test]
        public void 통로_높이()
        {
            var c = Layout();
            var expect = new (float x, float center, float half)[]
            {
                (0f, 0f, 7.28f), (100f, -49f, 7.28f), (130f, -52.189f, 2.2f), (200f, -49f, 7.28f), (300f, -77f, 7.28f), (380f, -49f, 7.28f),
            };
            foreach (var e in expect)
            {
                Assert.AreEqual(e.center, c.CenterAt(e.x), 0.01f, $"x={e.x} 중심");
                Assert.AreEqual(e.half, c.HalfAt(e.x), 0.01f, $"x={e.x} 반 높이");
            }

            //  수직 갱 둘: 좁은 구간 [a−2, b+4], a = 24.8(8 m에 22 m) · 266.25(10 m에 28 m). 굴뚝: [a−2, b+2], a = 315.35(21 m에 28 m).
            Assert.AreEqual(2.8f, c.HalfAt(24.8f + 4f), 0.01f);
            Assert.AreEqual(2.8f, c.HalfAt(266.25f + 4f), 0.01f);
            Assert.AreEqual(1.4f, c.HalfAt(315.35f + 8f), 0.01f);
            Assert.AreEqual(-22f, c.CenterAt(24.8f + 8f), 0.01f, "첫 갱 바닥");
            Assert.AreEqual(-77f, c.CenterAt(266.25f + 10f), 0.01f, "깊은 갱 바닥");
            Assert.AreEqual(-49f, c.CenterAt(315.35f + 21f), 0.01f, "굴뚝 꼭대기");
            //  경계(좁은 구간 시작) 1.5 m 밖 = 3 m 전이의 한가운데.
            Assert.AreEqual((MineCourse.BaseHalf + 2.8f) / 2f, c.HalfAt(24.8f - 2f - 1.5f), 0.01f);
            Assert.AreEqual((MineCourse.BaseHalf + 1.4f) / 2f, c.HalfAt(315.35f - 2f - 1.5f), 0.01f);
        }

        [Test]
        public void 평평한_앞부분은_첫_갱_전이_앞에서_끝난다()
        {
            var c = Layout();
            //  첫 갱 a = 24.8 → 좁은 구간 a − 2 → 전이 3 m 앞 = 19.8. 그 앞은 중심 0 · 반 높이 BaseHalf.
            Assert.AreEqual(19.8f, c.FlatEnd(), 0.01f);
            for (float x = -20f; x <= 19.8f; x += 0.5f)
            {
                Assert.AreEqual(0f, c.CenterAt(x), 1e-4f, $"x={x} 중심");
                Assert.AreEqual(MineCourse.BaseHalf, c.HalfAt(x), 1e-4f, $"x={x} 반 높이");
            }
            Assert.Less(c.HalfAt(19.8f + 0.5f), MineCourse.BaseHalf, "바로 뒤는 좁아지기 시작");
        }

        //  FlatEnd()는 세 갈래(Narrows·Lows·꺾은선) 중 가장 앞 값을 쓴다. 실제 코스는 늘 Narrows(첫 수직 갱)가
        //  가장 앞이라 위 테스트는 그 갈래만 지킨다. 나머지 둘은 합성 코스로 따로 지킨다 — Points/Narrows/Lows가
        //  internal이고 이 테스트 어셈블리에 InternalsVisibleTo가 없어서(확인함, LOP.MapTools.asmdef에 없다)
        //  프로덕션 코드를 건드리지 않고 리플렉션으로만 채운다.

        [Test]
        public void FlatEnd은_Lows_갈래가_가장_앞이면_그걸_쓴다()
        {
            //  Narrows·꺾은선이 없으니 이 갈래(Lows)를 지우면 FlatEnd가 Length(100)까지 튄다 — 사보타주로 확인했다.
            var c = Synthetic(
                points: new List<(double, double)> { (0, 0), (100, 0) },
                narrows: new List<(double, double, double)>(),
                lows: new List<(double, double)> { (5, 20) },
                length: 100f);
            Assert.AreEqual(5f - MineCourse.EdgeBlend, c.FlatEnd(), 1e-4f);
        }

        [Test]
        public void FlatEnd은_꺾은선_갈래가_가장_앞이면_그걸_쓴다()
        {
            //  Narrows·Lows가 없으니 이 갈래(꺾은선 루프)를 지우면 FlatEnd가 Length(100)까지 튄다 — 사보타주로 확인했다.
            var c = Synthetic(
                points: new List<(double, double)> { (0, 0), (10, 0), (20, 5) },
                narrows: new List<(double, double, double)>(),
                lows: new List<(double, double)>(),
                length: 100f);
            Assert.AreEqual(10f, c.FlatEnd(), 1e-4f);
        }

        static MineCourse Synthetic(List<(double, double)> points, List<(double, double, double)> narrows, List<(double, double)> lows, float length)
        {
            var c = new MineCourse();
            SetInternalField(c, "Points", points);
            SetInternalField(c, "Narrows", narrows);
            SetInternalField(c, "Lows", lows);
            c.Length = length;
            return c;
        }

        static void SetInternalField(MineCourse c, string name, object value)
        {
            var field = typeof(MineCourse).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"MineCourse.{name} 필드를 못 찾았다 — 리플렉션 경로가 깨졌다.");
            field.SetValue(c, value);
        }

        [Test]
        public void 두번_점프_굴은_커널로_날린_궤적과_틱마다_같다()
        {
            var c = Layout();
            //  첫 위 굴 = 두 번째 갈림길(242.1, 바닥 −49) — 굴 칸 중심 −49 + 2.6.
            MineTube tube = c.Tubes.Single(t => Math.Abs(t.X0 - (242.1f + 3f)) < 0.01f);

            //  게임 커널로 입구에서 날갯짓, 30틱 뒤 또 날갯짓 — 굴 중심선이 그 궤적과 틱마다 같아야 한다.
            float x = 242.1f + 3f, y = -46.4f, vy = 0f;
            Assert.AreEqual(y, tube.Center(x), 1e-3f);
            for (int k = 0; k < 60; k++)
            {
                bool flap = k % 30 == 0;
                vy = LOP.FlappyVerticalKernel.Next(vy, flap, LOP.FlappyAirflowKind.None, P.Flap, P.Gravity, P.MaxFall, P.Tick, 0f, 0f, 1f);
                y += vy * P.Tick;
                x += P.Forward * P.Tick;
                Assert.AreEqual(y, tube.Center(x), 1e-3f, $"틱 {k}");
            }
            Assert.AreEqual(x, tube.X1, 1e-3f);
            Assert.AreEqual(3.1f, tube.Gap, 1e-5f);
            //  위 굴: 칸막이 위 ~ 천장(BaseHalf + 1).
            Assert.AreEqual(-49f + 0.6f, tube.Low, 1e-4f);
            Assert.AreEqual(-49f + MineCourse.BaseHalf + 1f, tube.High, 1e-4f);
        }

        [Test]
        public void 같은_입력이면_같은_코스()
        {
            var a = Gates(Layout());
            var b = Gates(Layout());
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.AreEqual(a[i].GapCenter, b[i].GapCenter);
                Assert.AreEqual(a[i].Width, b[i].Width);
            }
        }

        [Test]
        public void Validate_통과() => Assert.IsNull(MineCourseRule.Validate(Layout()));

        [Test]
        public void Validate는_통로_밖_틈을_잡는다()
        {
            var c = Layout();
            var gates = Gates(c);
            MineGate g = gates[2];
            gates[2] = new MineGate(g.X, g.Width, g.GapCenter + 30f, g.Gap, g.Low, g.High);
            c.Gates = gates;
            string msg = MineCourseRule.Validate(c);
            Assert.IsNotNull(msg);
            //  한글 검색은 문화권 비교를 피해 Ordinal로.
            Assert.GreaterOrEqual(msg.IndexOf("관문", StringComparison.Ordinal), 0, msg);
        }

        [Test]
        public void 자를_x는_정렬되고_양끝을_포함한다()
        {
            var c = Layout();
            var xs = c.Breaks(1f);
            Assert.AreEqual(0f, xs[0], 1e-5f);
            Assert.AreEqual(c.Length, xs[xs.Count - 1], 1e-5f);
            for (int i = 1; i < xs.Count; i++) { Assert.Greater(xs[i], xs[i - 1]); }
            //  첫 수직 갱 꼭짓점(a, b)이 들어 있다 — 22 m 낙하를 꺾지 않으면 바닥이 엉뚱하게 깎인다.
            Assert.IsTrue(xs.Any(x => Math.Abs(x - 24.8f) < 1e-3f));
            Assert.IsTrue(xs.Any(x => Math.Abs(x - 32.8f) < 1e-3f));
        }
    }
}
