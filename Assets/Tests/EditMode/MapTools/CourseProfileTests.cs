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

        static CourseProfile Compose(ulong seed = 20260919UL)
            => CourseProfileRule.Compose(StartX, Length, Spacing, Half, Window, seed, LeadIn, Tail);

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
                Assert.AreEqual(r.Y1, p.CenterAt(r.X0) + Half, 1e-3f, "입구");
                Assert.AreEqual(r.Y1, p.CenterAt(r.X1) + Half, 1e-3f, "출구");
                Assert.AreEqual(Window, r.Y1 - r.Y0, 1e-4f, "세로 폭 = 평범한 틈");
            }
        }

        [Test]
        public void 혀는_지름길_아래에_있고_두께가_최소_이상이다()
        {
            foreach (ShortcutRect r in Compose().Shortcuts)
            {
                float[] t = r.Tongue;
                Assert.AreEqual(8, t.Length);
                Assert.AreEqual(r.Y0, t[1], 1e-4f);                       // 윗변 = 지름길 바닥
                Assert.AreEqual(r.Y0, t[7], 1e-4f);
                Assert.GreaterOrEqual(r.Y0 - t[3], CourseProfileRule.MinTongue - 1e-4f);
                Assert.Greater(t[0], r.X0);                                // 혀는 지름길 안쪽
                Assert.Less(t[6], r.X1);
            }
        }

        [Test]
        public void 얕은_U에는_지름길을_못_낸다()
        {
            //  깊이 절반에 창을 뚫으면 혀가 남아야 한다. 20m면 혀가 없다.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 20f, 1.3f, Half, Window));
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
            Assert.IsFalse(p.GateAllowedAt((r.X0 + r.Tongue[2]) * 0.5f, CourseProfileRule.GateMargin));
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
    }
}
