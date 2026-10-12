using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 광산 코스 배치. 기준값은 웹 프로토타입(v31 "0 · 광산 3막", 슬라럼 자리 = 계단 내리막)에서 뽑은 것이다 — 같은 난수·같은 소비 순서로
    /// 옮겼는지가 이 테스트의 요점이다. 숫자 하나가 어긋나면 "프로토타입에서 해 본 그 코스"가 아니다.
    /// <para>10-12 사람 판 레이아웃 변경으로 배치는 프로토타입 v33에서 일부러 갈라졌다(사람이 지나가게 — 첫 갱 9.2 m, 갱 반 높이 4.3, 낮은 천장 진폭 2.5,
    /// 굴 틈 3.4, 계단·급반전 간격 +2.6, 급반전 ±4.5, 갈림길 뒤 여유 3 m). 난수 소비 순서는 그대로라 틈 중심 난수값은 같고, x·길이가 그만큼 밀렸다.
    /// 아래 x·길이 기준값은 그 새 배치에서 뽑았다.</para>
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
        //  10-12 사람 판 레이아웃 변경: 프로토타입 v33(386.15)에서 갈라졌다 — 사람이 지나가게 간격을 벌려 422.35.
        public void 길이는_프로토타입과_같다() => Assert.AreEqual(422.35f, Layout().Length, 0.01f);

        [Test]
        public void 구간_순서와_시작점()
        {
            //  3막: 1막 하강(갱 → 계단 내리막 → 갈림길⬇) · 2막 갱 바닥(낮은 천장 → 롤러코스터 → 급반전 → 갈림길⬆ → 깊은 갱) · 3막 탈출(긴 통로 → 굴뚝 → 갈림길⬆).
            //  10-12 사람 판 레이아웃 변경: 첫 갱 9.2 m · 계단·급반전 간격 +2.6 · 갈림길 뒤 +3 m로 첫 갱 뒤가 전부 밀렸다.
            var expect = new (string name, float x)[]
            {
                ("수직 갱 낙하", 24.8f), ("계단 내리막", 46.85f), ("고수 갈림길 ⬇굴", 100.25f),
                ("낮은 천장", 138.2f), ("레일 롤러코스터", 171.05f), ("급반전", 234.9f), ("고수 갈림길 ⬆굴", 272.3f), ("수직 갱 낙하", 299.45f),
                ("긴 통로", 327.7f), ("굴뚝 오르기", 348.55f), ("고수 갈림길 ⬆굴", 375f),
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
            //  10-12 사람 판 레이아웃 변경: 첫 갱 8 → 9.2 m라 관문 2부터 x가 1.2 m 밀렸다(틈 중심 난수는 그대로).
            var expect = new (float x, float c, float w)[]
            {
                (14.975f, -1.364f, 1.95f), (20.375f, 1.699f, 1.95f), (42.425f, -22.214f, 1.95f), (47.825f, -26.5f, 1.95f),
            };
            for (int i = 0; i < expect.Length; i++)
            {
                Assert.AreEqual(expect[i].x, gates[i].X, 0.002f, $"관문 {i} x");
                Assert.AreEqual(expect[i].c, gates[i].GapCenter, 0.002f, $"관문 {i} 틈 중심");
                Assert.AreEqual(expect[i].w, gates[i].Width, 0.002f, $"관문 {i} 두께");
                Assert.AreEqual(3.75f, gates[i].Gap, 1e-4f, $"관문 {i} 틈");
            }
            //  관문 27 = 긴 통로(12 m, 틈 3.1). 10-12 사람 판 레이아웃 변경으로 x 300.5 → 333.7.
            Assert.AreEqual(333.7f, gates[27].X, 0.002f);
            Assert.AreEqual(12f, gates[27].Width, 1e-4f);
            Assert.AreEqual(3.1f, gates[27].Gap, 1e-4f);
            //  10-12 몸통 공 + 둘째 갱·굴뚝 28 → 24 m(게임 커널 검사: 28 m 갱은 최대 낙하로도 못 내려가고, 굴뚝은 초당 4번 가까이 쳐야 했다) — 그 사이 4 m 위로.
            Assert.AreEqual(-72.895f, gates[27].GapCenter, 0.002f);
            var last = gates[gates.Count - 1];
            Assert.AreEqual(413.925f, last.X, 0.002f);   // 10-12 사람 판 레이아웃 변경(377.725 → 413.925)
            Assert.AreEqual(-51.607f, last.GapCenter, 0.002f);
        }

        [Test]
        public void 난이도_값이_프로토타입과_같다()
        {
            //  10-12 사람 판 레이아웃 변경으로 프로토타입 v33과 갈라졌다(사람이 지나가게) — 아래 값은 새 배치 기준이다.
            var c = Layout();
            var gates = Gates(c);

            //  계단 내리막 6개(관문 3~8): 갱 바닥(−22)에서 관문마다 4.5 m씩 내려 −26.5 … −49. 틈은 보통(3.75).
            //  통로도 계단을 따라 내려간다 — 그 관문 x의 통로 중심 = 틈 중심.
            //  10-12 사람 판 레이아웃 변경: 관문 간격 5.4 → 8 m(StairExtra 2.6), 첫 관문 x는 첫 갱 9.2 m로 46.625 → 47.825.
            for (int i = 0; i < 6; i++)
            {
                float x = 47.825f + 8f * i, step = -22f - 4.5f * (i + 1);
                Assert.AreEqual(x, gates[3 + i].X, 0.002f, $"계단 {i} x");
                Assert.AreEqual(step, gates[3 + i].GapCenter, 1e-4f, $"계단 {i} 틈 중심");
                Assert.AreEqual(3.75f, gates[3 + i].Gap, 1e-4f, $"계단 {i} 틈");
                Assert.AreEqual(step, c.CenterAt(x), 1e-3f, $"계단 {i} 통로 중심");
            }
            //  급반전(관문 17~20): −49에서 +4.5 / −4.5 번갈아, 간격 8 m.
            //  10-12 사람 판 레이아웃 변경: ±5.5 → ±4.5, 간격 5.4 → 8 m(FlipExtra 2.6), 시작 x 216.075 → 235.875.
            var flip = new[] { -44.5f, -49f, -44.5f, -49f };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(235.875f + 8f * i, gates[17 + i].X, 0.002f, $"급반전 {i} x");
                Assert.AreEqual(flip[i], gates[17 + i].GapCenter, 1e-4f, $"급반전 {i} 틈 중심");
            }
            //  갈림길 보통 관문 — 틈 3.3, 폭 3 m 난수 띠(첫 갈림길 ⬇굴이라 보통 길은 위, 관문 10~12).
            var fork = new[] { -44.458f, -45.601f, -44.586f };
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(fork[i], gates[10 + i].GapCenter, 0.002f, $"갈림길 관문 {i}");
                Assert.AreEqual(3.3f, gates[10 + i].Gap, 1e-4f, $"갈림길 관문 {i} 틈");
            }

            //  물결 터널은 빠졌다 — 굴은 롤러코스터 1 + 갈림길 굴 3 + 패드 방 3.
            Assert.AreEqual(7, c.Tubes.Count);
            //  구간 시작 x는 코스에서 찾는다(값 자체는 구간_순서와_시작점이 지킨다).
            float coasterX = c.Sections.Single(s => s.Name == "레일 롤러코스터").X;
            float lowX = c.Sections.Single(s => s.Name == "낮은 천장").X;
            MineTube coaster = c.Tubes.Single(t => Math.Abs(t.X0 - coasterX) < 0.01f);
            Assert.AreEqual(4.8f, coaster.Gap, 1e-5f);
            Assert.AreEqual(coasterX + 55f, coaster.X1, 0.01f, "롤러코스터 55 m");

            //  낮은 천장(24 m): 반 높이 2.2, 중심선 물결 진폭 2.5 · 주기 17(¼ 주기에서 꼭대기). 계단 끝 바닥 −49.
            //  10-12 사람 판 레이아웃 변경: 진폭 3.5 → 2.5.
            Assert.AreEqual(2.2f, c.HalfAt(lowX + 10f), 1e-4f);
            Assert.AreEqual(-49f + 2.5f, c.CenterAt(lowX + 17f / 4f), 0.01f);
            Assert.AreEqual(MineCourse.BaseHalf, c.HalfAt(lowX + 24f + 3.5f), 1e-4f, "24 m 뒤 3 m 전이 밖은 넓은 통로");
        }

        [Test]
        public void 갈림길은_아래_위_위이고_패드는_굴_뒤에()
        {
            var c = Layout();
            //  10-12 사람 판 레이아웃 변경으로 x가 밀렸다(83.45 / 242.1 / 341.8 → 100.25 / 272.3 / 375).
            var expectForks = new (float x0, float x1, float b, bool up)[] { (100.25f, 120.95f, -49f, false), (272.3f, 293f, -49f, true), (375f, 395.7f, -49f, true) };
            var expectPads = new (float x0, float y0, float y1)[] { (108.95f, -55.0f, -52.2f), (281f, -47.8f, -45.0f), (383.7f, -47.8f, -45.0f) };
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
        public void 고수_길은_굴을_나오면_땅에_둘러싸인_패드_방에서_합류한다()
        {
            var c = Layout();
            //  웹 v33: 굴 끝(ax1) ~ 칸막이 끝(fx1), 굴 칸 중심 높이, 틈 4.6, 위·아래 끝은 굴과 같은 칸 끝.
            //  10-12 사람 판 레이아웃 변경으로 x만 밀렸다(굴 틈 3.4라도 굴 길이는 궤적이 정한다).
            var expect = new (float x0, float x1, float center, float low, float high)[]
            {
                (108.65f, 120.95f, -53.6f, -49f - MineCourse.BaseHalf - 1f, -49.6f),  // ⬇굴
                (280.7f, 293f, -46.4f, -48.4f, -49f + MineCourse.BaseHalf + 1f),      // ⬆굴
                (383.4f, 395.7f, -46.4f, -48.4f, -49f + MineCourse.BaseHalf + 1f),    // ⬆굴
            };
            for (int i = 0; i < 3; i++)
            {
                MineFork f = c.Forks[i];
                MineTube tunnel = c.Tubes.Single(t => Math.Abs(t.X0 - (f.X0 + 3f)) < 0.01f);
                MineTube room = c.Tubes.Single(t => Math.Abs(t.X0 - tunnel.X1) < 0.01f && t != tunnel);
                Assert.AreEqual(expect[i].x0, room.X0, 0.01f, $"방 {i} X0");
                Assert.AreEqual(expect[i].x1, room.X1, 0.01f, $"방 {i} X1 = 칸막이 끝");
                Assert.AreEqual(f.X1, room.X1, 1e-4f, $"방 {i}은 칸막이와 같이 끝난다");
                Assert.AreEqual(4.6f, room.Gap, 1e-5f, $"방 {i} 틈");
                Assert.AreEqual(expect[i].center, room.Center(room.X0), 1e-4f, $"방 {i} 중심(시작)");
                Assert.AreEqual(expect[i].center, room.Center(room.X1), 1e-4f, $"방 {i} 중심(끝) — 수평");
                Assert.AreEqual(expect[i].low, room.Low, 1e-4f, $"방 {i} Low");
                Assert.AreEqual(expect[i].high, room.High, 1e-4f, $"방 {i} High");
                //  패드는 방 안에.
                MinePad pad = c.Pads[i];
                Assert.Greater(pad.Rect.X0, room.X0); Assert.Less(pad.Rect.X1, room.X1);
                Assert.Greater(pad.Rect.Y0, expect[i].center - 2.3f); Assert.Less(pad.Rect.Y1, expect[i].center + 2.3f);
            }
        }

        [Test]
        public void 통로_높이()
        {
            var c = Layout();
            //  10-12 사람 판 레이아웃 변경: 낮은 천장이 138.2 ~ 162.2(진폭 2.5)로, 깊은 갱이 299.45 ~ 309.45로 밀려
            //  표본 x를 옮겼다(130 → 150: 낮은 천장 안, 300 → 330: 깊은 갱 바닥 긴 통로).
            var expect = new (float x, float center, float half)[]
            {
                (0f, 0f, 7.28f), (100f, -49f, 7.28f), (150f, -51.347f, 2.2f), (200f, -49f, 7.28f), (330f, -73f, 7.28f), (380f, -49f, 7.28f),
            };
            foreach (var e in expect)
            {
                Assert.AreEqual(e.center, c.CenterAt(e.x), 0.01f, $"x={e.x} 중심");
                Assert.AreEqual(e.half, c.HalfAt(e.x), 0.01f, $"x={e.x} 반 높이");
            }

            //  수직 갱 둘: 좁은 구간 [a−2, b+4], 첫 갱 9.2 m에 22 m · 깊은 갱 10 m에 24 m. 굴뚝: [a−2, b+2], 21 m에 24 m.
            //  10-12 사람 판 레이아웃 변경: 갱 반 높이 2.8 → 4.3, 첫 갱 8 → 9.2 m. a는 코스의 구간 x에서 찾는다.
            float drop1 = c.Sections.First(s => s.Name == "수직 갱 낙하").X;
            float drop2 = c.Sections.Last(s => s.Name == "수직 갱 낙하").X;
            float climb = c.Sections.Single(s => s.Name == "굴뚝 오르기").X;
            Assert.AreEqual(24.8f, drop1, 0.01f);
            Assert.AreEqual(4.3f, c.HalfAt(drop1 + 4f), 0.01f);
            Assert.AreEqual(4.3f, c.HalfAt(drop2 + 4f), 0.01f);
            Assert.AreEqual(1.4f, c.HalfAt(climb + 8f), 0.01f);
            Assert.AreEqual(-22f, c.CenterAt(drop1 + 9.2f), 0.01f, "첫 갱 바닥");
            Assert.AreEqual(-73f, c.CenterAt(drop2 + 10f), 0.01f, "깊은 갱 바닥");
            Assert.AreEqual(-49f, c.CenterAt(climb + 21f), 0.01f, "굴뚝 꼭대기");
            //  경계(좁은 구간 시작) 1.5 m 밖 = 3 m 전이의 한가운데.
            Assert.AreEqual((MineCourse.BaseHalf + 4.3f) / 2f, c.HalfAt(drop1 - 2f - 1.5f), 0.01f);
            Assert.AreEqual((MineCourse.BaseHalf + 1.4f) / 2f, c.HalfAt(climb - 2f - 1.5f), 0.01f);
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
            //  첫 위 굴 = 두 번째 갈림길(바닥 −49) — 굴 칸 중심 −49 + 2.6. 갈림길 x는 코스에서 찾는다(10-12 사람 판 레이아웃 변경으로 242.1 → 272.3).
            MineFork fork = c.Forks[1];
            Assert.IsTrue(fork.TunnelUp);
            MineTube tube = c.Tubes.Single(t => Math.Abs(t.X0 - (fork.X0 + 3f)) < 0.01f);

            //  게임 커널로 입구에서 날갯짓, 30틱 뒤 또 날갯짓 — 굴 중심선이 그 궤적과 틱마다 같아야 한다.
            float x = fork.X0 + 3f, y = -46.4f, vy = 0f;
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
            Assert.AreEqual(3.4f, tube.Gap, 1e-5f);   // 10-12 사람 판 레이아웃 변경(3.1 → 3.4)
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
            //  10-12 사람 판 레이아웃 변경: 첫 갱 8 → 9.2 m라 b = 32.8 → 34.0.
            Assert.IsTrue(xs.Any(x => Math.Abs(x - 24.8f) < 1e-3f));
            Assert.IsTrue(xs.Any(x => Math.Abs(x - 34.0f) < 1e-3f));
        }
    }
}
