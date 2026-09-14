using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// "증명된 경로가 날개가 쓸고 가는 원반 안으로 들어가는가"의 <b>기하</b>를 못박는다.
    /// 씬도 물리도 안 쓴다 — 여기서 재는 것은 규칙이지 맵이 아니다.
    /// </summary>
    public class SweptDiscGapTests
    {
        //  실제 Flappy 몸. 높이가 지름과 같아 캡슐 축이 점 하나로 줄어든다 — 그래서 축 길이를
        //  지키는 단언에는 아래의 긴 몸(TallHeight)을 따로 쓴다.
        const float BodyRadius = 0.45f;
        const float BodyHeight = 0.9f;
        //  축 길이가 0이 아닌 몸. 절반(1.0) − 반지름(0.45) = 0.55가 축의 반길이다.
        const float TallHeight = 2.0f;

        //  이진수로 정확히 떨어지는 값들 — 경계(여유 = 정확히 0)를 반올림 오차 없이 재려면
        //  0.45 같은 값으로는 안 된다.
        const float ExactRadius = 0.5f;
        const float ExactHeight = 1.0f;

        static List<Vector3> Path(params Vector3[] points) => new List<Vector3>(points);

        static ObstaclePlacement Disc(string name, float x, float y, float radius)
            => new ObstaclePlacement(name, x, y, radius, bandAbove: 9f, bandBelow: 9f, measured: true);

        static SweptDiscVerdict Judge(IReadOnlyList<Vector3> path, float bodyRadius, float bodyHeight,
                                      params ObstaclePlacement[] discs)
            => SweptDiscRule.Judge(SweptDiscRule.MeasureAll(path, bodyRadius, bodyHeight, discs));

        static bool Has(string text, string needle) => text.IndexOf(needle, StringComparison.Ordinal) >= 0;

        [Test]
        public void 여유는_거리에서_원반_반지름과_몸_반지름을_뺀_값이다()
        {
            //  몸 가운데 (0,0), 원반 중심 (10,0) 반지름 2 → 10 − 2 − 0.45.
            SweptDiscGap gap = SweptDiscRule.Measure("W", Path(new Vector3(0f, 0f, 0f)),
                                                     BodyRadius, BodyHeight, 10f, 0f, 2f);

            Assert.IsTrue(gap.Measured);
            Assert.AreEqual(7.55f, gap.Gap, 1e-4f);
        }

        [Test]
        public void 몸을_점으로_재면_틀린다_캡슐_축만큼_가깝다()
        {
            //  원반이 <b>바로 위</b>에 있으면 축 길이가 그대로 답에 들어간다: 축 위끝이
            //  0.55만큼 먼저 나와 있으므로 (10 − 0.55) − 2 − 0.45 = 7.00이다.
            //  몸을 점으로 보는 구현은 7.55를 내서 여기서 빨강이 된다.
            SweptDiscGap gap = SweptDiscRule.Measure("W", Path(new Vector3(0f, 0f, 0f)),
                                                     BodyRadius, TallHeight, 0f, 10f, 2f);

            Assert.AreEqual(7.00f, gap.Gap, 1e-4f);
        }

        [Test]
        public void 여유가_딱_0이면_회전_무관이다()
        {
            //  거리 2.5 = 원반 2.0 + 몸 0.5 → 여유 0. 원반에 닿지 않고 스치는 자리다.
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight,
                                             Disc("W", 2.5f, 0f, 2f));

            Assert.AreEqual(0f, verdict.Worst.Gap, 1e-6f);
            Assert.IsTrue(verdict.PhaseIndependent);
        }

        [Test]
        public void 아주_조금이라도_파고들면_틱0_자세에서만이다()
        {
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight,
                                             Disc("W", 2.499f, 0f, 2f));

            Assert.Less(verdict.Worst.Gap, 0f);
            Assert.IsFalse(verdict.PhaseIndependent);
        }

        [Test]
        public void 팔이_길어지면_무관이_깨진다()
        {
            var path = Path(new Vector3(0f, 0f, 0f));

            Assert.IsTrue(Judge(path, ExactRadius, ExactHeight, Disc("W", 2.5f, 0f, 2.0f)).PhaseIndependent);
            //  팔을 0.1 늘렸을 뿐인데 판정이 뒤집혀야 한다 — 안 뒤집히면 팔 길이를 안 보는 것이다.
            Assert.IsFalse(Judge(path, ExactRadius, ExactHeight, Disc("W", 2.5f, 0f, 2.1f)).PhaseIndependent);
        }

        [Test]
        public void 경로_중간_한_점만_원반에_들어가도_잡힌다()
        {
            //  끝점 둘은 원반에서 50m 위다 — 끝점만 보는 구현은 +49m대의 여유를 낸다.
            var path = Path(new Vector3(0f, 50f, 0f), new Vector3(5f, 50f, 0f),
                            new Vector3(10f, 0f, 0f),
                            new Vector3(15f, 50f, 0f), new Vector3(20f, 50f, 0f));

            SweptDiscVerdict verdict = Judge(path, ExactRadius, ExactHeight, Disc("W", 10f, 0f, 1f));

            Assert.AreEqual(-1.5f, verdict.Worst.Gap, 1e-4f);
            Assert.IsFalse(verdict.PhaseIndependent);
        }

        [Test]
        public void 틱_사이로_지나가는_것도_잡는다()
        {
            //  두 틱 다 원반에서 5m 떨어져 있지만 그 사이 선분이 원반 한가운데를 지난다.
            //  점만 재는 구현은 +3.5m를 내서 "무관"이라고 잘못 말한다.
            var path = Path(new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f));

            SweptDiscVerdict verdict = Judge(path, ExactRadius, ExactHeight, Disc("W", 0f, 0f, 1f));

            Assert.AreEqual(-1.5f, verdict.Worst.Gap, 1e-4f);
            Assert.IsFalse(verdict.PhaseIndependent);
        }

        [Test]
        public void 가장_가까웠던_자리는_틱_사이일_수_있다()
        {
            //  두 틱은 x = 0과 10인데 가장 가까운 자리는 그 사이 x = 5다.
            var path = Path(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f));

            SweptDiscGap gap = SweptDiscRule.Measure("W", path, ExactRadius, ExactHeight, 5f, 20f, 1f);

            Assert.AreEqual(5f, gap.AtX, 1e-3f);
            Assert.AreEqual(0, gap.AtTick, "최소가 난 구간의 시작 틱이어야 한다.");
        }

        [Test]
        public void 원반을_못_잰_풍차가_있으면_무관이라고_말하지_않는다()
        {
            var unmeasured = new ObstaclePlacement("NoCollider", 5f, 0f, 0f, 0f, 0f, measured: false);
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight,
                                             Disc("W", 50f, 0f, 2f), unmeasured);

            Assert.AreEqual(1, verdict.DiscCount);
            Assert.AreEqual(1, verdict.Unmeasured);
            Assert.IsFalse(verdict.PhaseIndependent, "못 잰 것이 남았으면 보장이라고 말할 수 없다.");
            Assert.IsTrue(Has(SweptDiscRule.Line(verdict), "⛔"));
            Assert.IsTrue(Has(SweptDiscRule.Line(verdict), "판정 못 함"));
        }

        [Test]
        public void 파고든_것을_이미_봤으면_못_잰_것이_있어도_아니라고_말한다()
        {
            var unmeasured = new ObstaclePlacement("NoCollider", 5f, 0f, 0f, 0f, 0f, measured: false);
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight,
                                             Disc("W", 1f, 0f, 2f), unmeasured);

            //  "모르겠다"보다 "아니다"가 강하다 — ⛔가 아니라 ⚠️가 나와야 한다.
            string line = SweptDiscRule.Line(verdict);
            Assert.IsTrue(Has(line, "⚠️"));
            Assert.IsTrue(Has(line, "파고듦"));
            Assert.IsFalse(Has(line, "⛔"));
        }

        [Test]
        public void 풍차가_없으면_잴_것이_없고_문구도_안_나온다()
        {
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight);

            Assert.IsFalse(verdict.Measured);
            Assert.IsFalse(verdict.PhaseIndependent);
            Assert.AreEqual(string.Empty, SweptDiscRule.Line(verdict));
        }

        [Test]
        public void 경로가_없으면_못_잰_것으로_남는다()
        {
            Assert.IsFalse(SweptDiscRule.Measure("W", null, BodyRadius, BodyHeight, 0f, 0f, 2f).Measured);
            Assert.AreEqual(0, SweptDiscRule.MeasureAll(new List<Vector3>(), BodyRadius, BodyHeight,
                                                        new[] { Disc("W", 0f, 0f, 2f) }).Count);
        }

        [Test]
        public void 여유가_같으면_먼저_온_풍차를_고른다()
        {
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight,
                                             Disc("First", 10f, 0f, 2f), Disc("Second", -10f, 0f, 2f));

            Assert.AreEqual("First", verdict.Worst.Name);
        }

        [Test]
        public void 가장_좁은_풍차가_판정을_대표한다()
        {
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight,
                                             Disc("Far", 50f, 0f, 2f), Disc("Near", 4f, 0f, 2f));

            Assert.AreEqual("Near", verdict.Worst.Name);
            Assert.AreEqual(1.5f, verdict.Worst.Gap, 1e-4f);
            Assert.AreEqual(2, verdict.DiscCount);
        }

        [Test]
        public void 무관한_줄은_기호와_숫자를_같이_적는다()
        {
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f)), ExactRadius, ExactHeight,
                                             Disc("Gauntlet/Windmill", 4f, 0f, 2f));

            //  기호는 Ordinal로 찾는다 — StringAssert는 문화권 비교라 없는 기호에도 매칭된다.
            string line = SweptDiscRule.Line(verdict);
            Assert.IsTrue(Has(line, "🌀"));
            Assert.IsTrue(Has(line, "회전 무관"));
            Assert.IsTrue(Has(line, "풍차 1개"));
            Assert.IsTrue(Has(line, "최소 1.50m"));
            Assert.IsTrue(Has(line, "Gauntlet/Windmill"));
            Assert.IsFalse(Has(line, "⚠️"));
        }

        [Test]
        public void 설명문은_판정_글리프를_쓰지_않는다()
        {
            //  설명문은 자리별 판정과 무관하게 찍힌다 — 여기 ✅를 넣으면 "이 리포트에 ✅가 있다"가
            //  참이 되어 자리별 판정을 확인하는 다른 검사들이 통째로 공허해진다.
            string note = SweptDiscRule.Note();

            Assert.IsFalse(Has(note, "✅"));
            Assert.IsFalse(Has(note, "🟡"));
            Assert.IsFalse(Has(note, "❌"));
            Assert.IsTrue(Has(note, "🌀"));
        }

        [Test]
        public void 파고든_줄은_얼마나_파고들었는지를_적는다()
        {
            SweptDiscVerdict verdict = Judge(Path(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f)),
                                             ExactRadius, ExactHeight, Disc("Windmill_3", 5f, 0f, 2f));

            string line = SweptDiscRule.Line(verdict);
            Assert.IsTrue(Has(line, "틱 0 위상에서만"));
            Assert.IsTrue(Has(line, "Windmill_3"));
            //  거리 0 − 2 − 0.5 = −2.5 → 부호를 뒤집어 "2.50m 파고듦"으로 적는다.
            Assert.IsTrue(Has(line, "2.50m 파고듦"));
            Assert.IsFalse(Has(line, "-2.50"));
        }
    }
}
