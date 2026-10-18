using NUnit.Framework;
using UnityEngine;
using LOP.UI;

namespace LOP.Tests
{
    /// <summary>
    /// 조준점 둘레의 당김 고리. 당김(0~1)·시위가 걸리는 임계치·손떨림 크기만 넣으면 단계·채움·색이 정해진다.
    /// 그림 그리는 쪽은 이 값을 칠하기만 하므로 "언제 흰색이고 언제 노란색인가"는 여기서 지킨다.
    /// </summary>
    public class ArcheryDrawRingTests
    {
        private const float Threshold = 0.3f;

        [Test]
        public void 활을_안_들면_고리가_없다()
        {
            Assert.AreEqual(ArcheryDrawRing.Phase.Hidden, ArcheryDrawRing.PhaseOf(false, 0.8f, Threshold));
        }

        [Test]
        public void 임계치_아래는_충전_중()
        {
            Assert.AreEqual(ArcheryDrawRing.Phase.Charging, ArcheryDrawRing.PhaseOf(true, 0f, Threshold));
            Assert.AreEqual(ArcheryDrawRing.Phase.Charging, ArcheryDrawRing.PhaseOf(true, 0.29f, Threshold));
        }

        [Test]
        public void 임계치부터는_쏠_수_있음()
        {
            Assert.AreEqual(ArcheryDrawRing.Phase.Armed, ArcheryDrawRing.PhaseOf(true, 0.3f, Threshold));
            Assert.AreEqual(ArcheryDrawRing.Phase.Armed, ArcheryDrawRing.PhaseOf(true, 0.99f, Threshold));
        }

        [Test]
        public void 끝까지_당기면_만작()
        {
            Assert.AreEqual(ArcheryDrawRing.Phase.Full, ArcheryDrawRing.PhaseOf(true, 1f, Threshold));
        }

        [Test]
        public void 채움은_당김을_0과_1_사이로_자른다()
        {
            Assert.AreEqual(0f, ArcheryDrawRing.FillOf(-0.2f));
            Assert.AreEqual(0.5f, ArcheryDrawRing.FillOf(0.5f));
            Assert.AreEqual(1f, ArcheryDrawRing.FillOf(1.3f));
        }

        [Test]
        public void 충전_중은_흐린_흰색_쏠_수_있으면_진한_흰색()
        {
            Color faint = ArcheryDrawRing.ColorOf(ArcheryDrawRing.Phase.Charging, 0f);
            Color solid = ArcheryDrawRing.ColorOf(ArcheryDrawRing.Phase.Armed, 0f);
            //  둘 다 흰색이고, 흐린 쪽이 눈에 띄게 더 투명하다.
            Assert.AreEqual(1f, faint.r, 1e-4f); Assert.AreEqual(1f, faint.g, 1e-4f); Assert.AreEqual(1f, faint.b, 1e-4f);
            Assert.AreEqual(1f, solid.r, 1e-4f); Assert.AreEqual(1f, solid.g, 1e-4f); Assert.AreEqual(1f, solid.b, 1e-4f);
            Assert.Less(faint.a, 0.6f);
            Assert.Greater(solid.a, 0.85f);
        }

        [Test]
        public void 만작은_노란색()
        {
            Color full = ArcheryDrawRing.ColorOf(ArcheryDrawRing.Phase.Full, 0f);
            Assert.AreEqual(ArcheryDrawRing.Yellow, full);
            //  #FFE14D 근처 — 빨강·초록이 높고 파랑이 낮다.
            Assert.Greater(full.r, 0.95f);
            Assert.Greater(full.g, 0.8f);
            Assert.Less(full.b, 0.4f);
        }

        [Test]
        public void 손이_떨릴수록_만작_색이_빨강으로_간다()
        {
            Color half = ArcheryDrawRing.ColorOf(ArcheryDrawRing.Phase.Full, 0.5f);
            Color all = ArcheryDrawRing.ColorOf(ArcheryDrawRing.Phase.Full, 1f);
            Assert.AreEqual(ArcheryDrawRing.Red, all);
            //  비례한다 — 절반이면 노랑과 빨강의 한가운데.
            Color mid = Color.Lerp(ArcheryDrawRing.Yellow, ArcheryDrawRing.Red, 0.5f);
            Assert.AreEqual(mid.g, half.g, 1e-4f);
            Assert.Less(all.g, half.g);
            Assert.Less(half.g, ArcheryDrawRing.Yellow.g);
        }

