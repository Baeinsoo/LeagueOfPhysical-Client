using NUnit.Framework;

namespace FlappyRace.Tests
{
    public class FlappyGapAimingTests
    {
        private const float Step = 0.5f;
        private const float BottomY = 0f;
        private const float BodyRadius = 0.45f;   // 실제 FlappyConfig 값 — 통과 가능 폭이 0.9m가 된다

        //  아래에서 위로: 0.0 ~ 4.5m. true = 막힘.
        private static bool[] Column(params bool[] blocked) => blocked;

        [Test]
        public void 막힌_구간_사이의_뚫린_구간을_찾는다()
        {
            //  0.0~0.5 막힘 / 1.0~2.5 뚫림(1.5m) / 3.0~ 막힘
            var column = Column(true, true, false, false, false, false, true, true);

            Assert.IsTrue(FlappyGapAiming.TryFindGap(column, BottomY, Step, currentY: 1.5f, BodyRadius,
                out float low, out float high));
            Assert.AreEqual(1.0f, low, 1e-4f);
            Assert.AreEqual(2.5f, high, 1e-4f);
        }

        [Test]
        public void 새가_못_지나가는_좁은_틈은_고르지_않는다()
        {
            //  뚫린 곳이 0.5m 한 칸뿐 — 몸(0.9m)이 안 들어간다.
            var column = Column(true, false, true, true, true, true);

            Assert.IsFalse(FlappyGapAiming.TryFindGap(column, BottomY, Step, currentY: 0.5f, BodyRadius,
                out _, out _));
        }

        [Test]
        public void 틈이_여럿이면_지금_높이에_가까운_쪽을_고른다()
        {
            //  아래 틈 0.5~1.5(중심 1.0) / 위 틈 3.0~4.0(중심 3.5)
            var column = Column(true, false, false, false, true, true, false, false, false, true);

            Assert.IsTrue(FlappyGapAiming.TryFindGap(column, BottomY, Step, currentY: 3.4f, BodyRadius,
                out float low, out float high));
            Assert.AreEqual(3.0f, low, 1e-4f, "위쪽 틈을 골라야 한다");
            Assert.AreEqual(4.0f, high, 1e-4f);
        }

        [Test]
        public void 표_끝까지_뚫려_있어도_그_구간을_놓치지_않는다()
        {
            //  마지막 칸까지 뚫려 있으면 루프가 구간을 닫지 못해 놓치기 쉽다.
            var column = Column(true, true, false, false, false, false);

            Assert.IsTrue(FlappyGapAiming.TryFindGap(column, BottomY, Step, currentY: 2f, BodyRadius,
                out float low, out float high));
            Assert.AreEqual(1.0f, low, 1e-4f);
            Assert.AreEqual(2.5f, high, 1e-4f);
        }

        [Test]
        public void 전부_막혀_있으면_못_찾는다()
        {
            Assert.IsFalse(FlappyGapAiming.TryFindGap(Column(true, true, true), BottomY, Step,
                currentY: 1f, BodyRadius, out _, out _));
        }

        //  겨냥: 틈이 날갯짓 아치보다 넓으면 가운데(위로 아치만큼 여유를 남기고), 좁으면 바닥.
        [Test]
        public void 넓은_틈은_아치만큼_여유를_남기고_가운데를_겨냥한다()
        {
            //  틈 10m, 아치 4m → 위아래로 3m씩 남기고 low+3
            Assert.AreEqual(3f, FlappyGapAiming.AimHeight(low: 0f, high: 10f, flapArc: 4f), 1e-4f);
        }

        [Test]
        public void 좁은_틈은_바닥에_붙여_겨냥한다()
        {
            //  틈 2m가 아치 4m보다 좁다 — 가운데를 노리면 아치가 천장을 넘는다.
            Assert.AreEqual(0f, FlappyGapAiming.AimHeight(low: 0f, high: 2f, flapArc: 4f), 1e-4f);
        }

        //  ── 여러 기둥 겹치기

        [Test]
        public void 두_기둥의_틈이_겹치는_구간만_남긴다()
        {
            Assert.IsTrue(FlappyGapAiming.TryIntersect(0f, 10f, 4f, 20f, out float low, out float high));
            Assert.AreEqual(4f, low, 1e-4f);
            Assert.AreEqual(10f, high, 1e-4f);
        }

        [Test]
        public void 겹치는_데가_없으면_실패한다()
        {
            //  앞 기둥은 아래쪽만, 뒤 기둥은 위쪽만 뚫려 있다 — 한 높이로 둘 다 지날 수 없다.
            Assert.IsFalse(FlappyGapAiming.TryIntersect(0f, 3f, 8f, 12f, out _, out _));
        }

