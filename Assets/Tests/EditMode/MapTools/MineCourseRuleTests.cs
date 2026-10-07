using System;
using System.Collections.Generic;
using System.Linq;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 광산 코스 배치. 기준값은 전부 웹 프로토타입(mode 9)에서 뽑은 것이다 — 같은 난수·같은 소비 순서로
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
        public void 길이는_프로토타입과_같다() => Assert.AreEqual(407.35f, Layout().Length, 0.01f);

        [Test]
        public void 구간_순서와_시작점()
        {
            var expect = new (string name, float x)[]
            {
                ("긴 통로", 35.6f), ("슬라럼", 56.45f), ("물결 터널", 94.25f),
                ("고수 갈림길 ⬆굴", 127.1f), ("수직 갱 낙하", 156.65f), ("낮은 천장", 182.9f), ("레일 롤러코스터", 225.75f),
                ("고수 갈림길 ⬇굴", 279.6f), ("급반전", 309.15f), ("굴뚝 오르기", 336.15f), ("고수 갈림길 ⬆굴", 363f),
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
            Assert.AreEqual(38, gates.Count);
            var expect = new (float x, float c, float w)[]
            {
                (14.975f, -1.366f, 1.95f), (20.375f, 1.218f, 1.95f), (25.775f, 1.457f, 1.95f),
                (31.175f, -1.271f, 1.95f), (41.6f, -0.679f, 12f), (52.025f, 0.451f, 1.95f),
            };
            for (int i = 0; i < expect.Length; i++)
            {
                Assert.AreEqual(expect[i].x, gates[i].X, 0.002f, $"관문 {i} x");
                Assert.AreEqual(expect[i].c, gates[i].GapCenter, 0.002f, $"관문 {i} 틈 중심");
                Assert.AreEqual(expect[i].w, gates[i].Width, 0.002f, $"관문 {i} 두께");
                //  관문 4 = 긴 통로(12 m) — 틈이 3.1로 좁다. 나머지는 원조 틈 3.75.
                Assert.AreEqual(i == 4 ? 3.1f : 3.75f, gates[i].Gap, 1e-4f, $"관문 {i} 틈");
            }
            var last = gates[gates.Count - 1];
            Assert.AreEqual(398.925f, last.X, 0.002f);
            Assert.AreEqual(1.043f, last.GapCenter, 0.002f);
        }

        [Test]
        public void 난이도_값이_프로토타입과_같다()
        {
            var c = Layout();
            var gates = Gates(c);

            //  슬라럼(관문 6~11): 통로 중심(0) ± 2.6을 번갈아.
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(57.425f + 5.4f * i, gates[6 + i].X, 0.002f, $"슬라럼 {i} x");
                Assert.AreEqual(i % 2 == 1 ? 2.6f : -2.6f, gates[6 + i].GapCenter, 1e-4f, $"슬라럼 {i} 틈 중심");
            }
            //  급반전(관문 26~29): 바닥 −22에서 +5.5 / −5.5 번갈아.
            var flip = new[] { -16.5f, -22f, -16.5f, -22f };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(310.125f + 5.4f * i, gates[26 + i].X, 0.002f, $"급반전 {i} x");
                Assert.AreEqual(flip[i], gates[26 + i].GapCenter, 1e-4f, $"급반전 {i} 틈 중심");
            }
            //  갈림길 반대쪽 보통 관문 — 폭 4 m 난수 띠(첫 갈림길, 관문 14~16).
            var fork = new[] { -3.911f, -4.496f, -6.275f };
            for (int i = 0; i < 3; i++) { Assert.AreEqual(fork[i], gates[14 + i].GapCenter, 0.002f, $"갈림길 관문 {i}"); }

            //  물결 터널 틈 3.7, 롤러코스터 틈 4.85.
            MineTube wave = c.Tubes.Single(t => Math.Abs(t.X0 - 94.25f) < 0.01f);
            Assert.AreEqual(3.7f, wave.Gap, 1e-5f);
            Assert.AreEqual(1.2f, wave.Center(94.25f + 4f), 1e-4f, "물결 진폭 1.2 · 주기 16");
            MineTube coaster = c.Tubes.Single(t => Math.Abs(t.X0 - 225.75f) < 0.01f);
            Assert.AreEqual(4.85f, coaster.Gap, 1e-5f);

            //  낮은 천장(182.9 ~ 216.9): 반 높이 2.2, 중심선 물결 진폭 3.5 · 주기 17(¼ 주기에서 꼭대기).
            Assert.AreEqual(2.2f, MineCourse.LowHalf, 1e-6f);
            Assert.AreEqual(2.2f, c.HalfAt(182.9f + 10f), 1e-4f);
            Assert.AreEqual(-22f + 3.5f, c.CenterAt(182.9f + 17f / 4f), 0.01f);
        }

        [Test]
        public void 갈림길은_위_아래_위이고_패드는_굴_뒤에()
        {
            var c = Layout();
            var expectForks = new (float x0, float x1, float b, bool up)[] { (127.1f, 147.8f, 0f, true), (279.6f, 300.3f, -22f, false), (363f, 383.7f, -1f, true) };
            var expectPads = new (float x0, float y0, float y1)[] { (135.8f, 3.1f, 5.9f), (288.3f, -27.9f, -25.1f), (371.7f, 2.1f, 4.9f) };
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
            var expect = new (float x, float center, float half)[] { (0f, 0f, 10.92f), (100f, 0f, 10.92f), (200f, -21.871f, 2.2f), (300f, -22f, 10.92f), (400f, -1f, 10.92f) };
            foreach (var e in expect)
            {
                Assert.AreEqual(e.center, c.CenterAt(e.x), 0.01f, $"x={e.x} 중심");
                Assert.AreEqual(e.half, c.HalfAt(e.x), 0.01f, $"x={e.x} 반 높이");
            }

            //  수직 갱: 좁은 구간 [a−2, b+4], a = 156.65.  굴뚝: [a−2, b+2], a = 336.15.
            Assert.AreEqual(2.8f, c.HalfAt(156.65f + 4f), 0.01f);
            Assert.AreEqual(1.4f, c.HalfAt(336.15f + 8f), 0.01f);
            //  경계(좁은 구간 시작) 1.5 m 밖 = 3 m 전이의 한가운데.
            Assert.AreEqual((MineCourse.BaseHalf + 2.8f) / 2f, c.HalfAt(156.65f - 2f - 1.5f), 0.01f);
            Assert.AreEqual((MineCourse.BaseHalf + 1.4f) / 2f, c.HalfAt(336.15f - 2f - 1.5f), 0.01f);
        }

        [Test]
        public void 두번_점프_굴은_커널로_날린_궤적과_틱마다_같다()
        {
            var c = Layout();
            MineTube tube = c.Tubes.Single(t => Math.Abs(t.X0 - (127.1f + 3f)) < 0.01f);

            //  게임 커널로 입구에서 날갯짓, 30틱 뒤 또 날갯짓 — 굴 중심선이 그 궤적과 틱마다 같아야 한다.
            float x = 127.1f + 3f, y = 4.5f, vy = 0f;
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
            Assert.AreEqual(0.6f, tube.Low, 1e-4f);
            Assert.AreEqual(MineCourse.BaseHalf + 1f, tube.High, 1e-4f);
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
            //  수직 갱 꼭짓점(a, b)이 들어 있다 — 22 m 낙하를 꺾지 않으면 바닥이 엉뚱하게 깎인다.
            Assert.IsTrue(xs.Any(x => Math.Abs(x - 156.65f) < 1e-3f));
            Assert.IsTrue(xs.Any(x => Math.Abs(x - 164.65f) < 1e-3f));
        }
    }
}