        [Test]
        public void 손떨림은_만작_전_흰색을_물들이지_않는다()
        {
            Assert.AreEqual(ArcheryDrawRing.ColorOf(ArcheryDrawRing.Phase.Armed, 0f),
                            ArcheryDrawRing.ColorOf(ArcheryDrawRing.Phase.Armed, 1f));
        }

        [Test]
        public void 손떨림이_없는_코스는_오래_잡아도_0()
        {
            var config = Config(freeSeconds: 0f, rampSeconds: 2.5f, maxDegrees: 0f, baseRatio: 0.4f);
            Assert.AreEqual(0f, ArcheryDrawRing.ShakeLevel(10f, config));
        }

        [Test]
        public void 손떨림은_여유_시간_동안_0이고_그_뒤로_비례해_1까지()
        {
            var config = Config(freeSeconds: 1f, rampSeconds: 2f, maxDegrees: 0.6f, baseRatio: 0.4f);
            //  잡자마자 깔리는 기준선(baseRatio)은 "오래 버틴 대가"가 아니다 — 0으로 친다.
            Assert.AreEqual(0f, ArcheryDrawRing.ShakeLevel(0f, config), 1e-4f);
            Assert.AreEqual(0f, ArcheryDrawRing.ShakeLevel(1f, config), 1e-4f);
            Assert.AreEqual(0.5f, ArcheryDrawRing.ShakeLevel(2f, config), 1e-4f);
            Assert.AreEqual(1f, ArcheryDrawRing.ShakeLevel(3f, config), 1e-4f);
            Assert.AreEqual(1f, ArcheryDrawRing.ShakeLevel(9f, config), 1e-4f);
        }

        [Test]
        public void 만작에_막_닿은_때만_번쩍인다()
        {
            Assert.IsTrue(ArcheryDrawRing.JustFull(ArcheryDrawRing.Phase.Armed, ArcheryDrawRing.Phase.Full));
            Assert.IsTrue(ArcheryDrawRing.JustFull(ArcheryDrawRing.Phase.Hidden, ArcheryDrawRing.Phase.Full));
            Assert.IsFalse(ArcheryDrawRing.JustFull(ArcheryDrawRing.Phase.Full, ArcheryDrawRing.Phase.Full));
            Assert.IsFalse(ArcheryDrawRing.JustFull(ArcheryDrawRing.Phase.Charging, ArcheryDrawRing.Phase.Armed));
        }

        [Test]
        public void 번쩍임은_짧게_한_번_줄어든다()
        {
            Assert.AreEqual(1f, ArcheryDrawRing.FlashAmount(0f), 1e-4f);
            Assert.Greater(ArcheryDrawRing.FlashAmount(ArcheryDrawRing.FlashSeconds * 0.5f), 0f);
            Assert.AreEqual(0f, ArcheryDrawRing.FlashAmount(ArcheryDrawRing.FlashSeconds), 1e-4f);
            Assert.AreEqual(0f, ArcheryDrawRing.FlashAmount(5f));
            //  아직 만작에 안 닿았으면(음수) 번쩍이지 않는다.
            Assert.AreEqual(0f, ArcheryDrawRing.FlashAmount(-1f));
        }

        private static ArcheryConfig Config(float freeSeconds, float rampSeconds, float maxDegrees, float baseRatio)
        {
            return new ArcheryConfig(
                wavePeriodTicks: 0, minTargets: 0, maxTargets: 0,
                spawnRadius: 16f, spawnMinY: 3.5f, spawnMaxY: 13f, minSeparation: 0f,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: freeSeconds, shakeRampSeconds: rampSeconds, shakeMaxDegrees: maxDegrees,
                riseHeightMin: 0.5f, riseHeightMax: 1.5f, staggerTicks: 0, restTicks: 0,
                kinds: new ArcheryTargetKind[0], courseKind: ArcheryCourseKind.ShootOff,
                shakeBaseRatio: baseRatio);
        }
    }
}