        //  ── 낙하 예측

        [Test]
        public void 종단속도까지_가속하고_그_뒤로는_일정하게_떨어진다()
        {
            //  중력 70, 종단 30 → 약 0.43초 만에 종단속도에 닿는다. 그 뒤 1초는 정확히 30m.
            float after50 = FlappyGapAiming.PredictHeight(
                y: 0f, verticalSpeed: 0f, ticks: 50, deltaTime: 0.02f, gravity: 70f, maxFallSpeed: 30f);
            float after100 = FlappyGapAiming.PredictHeight(
                y: 0f, verticalSpeed: 0f, ticks: 100, deltaTime: 0.02f, gravity: 70f, maxFallSpeed: 30f);

            Assert.AreEqual(-30f, after100 - after50, 0.05f);
        }

        [Test]
        public void 날갯짓_정점은_해석식보다_반_틱만큼_낮다()
        {
            //  23으로 시작해 16틱(0.32초)에 정점을 찍는다. 해석식 v²/2g = 3.78m인데 실제는 3.55m다 —
            //  틱 단위로 적분하면 매 틱 "줄어든 뒤의 속도"로 움직여서 반 틱만큼 손해를 본다.
            //  겨냥은 시뮬이 실제로 가는 높이에 맞춰야 하므로 이 값을 기준으로 잡는다.
            float peak = FlappyGapAiming.PredictHeight(
                y: 0f, verticalSpeed: 23f, ticks: 16, deltaTime: 0.02f, gravity: 70f, maxFallSpeed: 30f);
            Assert.AreEqual(3.552f, peak, 0.01f);
            Assert.Less(peak, FlappyGapAiming.FlapArc(23f, 70f), "해석식보다 낮아야 한다");
        }

        //  ── 날갯짓 판단: "지금 낮은가"가 아니라 "도착할 때 걸리는가"

        [Test]
        public void 지금은_높아도_도착할_때_바닥_아래면_친다()
        {
            //  현재 높이는 틈(0~10) 한가운데인 5m지만, 종단속도로 떨어지는 중이라
            //  20틱(0.4초) 뒤에는 -7m까지 내려간다 → 지금 쳐야 한다.
            Assert.IsTrue(FlappyGapAiming.ShouldFlap(
                y: 5f, verticalSpeed: -30f, low: 0f, high: 10f,
                ticks: 20, deltaTime: 0.02f, gravity: 70f, maxFallSpeed: 30f,
                flapImpulse: 23f, margin: 0.45f));
        }

        [Test]
        public void 가만히_둬도_통과하면_치지_않는다()
        {
            Assert.IsFalse(FlappyGapAiming.ShouldFlap(
                y: 5f, verticalSpeed: 0f, low: 0f, high: 10f,
                ticks: 5, deltaTime: 0.02f, gravity: 70f, maxFallSpeed: 30f,
                flapImpulse: 23f, margin: 0.45f));
        }

        [Test]
        public void 치면_천장인데_안_쳐도_바닥_위면_치지_않는다()
        {
            //  틈이 좁고(0~4m) 새가 그 위쪽에 있다. 치면 천장을 넘고, 안 쳐도 바닥 위에 남는다.
            Assert.IsFalse(FlappyGapAiming.ShouldFlap(
                y: 3f, verticalSpeed: 0f, low: 0f, high: 4f,
                ticks: 5, deltaTime: 0.02f, gravity: 70f, maxFallSpeed: 30f,
                flapImpulse: 23f, margin: 0.45f));
        }

        [Test]
        public void 안_치면_바닥_아래로_떨어질_때는_천장을_넘더라도_친다()
        {
            //  둘 다 나쁘면 떨어져 죽는 쪽보다 천장을 택한다.
            Assert.IsTrue(FlappyGapAiming.ShouldFlap(
                y: 0.2f, verticalSpeed: -30f, low: 0f, high: 4f,
                ticks: 20, deltaTime: 0.02f, gravity: 70f, maxFallSpeed: 30f,
                flapImpulse: 23f, margin: 0.45f));
        }

        [Test]
        public void 날갯짓_아치는_실제_설정값에서_나온다()
        {
            //  FlappyConfig: flapImpulse 23, gravity 70 → 23²/(2·70) = 3.778m
            Assert.AreEqual(3.778f, FlappyGapAiming.FlapArc(23f, 70f), 1e-3f);
        }
    }
}
