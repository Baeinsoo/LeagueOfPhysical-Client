using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 배치만 보고 "어떤 위상에서도 통과 가능한가"를 판정하는 산술을 못박는다. 씬도 물리도
    /// 안 쓴다 — 여기서 재는 것은 <b>규칙</b>이지 맵이 아니다.
    /// </summary>
    public class ObstaclePlacementTests
    {
        //  실제 Flappy 값. 여기를 고치면 다른 숫자를 재는 것이다.
        const float FlapImpulse = 23f;
        const float Gravity = 70f;
        const float TickSeconds = 0.02f;
        const float BodyHeight = 0.9f;

        //  아치(4.012001) + 몸(0.9). 아래 테스트가 전부 이 기준을 쓴다.
        static float Required => ObstaclePlacementRule.RequiredBand(FlapImpulse, Gravity, TickSeconds, BodyHeight);

        static ObstaclePlacement Placement(float above, float below, string name = "Windmill")
            => new ObstaclePlacement(name, centerX: 100f, centerY: 0f, discRadius: 4.2f,
                                     bandAbove: above, bandBelow: below, measured: true);

        [Test]
        public void 기준은_날갯짓_아치와_몸_높이의_합이다()
        {
            //  숫자를 박지 않았다는 증거 — 실제 물리값에서 4.912가 나온다.
            Assert.AreEqual(4.912f, Required, 0.0005f);
            Assert.AreEqual(BotPilot.FlapArc(FlapImpulse, Gravity, TickSeconds) + BodyHeight, Required, 1e-6f);
        }

        [Test]
        public void 중력이_바뀌면_기준도_따라간다()
        {
            //  아치를 상수로 박으면 이 단언이 빨강이 된다 — 물리가 바뀌면 기준도 움직여야 한다.
            float weaker = ObstaclePlacementRule.RequiredBand(FlapImpulse, Gravity * 2f, TickSeconds, BodyHeight);
            Assert.Less(weaker, Required, "중력이 세지면 아치가 낮아져 기준도 낮아져야 한다.");
        }

        [Test]
        public void 위쪽_밴드만_충분해도_보장된다()
        {
            PlacementVerdict verdict = ObstaclePlacementRule.Judge(Placement(above: Required + 1f, below: 0.5f), Required);
            Assert.IsTrue(verdict.Guaranteed);
            Assert.AreEqual(0f, verdict.Shortfall);
        }

        [Test]
        public void 아래쪽_밴드만_충분해도_보장된다()
        {
            PlacementVerdict verdict = ObstaclePlacementRule.Judge(Placement(above: 0.5f, below: Required + 1f), Required);
            Assert.IsTrue(verdict.Guaranteed);
            Assert.AreEqual(0f, verdict.Shortfall);
        }

        [Test]
        public void 둘_다_모자라면_미달이고_모자란_양은_더_나은_밴드_기준이다()
        {
            //  x=276의 실측값이다 — 위 1.70 / 아래 0.90.
            PlacementVerdict verdict = ObstaclePlacementRule.Judge(Placement(above: 1.70f, below: 0.90f), Required);
            Assert.IsFalse(verdict.Guaranteed);
            //  좁은 쪽(0.90)이 아니라 넓은 쪽(1.70) 기준이다 — 우회는 한쪽만 되면 되므로.
            Assert.AreEqual(Required - 1.70f, verdict.Shortfall, 1e-4f);
            Assert.AreEqual(3.212f, verdict.Shortfall, 0.0005f);
        }

        [Test]
        public void 딱_기준만큼이면_보장된다()
        {
            Assert.IsTrue(ObstaclePlacementRule.Judge(Placement(above: Required, below: 0f), Required).Guaranteed);
        }

        [Test]
        public void 기준에서_1mm_모자라면_미달이다()
        {
            PlacementVerdict verdict = ObstaclePlacementRule.Judge(Placement(above: Required - 0.001f, below: 0f), Required);
            Assert.IsFalse(verdict.Guaranteed);
            Assert.AreEqual(0.001f, verdict.Shortfall, 1e-5f);
        }

        [Test]
        public void x구간에서_가장_좁아지는_자리가_밴드를_정한다()
        {
            //  가운데 한 자리만 좁다. 넓은 쪽을 쓰면 이 배치가 통과로 찍혀 이 테스트가 빨강이 된다.
            var samples = new List<BandSample>
            {
                new BandSample(9f, 9f),
                new BandSample(1.70f, 0.90f),
                new BandSample(9f, 9f),
            };
            ObstaclePlacement placement = ObstaclePlacement.Measure("Windmill", 276f, -0.1f, 4.2f, samples);
            Assert.IsTrue(placement.Measured);
            Assert.AreEqual(1.70f, placement.BandAbove, 1e-4f);
            Assert.AreEqual(0.90f, placement.BandBelow, 1e-4f);
            Assert.IsFalse(ObstaclePlacementRule.Judge(placement, Required).Guaranteed);
        }

        [Test]
        public void 반지름이_0이면_측정_안_됨이다()
        {
            //  콜라이더를 못 찾은 경우다. 0m 밴드로 찍으면 "재 보니 꽉 막혔다"로 읽히므로
            //  통과로도 미달로도 세지 않는다.
            var samples = new List<BandSample> { new BandSample(9f, 9f) };
            ObstaclePlacement placement = ObstaclePlacement.Measure("Windmill", 10f, 0f, 0f, samples);
            Assert.IsFalse(placement.Measured);

            PlacementVerdict verdict = ObstaclePlacementRule.Judge(placement, Required);
            Assert.IsFalse(verdict.Measured);
            Assert.IsFalse(verdict.Guaranteed, "못 잰 것을 보장으로 세면 안 된다.");
            Assert.AreEqual(0f, verdict.Shortfall, "못 잰 것에 고칠 양을 지어내면 안 된다.");
        }

        [Test]
        public void 표본이_하나도_없으면_측정_안_됨이다()
        {
            Assert.IsFalse(ObstaclePlacement.Measure("Windmill", 10f, 0f, 4.2f, new List<BandSample>()).Measured);
            Assert.IsFalse(ObstaclePlacement.Measure("Windmill", 10f, 0f, 4.2f, null).Measured);
        }

        [Test]
        public void 절은_미달_장애물에_고칠_양을_적는다()
        {
            string section = ObstaclePlacementRule.Section(
                new List<ObstaclePlacement> { Placement(1.70f, 0.90f, "FillWindmill") }, Required);

            //  기호는 Ordinal로 찾는다 — StringAssert는 문화권 비교라 없는 기호에도 매칭된다.
            Assert.GreaterOrEqual(section.IndexOf("❌", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("✅", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("풍차 1개 중 1개가 기준 미달", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("3.21m 모자람", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("팔을 3.21m 줄이거나 회랑을 3.21m 넓히면 만족", StringComparison.Ordinal), 0);
            //  보장하지 않는 것을 스스로 밝힌다.
            Assert.GreaterOrEqual(section.IndexOf("도달 가능성은 ① 봇 비행이 답한다", StringComparison.Ordinal), 0);
        }

        [Test]
        public void 절은_보장된_장애물에_고칠_양을_안_적는다()
        {
            string section = ObstaclePlacementRule.Section(
                new List<ObstaclePlacement>
                {
                    new ObstaclePlacement("Gauntlet/Windmill", 412f, 2f, 2.10f, 6.30f, 5.10f, measured: true),
                }, Required);

            Assert.GreaterOrEqual(section.IndexOf("✅", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("❌", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("풍차 1개 중 0개가 기준 미달", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("모자람", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("팔 L=2.10", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("위 6.30m / 아래 5.10m", StringComparison.Ordinal), 0);
        }

        [Test]
        public void 못_잰_장애물은_미달_수에도_안_들어간다()
        {
            string section = ObstaclePlacementRule.Section(
                new List<ObstaclePlacement>
                {
                    Placement(1.70f, 0.90f, "FillWindmill"),
                    new ObstaclePlacement("NoCollider", 5f, 0f, 0f, 0f, 0f, measured: false),
                }, Required);

            Assert.GreaterOrEqual(section.IndexOf("풍차 2개 중 1개가 기준 미달", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("1개는 원판을 못 재 판정 못 함", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("⛔", StringComparison.Ordinal), 0);
        }
    }
}
