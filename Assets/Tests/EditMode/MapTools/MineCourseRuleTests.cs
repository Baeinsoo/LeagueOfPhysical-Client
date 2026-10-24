using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LOP.MapTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 광산 코스 배치. 기준값은 웹 프로토타입(v31 "0 · 광산 3막", 슬라럼 자리 = 계단 내리막)에서 뽑은 것이다 — 같은 난수·같은 소비 순서로
    /// 옮겼는지가 이 테스트의 요점이다. 숫자 하나가 어긋나면 "프로토타입에서 해 본 그 코스"가 아니다.
    /// <para>10-12 사람 판 레이아웃 변경으로 배치는 프로토타입 v33에서 일부러 갈라졌다(사람이 지나가게 — 첫 갱 9.2 m, 갱 반 높이 4.3, 낮은 천장 진폭 2.5,
    /// 굴 틈 3.4, 계단·급반전 간격 +2.6, 급반전 ±4.5, 갈림길 뒤 여유 3 m). 난수 소비 순서는 그대로라 틈 중심 난수값은 같고, x·길이가 그만큼 밀렸다.
    /// 아래 x·길이 기준값은 그 새 배치에서 뽑았다.</para>
    /// 10-13 동적 지형 1차: 기준값은 시안 v10 "설계안"(PLAN_ORDER) 덤프 — 길이 445.95.
    /// 시안 v10에서 갈라짐(10-13 검사): 급반전 앞 보통 관문 → 첫 급반전 간격 5.4 → 8 m(FlipLead 2.6) — 급반전부터 뒤는 x·길이가 2.6씩 밀렸다(448.55).
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
        public void 길이는_시안과_같다() => Assert.AreEqual(483.05f, Layout().Length, 0.01f);   // 시안 v10 445.95에서 갈라짐(10-13 검사, +2.6 · 10-15 무너진 갱도 길이 10 → 11, +1 · 10-24 낙석 +3줄 13.5 · 진자 +2자리 20)

        [Test]
        public void 구간_순서와_시작점()
        {
            //  1막(갱 → 흔들리는 계단 → 낙석 → 갈림길⬇ 무너진 갱도) · 2막(진자 광차 → 짧은 롤러코스터 → 조인 급반전 → 갈림길⬆ 두 번 굴 → 깊은 갱)
            //  · 3막(승강기 → 굴뚝 → 갈림길⬆ 레일 굴).
            var expect = new (string name, float x)[]
            {
                ("수직 갱 낙하", 24.8f), ("계단 내리막", 46.85f), ("낙석", 93.05f), ("고수 갈림길 ⬇굴", 133.4f),
                ("진자 광차", 176.95f), ("레일 롤러코스터", 235.8f), ("급반전", 274.65f), ("고수 갈림길 ⬆굴", 309.85f), ("수직 갱 낙하", 337f),
                ("승강기", 365.25f), ("굴뚝 오르기", 402.65f), ("고수 갈림길 ⬆굴", 429.1f),
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
        public void 고정_관문들이_같은_난수로_선다()
        {
            var gates = Gates(Layout());
            //  시안 덤프 42개 중 승강기 10개를 뺀 32개.
            Assert.AreEqual(32, gates.Count);
            var expect = new (float x, float c)[]
            {
                (14.975f, -1.364f), (20.375f, 1.699f), (42.425f, -22.214f), (88.625f, -50.075f), (128.975f, -47.866f),
            };
            for (int i = 0; i < expect.Length; i++)
            {
                Assert.AreEqual(expect[i].x, gates[i].X, 0.002f, $"관문 {i} x");
                Assert.AreEqual(expect[i].c, gates[i].GapCenter, 0.002f, $"관문 {i} 틈 중심");
                Assert.AreEqual(3.75f, gates[i].Gap, 1e-4f, $"관문 {i} 틈");
            }
            var last = gates[gates.Count - 1];
            Assert.AreEqual(474.625f, last.X, 0.002f);
            Assert.AreEqual(-48.192f, last.GapCenter, 0.002f);
        }

        [Test]
        public void 급반전은_간격_6_8_흔들림_없음()
        {
            var c = Layout();
            var gates = Gates(c);
            float flipX = c.Sections.Single(s => s.Name == "급반전").X;
            var flip = gates.Where(g => g.X > flipX && g.X < flipX + 4 * 6.8f).ToList();
            Assert.AreEqual(4, flip.Count);
            var centers = new[] { -44.5f, -49f, -44.5f, -49f };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(278.225f + 6.8f * i, flip[i].X, 0.002f, $"급반전 {i} x");
                Assert.AreEqual(centers[i], flip[i].GapCenter, 1e-4f, $"급반전 {i} 틈 중심");
            }
            //  첫 급반전 앞은 8 m(10-13 검사, 시안 v10 5.4에서 갈라짐) — 앞 보통 관문에서 6 m를 오를 여유.
            var before = gates.Last(g => g.X < flipX);
            Assert.AreEqual(8f, flip[0].X - before.X, 0.002f, "앞 보통 관문 → 첫 급반전");
            Assert.IsFalse(c.Lifts.Any(l => l.Gate.X > flipX && l.Gate.X < flipX + 30f), "급반전은 흔들리지 않는다");
        }

        [Test]
        public void 흔들리는_계단은_승강기_관문_여섯()
        {
            var c = Layout();
            float stairX = c.Sections.Single(s => s.Name == "계단 내리막").X;
            var stairs = c.Lifts.Where(l => l.Gate.X > stairX && l.Gate.X < stairX + 6 * 6.8f).ToList();
            Assert.AreEqual(6, stairs.Count);
            for (int i = 0; i < 6; i++)
            {
                MineLift l = stairs[i];
                Assert.AreEqual(47.825f + 6.8f * i, l.Gate.X, 0.002f, $"계단 {i} x");
                Assert.AreEqual(-26.5f - 4.5f * i, l.Gate.GapCenter, 1e-4f, $"계단 {i} 틈 중심");
                Assert.AreEqual(3.75f, l.Gate.Gap, 1e-4f);
                Assert.AreEqual(1.0f, l.Amplitude, 1e-5f);
                Assert.AreEqual(3.0f, l.Period, 1e-5f);
                Assert.AreEqual(60f * i, l.Phase, 1e-4f);
                Assert.AreEqual(l.Gate.GapCenter, c.CenterAt(l.Gate.X), 1e-3f, $"계단 {i} 통로가 따라 내려간다");
            }
        }

        [Test]
        public void 승강기는_관문_넷_간격_8()
        {
            var c = Layout();
            float liftX = c.Sections.Single(s => s.Name == "승강기").X;
            var lifts = c.Lifts.Where(l => l.Gate.X > liftX).ToList();
            Assert.AreEqual(4, lifts.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(366.225f + 8f * i, lifts[i].Gate.X, 0.002f, $"승강기 {i} x");
                Assert.AreEqual(-73f, lifts[i].Gate.GapCenter, 1e-4f);
                Assert.AreEqual(3.75f, lifts[i].Gate.Gap, 1e-4f);
                Assert.AreEqual(1.8f, lifts[i].Amplitude, 1e-5f);
                Assert.AreEqual(3.5f, lifts[i].Period, 1e-5f);
                Assert.AreEqual(90f * i, lifts[i].Phase, 1e-4f);
            }
            Assert.AreEqual(10, c.Lifts.Count, "계단 6 + 승강기 4");
        }

        [Test]
        public void 진자_광차는_다섯_자리_네_대()
        {
            var c = Layout();
            Assert.AreEqual(20, c.Pendulums.Count, "10-24 사람 판: 3 → 5자리로 늘림");
            const float top = -49f + MineCourse.BaseHalf;   // 천장 −41.72
            for (int i = 0; i < 5; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    MinePendulum p = c.Pendulums[i * 4 + k];
                    float cy = top - (2f * MineCourse.BaseHalf / 4f) * (k + 0.5f);
                    float len = top - cy + 1.2f;
                    Assert.AreEqual(180.95f + 10f * i, p.PivotX, 0.002f, $"자리 {i}");
                    Assert.AreEqual(top + 1.2f, p.PivotY, 1e-3f);
                    Assert.AreEqual(len, p.Length, 1e-3f, $"자리 {i} 광차 {k} 줄");
                    Assert.AreEqual(Mathf.Asin(2.5f / len) * Mathf.Rad2Deg, p.Amplitude, 1e-2f, $"자리 {i} 광차 {k} 각도");
                    Assert.AreEqual(2.4f, p.Period, 1e-5f);
                    Assert.AreEqual(k * 90f + i * Mathf.Rad2Deg, p.Phase, 1e-3f, "위상 = k·90° + 자리·1rad(57.3°)");
                    Assert.AreEqual(1.6f, p.CartWidth, 1e-5f);
                    Assert.AreEqual(1.2f, p.CartHeight, 1e-5f);
                }
            }
            //  맨 아래 광차 밑 빈 띠(1.22 m)에서 몸(지름 0.9)을 빼면 0.32 m — 날갯짓 한 번 높이(1.52 m)보다 훨씬 좁아
            //  바닥을 기어 그냥 피해 갈 수 없다(intent 성공 판정 2). 맨 위도 같은 간격이다.
            MinePendulum bottom = c.Pendulums[3];
            float cartBottom = bottom.PivotY - bottom.Length - bottom.CartHeight / 2f;
            float flapHeight = 10.125f * 10.125f / (2f * 33.75f);
            Assert.Less(cartBottom - (-49f - MineCourse.BaseHalf) - 0.9f, flapHeight);
        }

        [Test]
        public void 낙석은_일곱_줄_간격_4_5()
        {
            var c = Layout();
            Assert.AreEqual(7, c.Rockfalls.Count, "10-24 사람 판: 4 → 7줄로 늘림");
            for (int i = 0; i < 7; i++)
            {
                MineRockfall r = c.Rockfalls[i];
                Assert.AreEqual(94.65f + 4.5f * i, r.X, 0.002f, $"줄 {i} x");
                Assert.AreEqual(-49f + MineCourse.BaseHalf, r.Ceiling, 1e-3f);
                Assert.AreEqual(2f * MineCourse.BaseHalf + 1.2f, r.Drop, 1e-3f);
                Assert.AreEqual(1.2f, r.Size, 1e-5f);
                Assert.AreEqual(9f, r.Speed, 1e-5f);
                Assert.AreEqual(2f, r.Period, 1e-5f);
                Assert.AreEqual(45f * i, r.Phase, 1e-4f, "줄마다 +0.25초 = 45° — 10-24 사람 판: 0.5초면 줄마다 평균 3.5 m를 오르내려야 해 '무조건 맞는' 느낌, 0.25초면 빈 자리가 물결처럼 2 m 안쪽으로 움직인다");
            }
        }

        [Test]
        public void 갈림길마다_고수_길_모양이_다르다()
        {
            var c = Layout();
            var shapes = c.Forks.Select(f => f.Shape).ToArray();
            CollectionAssert.AreEqual(new[] { ForkShape.Collapse, ForkShape.Arc, ForkShape.Rail }, shapes);
            var expectForks = new (float x0, float x1, bool up)[] { (133.4f, 159.7f, false), (309.85f, 330.55f, true), (429.1f, 456.4f, true) };
            var expectPadX = new[] { 147.7f, 318.55f, 444.4f };
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(expectForks[i].x0, c.Forks[i].X0, 0.01f, $"갈림길 {i} X0");
                Assert.AreEqual(expectForks[i].x1, c.Forks[i].X1, 0.01f, $"갈림길 {i} X1");
                Assert.AreEqual(expectForks[i].up, c.Forks[i].TunnelUp);
                Assert.AreEqual(expectPadX[i], c.Pads[i].Rect.X0, 0.01f, $"패드 {i}");
            }
            MineTube Tunnel(int i) => c.Tubes.Single(t => Math.Abs(t.X0 - (c.Forks[i].X0 + 3f)) < 0.01f);
            Assert.AreEqual(3.6f, Tunnel(0).Gap, 1e-5f, "무너진 갱도 3.6");
            Assert.AreEqual(11f, Tunnel(0).X1 - Tunnel(0).X0, 0.01f);
            Assert.AreEqual(3.1f, Tunnel(1).Gap, 1e-5f, "두 번 굴 3.1");
            Assert.AreEqual(3.6f, Tunnel(2).Gap, 1e-5f, "레일 굴 3.6");
            Assert.AreEqual(12f, Tunnel(2).X1 - Tunnel(2).X0, 0.01f);
        }

        [Test]
        public void 갈림길_반대쪽_보통_관문_틈은_3_3()
        {
            //  10-14 리뷰 T5: 사람 판으로 정한 "안전하지만 느린 길"(굴 3.1보다 넉넉한 3.3) 단언이 빠졌던 걸 되살린다.
            var c = Layout();
            var gates = Gates(c);
            foreach (MineFork f in c.Forks)
            {
                var near = gates.Where(g => g.X > f.X0 && g.X < f.X1).ToList();
                Assert.IsNotEmpty(near, $"갈림길 x={f.X0} 반대쪽 보통 관문이 없다");
                foreach (MineGate g in near) { Assert.AreEqual(3.3f, g.Gap, 1e-4f, $"갈림길 x={f.X0} 반대쪽 관문 x={g.X} 틈"); }
            }
        }

        [Test]
        public void 무너진_갱도_받침목은_위아래_번갈아_셋()
        {
            var c = Layout();
            MineFork f = c.Forks[0];
            float ax0 = f.X0 + 3f, laneC = f.Base - 4.6f;
            Assert.AreEqual(3, c.Props.Count);
            for (int j = 0; j < 3; j++)
            {
                MineRect p = c.Props[j];
                Assert.AreEqual(ax0 + 1.0f + 4f * j, p.X0, 1e-3f, $"받침목 {j} x");   // 10-15 1.5 + 3j → 1.0 + 4j
                Assert.AreEqual(0.6f, p.X1 - p.X0, 1e-4f, "폭 0.6");
                Assert.AreEqual(0.6f, p.Y1 - p.Y0, 1e-4f, "0.6 튀어나옴");   // 10-15 0.8 → 0.6
                bool fromTop = j % 2 == 0;
                Assert.AreEqual(fromTop ? laneC + 1.8f : laneC - 1.8f, fromTop ? p.Y1 : p.Y0, 1e-3f, $"받침목 {j}는 굴 가장자리에 붙는다");
            }
        }

        [Test]
        public void 레일_굴은_두_번_솟았다_내려온다()
        {
            var c = Layout();
            MineFork f = c.Forks[2];
            MineTube t = c.Tubes.Single(x => Math.Abs(x.X0 - (f.X0 + 3f)) < 0.01f);
            float laneC = f.Base + 2.6f;
            Assert.AreEqual(laneC, t.Center(t.X0), 1e-4f);
            Assert.AreEqual(laneC + 1.6f, t.Center(t.X0 + 3f), 1e-3f, "반 주기에 꼭대기 1.6");
            Assert.AreEqual(laneC, t.Center(t.X0 + 6f), 1e-3f);
            Assert.AreEqual(laneC + 1.6f, t.Center(t.X0 + 9f), 1e-3f);
            Assert.AreEqual(laneC, t.Center(t.X1), 1e-3f);
        }

        [Test]
        public void 고수_굴_끝은_패드_방_틈_안에서_나온다()
        {
            //  끝 높이가 어긋나면 패드 방 벽에 막힌다(시안 10-12). 두 번 굴은 두 번 튀어 0.4 m 높아 끝나지만 방(4.6) 틈 안이다.
            var c = Layout();
            foreach (MineFork f in c.Forks)
            {
                MineTube tunnel = c.Tubes.Single(t => Math.Abs(t.X0 - (f.X0 + 3f)) < 0.01f);
                MineTube room = c.Tubes.Single(t => Math.Abs(t.X0 - tunnel.X1) < 0.01f && t != tunnel);
                float end = tunnel.Center(tunnel.X1), roomC = room.Center(room.X0);
                Assert.GreaterOrEqual(end - tunnel.Gap / 2f, roomC - room.Gap / 2f - 1e-4f, $"x={f.X0} 굴 끝 아래");
                Assert.LessOrEqual(end + tunnel.Gap / 2f, roomC + room.Gap / 2f + 1e-4f, $"x={f.X0} 굴 끝 위");
                if (f.Shape != ForkShape.Arc) { Assert.AreEqual(roomC, end, 1e-3f, "곧은·레일 굴은 칸 중심으로 끝난다"); }
            }
        }

        [Test]
        public void 패드는_굴_뒤_방_안이고_부스트는_칸막이_안에서_끝난다()
        {
            //  옛 갈림길 기준값 테스트(10-12)가 지키던 불변식 — 굴 모양이 갈림길마다 달라져도 그대로여야 한다. 값은 코스에서 찾는다.
            var c = Layout();
            Assert.AreEqual(3, c.Forks.Count);
            Assert.AreEqual(3, c.Pads.Count);
            Assert.AreEqual(3, c.Walls.Count);
            for (int i = 0; i < 3; i++)
            {
                MineFork f = c.Forks[i];
                MinePad pad = c.Pads[i];
                MineTube tunnel = c.Tubes.Single(t => Math.Abs(t.X0 - (f.X0 + 3f)) < 0.01f);
                MineTube room = c.Tubes.Single(t => Math.Abs(t.X0 - tunnel.X1) < 0.01f && t != tunnel);
                Assert.AreEqual(1.1f, pad.Duration, 1e-5f);
                Assert.Greater(pad.Rect.X0, tunnel.X1, $"패드 {i}가 굴 뒤");
                Assert.AreEqual(f.X1, room.X1, 1e-4f, $"방 {i}은 칸막이와 같이 끝난다");
                Assert.AreEqual(4.6f, room.Gap, 1e-5f, $"방 {i} 틈");
                Assert.AreEqual(room.Center(room.X0), room.Center(room.X1), 1e-4f, $"방 {i} 수평");
                float roomC = room.Center(room.X0);
                Assert.Greater(pad.Rect.X0, room.X0); Assert.Less(pad.Rect.X1, room.X1);
                Assert.Greater(pad.Rect.Y0, roomC - 2.3f); Assert.Less(pad.Rect.Y1, roomC + 2.3f);
                float boost = LOP.FlappyDashCurve.Distance(P.Forward, pad.Duration, DashDuration, DashMult, P.Tick);
                Assert.Less(pad.Rect.X0 + boost + NoseHalfLength, f.X1, $"패드 {i} 부스트가 칸막이 안");
            }
        }

        [Test]
        public void 롤러코스터는_30_m_물결_한_번_틈_4_4()
        {
            var c = Layout();
            float x = c.Sections.Single(s => s.Name == "레일 롤러코스터").X;
            MineTube t = c.Tubes.Single(q => Math.Abs(q.X0 - x) < 0.01f);
            Assert.AreEqual(4.4f, t.Gap, 1e-5f);
            Assert.AreEqual(x + 30f, t.X1, 0.01f);
            Assert.AreEqual(c.CenterAt(t.X1 + 1f), t.Center(t.X1), 1e-3f, "끝 높이가 기준선 — 다음 관문과 잇는다");
        }

        [Test]
        public void Validate는_통로_밖으로_흔들리는_승강기를_잡는다()
        {
            var c = Layout();
            var lifts = c.Lifts.ToList();
            MineLift l = lifts[0];
            lifts[0] = new MineLift(l.Gate, 10f, l.Period, l.Phase);
            c.Lifts = lifts;
            string msg = MineCourseRule.Validate(c);
            Assert.IsNotNull(msg);
            Assert.GreaterOrEqual(msg.IndexOf("승강기", StringComparison.Ordinal), 0, msg);
        }

        [Test]
        public void 통로_높이()
        {
            var c = Layout();
            //  10-12 사람 판 레이아웃 변경: 낮은 천장이 138.2 ~ 162.2(진폭 2.5)로, 깊은 갱이 299.45 ~ 309.45로 밀려
            //  표본 x를 옮겼다(130 → 150: 낮은 천장 안, 300 → 330: 깊은 갱 바닥 긴 통로).
            //  10-13 동적 지형: 낮은 천장이 빠져 150 → 170(진자 광차의 평평한 통로), 380은 굴뚝 한가운데가 되어 430(마지막 갈림길 뒤).
            //  10-24 낙석·진자를 늘려 낙석 뒤는 +13.5, 진자 뒤는 +33.5 밀었다(170 → 183.5, 200 → 233.5, 330 → 363.5, 430 → 463.5).
            var expect = new (float x, float center, float half)[]
            {
                (0f, 0f, 7.28f), (100f, -49f, 7.28f), (183.5f, -49f, 7.28f), (233.5f, -49f, 7.28f), (363.5f, -73f, 7.28f), (463.5f, -49f, 7.28f),
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
            Assert.AreEqual(3.1f, tube.Gap, 1e-5f);   // 10-12 사람 판 레이아웃 변경(3.1 → 3.4), 10-13 동적 지형: 3.4 → 2.8, 10-15 폰 사람 판: 2.8 → 3.1
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
        public void 광산엔_알갱이_무더기를_놓지_않는다()
        {
            //  spec 2026-10-23-flappy-dive-charge: 광산은 다이브 충전 맵 — 금빛 보너스도 뺐다(사용자 10-23).
            Assert.AreEqual(0, Layout().Motes.Count);
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
