using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 조각을 이어 붙인 코스. <b>새가 실제로 따라갈 수 있는 모양인가</b>가 이 테스트의 전부다 —
    /// 오르막은 탭 빈도가, 내리막은 최대 낙하 속도가 한계를 정한다(spec §2).
    /// </summary>
    public class CourseProfileTests
    {
        const float StartX = 0f;
        const float Length = 612f;          // 90초 × 6.8m/s
        const float Spacing = 11.4f;
        const float Half = 10.92f;          // 카메라 30m · FOV 40의 화면 세로 절반
        const float Window = 4.37f;
        const float LeadIn = Spacing * 4f;
        const float Tail = Spacing * 8f;

        //  FlappyConfig 값(날갯짓 18.6 · 중력 59 · 전진 6.8 · 틱 0.02). 시험은 숫자를 직접 쓴다.
        static readonly FlapArc Arc = new FlapArc(18.6f, 59f, 6.8f, 0.02f);
        const float MaxFall = 30f;

        static CourseProfile Compose(ulong seed = 20260919UL)
            => CourseProfileRule.Compose(StartX, Length, Spacing, Half, Window, seed, LeadIn, Tail, Arc);

        [Test]
        public void 출발점은_높이_0이고_앞뒤_여유까지_덮는다()
        {
            var p = Compose();
            Assert.AreEqual(0f, p.CenterAt(StartX), 1e-4f);
            Assert.AreEqual(StartX - LeadIn, p.X(0), 1e-3f);
            Assert.AreEqual(StartX + Length + Tail, p.X(p.VertexCount - 1), 1e-2f);
        }

        [Test]
        public void 경사는_물리_한계_안이다()
        {
            //  생산 코드 상수(DropSlope, Sections[].RiseSlope)와 비교하면 그 상수를 올려도 테스트가
            //  못 잡는다(자기참조). 그래서 여기선 spec의 리터럴 값으로 직접 비교한다.
            //  내리막: spec 값 2.5, 그리고 최대 낙하 30m/s ÷ 전진 6.8m/s = 4.4가 물리 한계.
            //  오르막: spec §3 표의 구간별 값(1.0/1.3/1.5), 그리고 1.5를 넘으면 초당 3.5탭을 넘고
            //  2.0이면 초당 6탭이 넘어 못 따라간다 — 그 절대 한계도 함께 건다.
            const float dropSlopeSpec = 2.5f;
            const float dropSlopePhysicsCeiling = 30f / 6.8f;
            var riseSlopeSpec = new[] { 1.0f, 1.3f, 1.5f };
            const float riseSlopeAbsoluteCeiling = 1.5f;

            var p = Compose();
            float sectionLen = Length / CourseProfileRule.Sections.Length;
            for (int i = 1; i < p.VertexCount; i++)
            {
                float dx = p.X(i) - p.X(i - 1);
                float dy = p.Y(i) - p.Y(i - 1);
                Assert.Greater(dx, 0f, $"{i}번째 꼭짓점이 뒤로 갔다");
                float slope = dy / dx;
                if (slope < 0f)
                {
                    Assert.LessOrEqual(-slope, dropSlopeSpec + 1e-3f, $"x={p.X(i):F1} 내리막(spec)");
                    Assert.LessOrEqual(-slope, dropSlopePhysicsCeiling + 1e-3f, $"x={p.X(i):F1} 내리막(물리 한계)");
                }
                else if (slope > 0f)
                {
                    int s = System.Math.Min(CourseProfileRule.Sections.Length - 1,
                                            (int)((p.X(i - 1) - StartX) / sectionLen));
                    Assert.LessOrEqual(slope, riseSlopeSpec[s] + 1e-3f,
                                       $"x={p.X(i):F1} 오르막(구간 {s + 1}, spec)");
                    Assert.LessOrEqual(slope, riseSlopeAbsoluteCeiling + 1e-3f,
                                       $"x={p.X(i):F1} 오르막(절대 한계)");
                }
            }
        }

        [Test]
        public void 평지는_모두_한_칸_이상이다()
        {
            //  벽 뒤에 바로 관문이 오지 않게 조각 사이 평지를 남긴다. A 꼭대기(12m)도 한 칸보다 길다.
            foreach (FlatSpan f in Compose().Flats)
            {
                Assert.GreaterOrEqual(f.Length, Spacing - 1e-3f, $"평지 {f.From:F1}~{f.To:F1}");
            }
        }

        [Test]
        public void 높낮이가_실제로_크게_바뀐다()
        {
            var p = Compose();
            Assert.GreaterOrEqual(p.MaxY - p.MinY, 40f, "U 40m가 들어가야 한다");
        }

        [Test]
        public void 지름길은_구간_2와_3에_하나씩이다()
        {
            var shortcuts = Compose().Shortcuts;
            Assert.AreEqual(2, shortcuts.Count);
            float sectionLen = Length / 3f;
            Assert.That(shortcuts[0].X0, Is.GreaterThan(StartX + sectionLen).And.LessThan(StartX + 2 * sectionLen));
            Assert.That(shortcuts[1].X0, Is.GreaterThan(StartX + 2 * sectionLen));
        }

        [Test]
        public void 지름길_입구와_출구에서_천장이_지름길_윗면과_만난다()
        {
            //  입구·출구는 "천장선이 지름길 윗면을 지나는 곳"으로 정의된다. 여기가 어긋나면
            //  위쪽 상자와 경사 조각 사이에 틈이 생기거나 겹친다.
            var p = Compose();
            foreach (ShortcutRect r in p.Shortcuts)
            {
                float mouthTop = r.ChannelCenterAt(r.X0) + r.Entrance.Thickness * 0.5f;
                Assert.AreEqual(mouthTop, p.CenterAt(r.X0) + Half, 1e-3f, "입구 = 천장이 굴 윗면을 지나는 곳");
                Assert.AreEqual(r.Y1, p.CenterAt(r.X1) + Half, 1e-3f, "출구");
                Assert.AreEqual(Window, r.Y1 - r.Y0, 1e-4f, "세로 폭 = 평범한 틈");
            }
        }

        [Test]
        public void 얕은_U에는_지름길을_못_낸다()
        {
            //  깊이 절반에 창을 뚫으면 혀가 남아야 한다. 20m면 혀가 없다.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 20f, 1.3f, Half, Window, Easy, Arc));
        }

        [Test]
        public void 관문은_평지에서만_끝으로부터_여유를_두고_선다()
        {
            var p = Compose();
            foreach (FlatSpan f in p.Flats)
            {
                Assert.IsTrue(p.GateAllowedAt((f.From + f.To) * 0.5f, CourseProfileRule.GateMargin));
                Assert.IsFalse(p.GateAllowedAt(f.From + 1f, CourseProfileRule.GateMargin),
                               $"평지 {f.From:F1} 시작 1m 안쪽에 관문이 선다");
            }
            //  U 벽 한가운데에는 서면 안 된다.
            ShortcutRect r = p.Shortcuts[0];
            Assert.IsFalse(p.GateAllowedAt((r.X0 + r.ValleyBottom0) * 0.5f, CourseProfileRule.GateMargin));
        }

        [Test]
        public void 같은_씨앗은_같은_코스_다른_씨앗은_다른_코스()
        {
            var a = Compose(1UL); var b = Compose(1UL); var c = Compose(2UL);
            Assert.AreEqual(a.VertexCount, b.VertexCount);
            for (int i = 0; i < a.VertexCount; i++) { Assert.AreEqual(a.X(i), b.X(i)); Assert.AreEqual(a.Y(i), b.Y(i)); }
            bool differs = a.VertexCount != c.VertexCount;
            for (int i = 0; differs == false && i < a.VertexCount; i++) { differs = a.X(i) != c.X(i); }
            Assert.IsTrue(differs);
        }

        [Test]
        public void 바닥_조각은_끊김_없이_꺾은선을_그대로_덮는다()
        {
            var p = Compose();
            var pieces = CourseProfileRule.FloorPieces(p, new[] { 204f, 408f });
            Assert.AreEqual(p.X(0), pieces[0].X0, 1e-3f);
            for (int i = 0; i < pieces.Count; i++)
            {
                Assert.AreEqual(p.CenterAt(pieces[i].X0), pieces[i].Lift0, 1e-3f);
                Assert.AreEqual(p.CenterAt(pieces[i].X1), pieces[i].Lift1, 1e-3f);
                if (i > 0) { Assert.AreEqual(pieces[i - 1].X1, pieces[i].X0, 1e-4f, "끊김"); }
            }
            Assert.AreEqual(p.X(p.VertexCount - 1), pieces[pieces.Count - 1].X1, 1e-3f);
            //  구간 경계에서 끊겨야 색이 바뀐다.
            Assert.IsTrue(pieces.Exists(q => System.Math.Abs(q.X0 - 204f) < 1e-3f));
        }

        [Test]
        public void 천장_조각은_지름길_구간을_도려낸다()
        {
            var p = Compose();
            var pieces = CourseProfileRule.CeilingPieces(p, new float[0]);
            foreach (ShortcutRect r in p.Shortcuts)
            {
                foreach (RampPiece q in pieces)
                {
                    float mid = (q.X0 + q.X1) * 0.5f;
                    Assert.IsFalse(mid > r.X0 && mid < r.X1, $"x={mid:F1} 천장 조각이 지름길을 막는다");
                }
                Assert.IsTrue(pieces.Exists(q => System.Math.Abs(q.X1 - r.X0) < 1e-3f), "입구에서 끝나는 조각");
                Assert.IsTrue(pieces.Exists(q => System.Math.Abs(q.X0 - r.X1) < 1e-3f), "출구에서 시작하는 조각");
            }
        }

        static readonly ShortcutEntrance Easy = new ShortcutEntrance(1, 4.35f, 4f);
        static readonly ShortcutEntrance Hard = new ShortcutEntrance(3, 3.6f, 6f);

        [Test]
        public void 날갯짓_호는_틱_궤적의_숫자다()
        {
            Assert.AreEqual(32, Arc.TicksPerArc);
            Assert.AreEqual(4.352f, Arc.Span, 1e-3f);
            Assert.AreEqual(0.198f, Arc.RisePerArc, 2e-3f);
            Assert.AreEqual(3.12f, Arc.Apex, 1e-2f);
        }

        [Test]
        public void 호_높이는_진짜_커널을_틱마다_돌린_높이와_같다()
        {
            //  떨어지는 중(−12m/s)에 첫 틱에 친다 — 날갯짓은 세로 속도를 덮어쓰므로 그 전 속도는 상관없다.
            float vy = -12f, y = 0f;
            for (int n = 1; n <= Arc.TicksPerArc; n++)
            {
                vy = FlappyTickMath.NextVerticalSpeed(vy, n == 1, 18.6f, 59f, MaxFall, 0.02f);
                y = FlappyTickMath.AdvanceHeight(y, vy, 0.02f);
                Assert.AreEqual(y, Arc.HeightAt(n * 6.8f * 0.02f), 1e-3f, $"{n}틱");
            }
        }

        [Test]
        public void 구간마다_입구_난이도가_다르다()
        {
            Assert.IsFalse(CourseProfileRule.Sections[0].ValleyShortcut);
            ShortcutEntrance s2 = CourseProfileRule.Sections[1].Entrance;
            ShortcutEntrance s3 = CourseProfileRule.Sections[2].Entrance;
            Assert.AreEqual((1, 4.35f, 4f), (s2.Arcs, s2.Thickness, s2.Lip), "구간 2 = 쉬운 굴");
            Assert.AreEqual((3, 3.6f, 6f), (s3.Arcs, s3.Thickness, s3.Lip), "구간 3 = 어려운 굴");
        }

        [Test]
        public void 입구_굴_가운데선은_호마다_날갯짓한_새의_틱_궤적이다()
        {
            var arcs = new List<int>();
            foreach (ShortcutRect r in Compose().Shortcuts)
            {
                arcs.Add(r.Entrance.Arcs);
                float vy = -12f, y = r.CenterY;
                int ticks = r.Entrance.Arcs * r.Arc.TicksPerArc;
                for (int n = 0; n < ticks; n++)
                {
                    vy = FlappyTickMath.NextVerticalSpeed(vy, n % r.Arc.TicksPerArc == 0, 18.6f, 59f, MaxFall, 0.02f);
                    y = FlappyTickMath.AdvanceHeight(y, vy, 0.02f);
                    //  x를 누적이 아니라 매번 새로 계산한다 — 누적하면 뜬 오차가 쌓여(2026-09-25
                    //  리뷰에서 실측 x0≈519·62틱에 0.85mm) 채널 끝 근처의 높이 비교가 어긋난다.
                    float x = r.X0 + (n + 1) * 6.8f * 0.02f;
                    Assert.AreEqual(y, r.ChannelCenterAt(x), 2e-3f, $"x0={r.X0:F0} {n + 1}틱");
                }
                Assert.AreEqual(r.ChannelEnd, r.X0 + ticks * 6.8f * 0.02f, 1e-3f, "굴은 마지막 호에서 끝난다");
            }
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, arcs, "쉬운 굴 하나, 어려운 굴 하나");
        }

        [Test]
        public void 호가_너무_많거나_턱이_너무_길거나_굴이_창보다_넓으면_던진다()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 30f, 1.3f, Half, Window, new ShortcutEntrance(10, 4.35f, 4f), Arc),
                "굴이 출구를 넘는다");
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 30f, 1.3f, Half, Window, new ShortcutEntrance(1, 4.35f, 20f), Arc),
                "턱 아래 계곡이 막힌다");
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 30f, 1.3f, Half, Window, new ShortcutEntrance(1, 5f, 4f), Arc),
                "굴이 곧은 길보다 넓다");
        }

        static bool Inside(List<float[]> strips, float x, float y)
        {
            foreach (float[] s in strips)
            {
                if (x < s[0] || x > s[2]) { continue; }
                float t = (x - s[0]) / (s[2] - s[0]);
                float bottom = s[1] + (s[3] - s[1]) * t;
                float top = s[7] + (s[5] - s[7]) * t;
                if (y >= bottom && y <= top) { return true; }
            }
            return false;
        }

        static IEnumerable<ShortcutRect> BothTiers()
        {
            yield return CourseProfileRule.ValleyShortcut(100f, 0f, 30f, 1.3f, Half, Window, Easy, Arc);
            yield return CourseProfileRule.ValleyShortcut(100f, 0f, 40f, 1.5f, Half, Window, Hard, Arc);
        }

        [Test]
        public void 띠는_세로변이고_빈틈없이_이어진다()
        {
            foreach (ShortcutRect r in BothTiers())
            {
                List<float[]> roof = CourseProfileRule.ShortcutRoof(r, 1f, 0.25f);
                List<float[]> tongue = CourseProfileRule.ShortcutTongue(r, 0.25f);
                foreach (var (name, strips, end) in new[] { ("지붕", roof, r.X1), ("혀", tongue, r.TongueEnd) })
                {
                    Assert.AreEqual(r.X0, strips[0][0], 1e-4f, $"{name}는 입구에서 시작");
                    Assert.AreEqual(end, strips[strips.Count - 1][2], 1e-4f, $"{name} 끝");
                    for (int i = 0; i < strips.Count; i++)
                    {
                        float[] s = strips[i];
                        Assert.AreEqual(s[0], s[6], 1e-6f, $"{name} {i} 왼변 세로");
                        Assert.AreEqual(s[2], s[4], 1e-6f, $"{name} {i} 오른변 세로");
                        Assert.Less(s[0], s[2], $"{name} {i} 폭");
                        Assert.GreaterOrEqual(s[7], s[1] - 1e-4f, $"{name} {i} 왼쪽 위≥아래");
                        Assert.GreaterOrEqual(s[5], s[3] - 1e-4f, $"{name} {i} 오른쪽 위≥아래");
                        if (i > 0) { Assert.AreEqual(strips[i - 1][2], s[0], 1e-5f, $"{name} {i} 이음새"); }
                    }
                }
            }
        }

        [Test]
        public void 얇은_조각이_생기지_않는다()
        {
            //  step 자르기가 굴 끝·호 경계 바로 옆에 겹치면 폭 1cm 미만인 조각이 생긴다 —
            //  그런 조각을 메시 콜라이더로 구우면(Task 2) 판정면이 쉽게 깨진다. 혀의 마지막
            //  조각만 빼는 이유는 TongueEnd에서 위·아래가 만나 삼각형이 되는 자리라 폭이
            //  구조적으로 짧을 수 있어서다(다른 조각과 달리 step 겹침 문제가 아니다).
            foreach (ShortcutRect r in BothTiers())
            {
                List<float[]> roof = CourseProfileRule.ShortcutRoof(r, 1f, 0.25f);
                foreach (float[] s in roof)
                {
                    Assert.GreaterOrEqual(s[2] - s[0], 0.01f, $"x0={r.X0:F0} 지붕 조각");
                }
                List<float[]> tongue = CourseProfileRule.ShortcutTongue(r, 0.25f);
                for (int i = 0; i < tongue.Count - 1; i++)
                {
                    Assert.GreaterOrEqual(tongue[i][2] - tongue[i][0], 0.01f, $"x0={r.X0:F0} 혀 조각 {i}");
                }
            }
        }

        [Test]
        public void 굴은_열려_있고_바로_위는_지붕_바로_아래는_혀다()
        {
            foreach (ShortcutRect r in BothTiers())
            {
                List<float[]> roof = CourseProfileRule.ShortcutRoof(r, 1f, 0.25f);
                List<float[]> tongue = CourseProfileRule.ShortcutTongue(r, 0.25f);
                float half = r.Entrance.Thickness * 0.5f;
                for (float x = r.X0 + 0.01f; x < r.ChannelEnd - 0.01f; x += 0.05f)
                {
                    float c = r.ChannelCenterAt(x);
                    string at = $"x0={r.X0:F0} x={x:F2}";
                    //  윗면·바닥 5cm 안쪽까지 비어 있어야 한다 — 띠가 호 경계(꺾인 점)를 걸치면 여기서 걸린다.
                    Assert.IsFalse(Inside(roof, x, c + half - 0.05f), $"{at} 굴 윗부분이 지붕에 먹혔다");
                    Assert.IsFalse(Inside(tongue, x, c - half + 0.05f), $"{at} 굴 바닥이 혀에 먹혔다");
                    Assert.IsTrue(Inside(roof, x, c + half + 0.05f), $"{at} 굴 위가 비었다");
                    Assert.IsTrue(Inside(tongue, x, c - half - 0.05f), $"{at} 굴 아래가 비었다");
                }
                for (float x = r.ChannelEnd + 0.01f; x < r.TongueEnd - 0.1f; x += 0.05f)
                {
                    string at = $"x0={r.X0:F0} 곧은 길 x={x:F2}";
                    Assert.IsFalse(Inside(roof, x, r.CenterY) || Inside(tongue, x, r.CenterY), $"{at} 막혔다");
                    Assert.IsTrue(Inside(roof, x, r.Y1 + 0.05f), $"{at} 위가 비었다");
                    Assert.IsTrue(Inside(tongue, x, r.Y0 - 0.05f), $"{at} 아래가 비었다");
                }
            }
        }

        [Test]
        public void 입구_턱이_막고_턱_아래는_열려_있다()
        {
            foreach (ShortcutRect r in BothTiers())
            {
                List<float[]> tongue = CourseProfileRule.ShortcutTongue(r, 0.25f);
                Assert.IsTrue(Inside(tongue, r.X0 + 0.05f, r.LipBottom + 0.2f), $"x0={r.X0:F0} 턱이 비었다");
                Assert.IsFalse(Inside(tongue, r.X0 + 0.05f, r.LipBottom - 0.5f), $"x0={r.X0:F0} 턱 아래가 막혔다");
            }
        }
    }
}
