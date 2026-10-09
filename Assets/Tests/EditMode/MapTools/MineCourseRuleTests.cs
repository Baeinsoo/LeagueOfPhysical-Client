using System;
using System.Collections.Generic;
using System.Linq;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 광산 코스 배치. 기준값은 전부 웹 프로토타입(v30 "0 · 광산 3막")에서 뽑은 것이다 — 같은 난수·같은 소비 순서로
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
        public void 길이는_프로토타입과_같다() => Assert.AreEqual(396.95f, Layout().Length, 0.01f);

        [Test]
        public void 구간_순서와_시작점()
        {
            //  3막: 1막 하강(갱 → 슬라럼 → 갈림길⬇) · 2막 갱 바닥(낮은 천장 → 롤러코스터 → 급반전 → 갈림길⬆ → 깊은 갱) · 3막 탈출(긴 통로 → 굴뚝 → 갈림길⬆).
            var expect = new (string name, float x)[]
            {
                ("수직 갱 낙하", 24.8f), ("슬라럼", 45.65f), ("고수 갈림길 ⬇굴", 94.25f),
                ("낮은 천장", 129.2f), ("레일 롤러코스터", 162.05f), ("급반전", 225.9f), ("고수 갈림길 ⬆굴", 252.9f), ("수직 갱 낙하", 277.05f),
                ("긴 통로", 305.3f), ("굴뚝 오르기", 326.15f), ("고수 갈림길 ⬆굴", 352.6f),
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
            Assert.AreEqual(37, gates.Count);
            //  출발 관문 2 → 갱 바닥(−22)의 사이 관문 1 → 슬라럼 첫 관문.
            var expect = new (float x, float c, float w)[]
            {
                (14.975f, -1.364f, 1.95f), (20.375f, 1.699f, 1.95f), (41.225f, -22.214f, 1.95f), (46.625f, -24.6f, 1.95f),
            };
            for (int i = 0; i < expect.Length; i++)
            {
                Assert.AreEqual(expect[i].x, gates[i].X, 0.002f, $"관문 {i} x");
                Assert.AreEqual(expect[i].c, gates[i].GapCenter, 0.002f, $"관문 {i} 틈 중심");
                Assert.AreEqual(expect[i].w, gates[i].Width, 0.002f, $"관문 {i} 두께");
                Assert.AreEqual(3.75f, gates[i].Gap, 1e-4f, $"관문 {i} 틈");
            }
            //  관문 29 = 긴 통로(12 m, 틈 3.1).
            Assert.AreEqual(311.3f, gates[29].X, 0.002f);
            Assert.AreEqual(12f, gates[29].Width, 1e-4f);
            Assert.AreEqual(3.1f, gates[29].Gap, 1e-4f);
            Assert.AreEqual(-49.895f, gates[29].GapCenter, 0.002f);
            var last = gates[gates.Count - 1];
            Assert.AreEqual(388.525f, last.X, 0.002f);
            Assert.AreEqual(-24.607f, last.GapCenter, 0.002f);
        }

        [Test]
        public void 난이도_값이_프로토타입과_같다()
        {
            var c = Layout();
            var gates = Gates(c);

            //  슬라럼 8개(관문 3~10): 갱 바닥(−22) ± 2.6을 번갈아.
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(46.625f + 5.4f * i, gates[3 + i].X, 0.002f, $"슬라럼 {i} x");
                Assert.AreEqual(i % 2 == 1 ? -19.4f : -24.6f, gates[3 + i].GapCenter, 1e-4f, $"슬라럼 {i} 틈 중심");
            }
            //  급반전(관문 19~22): −22에서 +5.5 / −5.5 번갈아.
            var flip = new[] { -16.5f, -22f, -16.5f, -22f };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(226.875f + 5.4f * i, gates[19 + i].X, 0.002f, $"급반전 {i} x");
                Assert.AreEqual(flip[i], gates[19 + i].GapCenter, 1e-4f, $"급반전 {i} 틈 중심");
            }
            //  갈림길 보통 관문 — 틈 3.3, 폭 3 m 난수 띠(첫 갈림길 ⬇굴이라 보통 길은 위, 관문 12~14).
            var fork = new[] { -17.458f, -18.601f, -17.586f };
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(fork[i], gates[12 + i].GapCenter, 0.002f, $"갈림길 관문 {i}");
                Assert.AreEqual(3.3f, gates[12 + i].Gap, 1e-4f, $"갈림길 관문 {i} 틈");
            }

            //  물결 터널은 빠졌다 — 굴은 롤러코스터 1 + 갈림길 굴 3.
            Assert.AreEqual(4, c.Tubes.Count);
            MineTube coaster = c.Tubes.Single(t => Math.Abs(t.X0 - 162.05f) < 0.01f);
            Assert.AreEqual(4.8f, coaster.Gap, 1e-5f);
            Assert.AreEqual(162.05f + 55f, coaster.X1, 0.01f, "롤러코스터 55 m");

            //  낮은 천장(129.2 ~ 153.2, 24 m): 반 높이 2.2, 중심선 물결 진폭 3.5 · 주기 17(¼ 주기에서 꼭대기).
            Assert.AreEqual(2.2f, c.HalfAt(129.2f + 10f), 1e-4f);
            Assert.AreEqual(-22f + 3.5f, c.CenterAt(129.2f + 17f / 4f), 0.01f);
            Assert.AreEqual(MineCourse.BaseHalf, c.HalfAt(153.2f + 3.5f), 1e-4f, "24 m 뒤 3 m 전이 밖은 넓은 통로");
        }

        [Test]
        public void 갈림길은_아래_위_위이고_패드는_굴_뒤에()
        {
            var c = Layout();
            var expectForks = new (float x0, float x1, float b, bool up)[] { (94.25f, 114.95f, -22f, false), (252.9f, 273.6f, -22f, true), (352.6f, 373.3f, -22f, true) };
            var expectPads = new (float x0, float y0, float y1)[] { (102.95f, -28.0f, -25.2f), (261.6f, -20.8f, -18.0f), (361.3f, -20.8f, -18.0f) };
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
                (0f, 0f, 7.28f), (100f, -22f, 7.28f), (150f, -18.548f, 2.2f), (200f, -22f, 7.28f), (300f, -50f, 7.28f), (390f, -22f, 7.28f),
            };
            foreach (var e in expect)
            {
                Assert.AreEqual(e.center, c.CenterAt(e.x), 0.01f, $"x={e.x} 중심");
                Assert.AreEqual(e.half, c.HalfAt(e.x), 0.01f, $"x={e.x} 반 높이");
            }

            //  수직 갱 둘: 좁은 구간 [a−2, b+4], a = 24.8(8 m에 22 m) · 277.05(10 m에 28 m). 굴뚝: [a−2, b+2], a = 326.15(21 m에 28 m).
            Assert.AreEqual(2.8f, c.HalfAt(24.8f + 4f), 0.01f);
            Assert.AreEqual(2.8f, c.HalfAt(277.05f + 4f), 0.01f);
            Assert.AreEqual(1.4f, c.HalfAt(326.15f + 8f), 0.01f);
            Assert.AreEqual(-22f, c.CenterAt(24.8f + 8f), 0.01f, "첫 갱 바닥");
            Assert.AreEqual(-50f, c.CenterAt(277.05f + 10f), 0.01f, "깊은 갱 바닥");
            Assert.AreEqual(-22f, c.CenterAt(326.15f + 21f), 0.01f, "굴뚝 꼭대기");
            //  경계(좁은 구간 시작) 1.5 m 밖 = 3 m 전이의 한가운데.
            Assert.AreEqual((MineCourse.BaseHalf + 2.8f) / 2f, c.HalfAt(24.8f - 2f - 1.5f), 0.01f);
            Assert.AreEqual((MineCourse.BaseHalf + 1.4f) / 2f, c.HalfAt(326.15f - 2f - 1.5f), 0.01f);
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

        [Test]
        public void 두번_점프_굴은_커널로_날린_궤적과_틱마다_같다()
        {
            var c = Layout();
            //  첫 위 굴 = 두 번째 갈림길(252.9, 바닥 −22) — 굴 칸 중심 −22 + 2.6.
            MineTube tube = c.Tubes.Single(t => Math.Abs(t.X0 - (252.9f + 3f)) < 0.01f);

            //  게임 커널로 입구에서 날갯짓, 30틱 뒤 또 날갯짓 — 굴 중심선이 그 궤적과 틱마다 같아야 한다.
            float x = 252.9f + 3f, y = -19.4f, vy = 0f;
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
            Assert.AreEqual(-22f + 0.6f, tube.Low, 1e-4f);
            Assert.AreEqual(-22f + MineCourse.BaseHalf + 1f, tube.High, 1e-4f);
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
