using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 장애물·구역 그림의 순수 식(spec 2026-11-05 §11.2). 내 레인은 그림 틱이 상태 뒤 한 틱이라 거의 늘 결과 뒤고,
    /// 남의 레인은 상태(최신 스냅)가 그림 틱보다 앞서 있어 "결과 틱 > 그림 틱"이면 그 장애물·구역이 그림에선 아직 살아 있다.
    /// </summary>
    public class CardRushHazardLayoutTests
    {
        //  0: 바리케이드 20 m(생김새 2), 1: 굴러오는 것 30 m(생김새 1), 2: 바리케이드 40 m.
        private static readonly CardRushTrack track = CardRushTrack.ForTests(140000,
            new[]
            {
                new CardRushObstacle(CardRushObstacleKind.Barricade, 20000, 2),
                new CardRushObstacle(CardRushObstacleKind.Roller, 30000, 1),
                new CardRushObstacle(CardRushObstacleKind.Barricade, 40000, 0),
            },
            new[]
            {
                new CardRushZone(14000, 15000, 18000, 70),
                new CardRushZone(50000, 51000, 54000, 60),
            }, 9);

        private static readonly CardRushTuning tuning = CardRushServerCorrectionHandlerTests.Config.Tuning;

        private static CardRushObstacleLook Obstacle(CardRushRunnerState s, int index, double d, float bodyMm = 0f)
            => CardRushHazardLayout.ObstacleLook(s, index, d, bodyMm, track, tuning);

        [Test]
        public void 시작_튜닝의_기다림은_60틱()
        {
            Assert.AreEqual(60, tuning.BarricadeWaitTicks, "아래 시험의 /60이 이 값");
        }

        [Test]
        public void 바리케이드는_닿은_틱부터_기다린_몫만큼_올라간다()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 0, ObstacleTick = 1000, PosMm = 20000 - CardRushRules.BarricadeStopMm };
            var before = Obstacle(s, 0, 999);
            Assert.AreEqual(CardRushObstaclePhase.Standing, before.Phase, "닿기 전 그림 틱");
            Assert.AreEqual(20000f, before.AlongMm);
            Assert.AreEqual(2, before.Variant);

            var mid = Obstacle(s, 0, 1015);
            Assert.AreEqual(CardRushObstaclePhase.Lifting, mid.Phase);
            Assert.AreEqual(15f / 60f, mid.T01, 1e-5f);
            Assert.AreEqual((1060 - 1015) * 0.02f, mid.SecondsLeft, 1e-5f, "남은 초");
            Assert.AreEqual(0f, Obstacle(s, 0, 1000).T01, 1e-6f);
        }

        [Test]
        public void 와장창은_결과_틱부터_035초()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 2000, PosMm = 19000 };
            var at = Obstacle(s, 0, 2000, 19000f);
            Assert.AreEqual(CardRushObstaclePhase.Smashed, at.Phase);
            Assert.AreEqual(0f, at.T01, 1e-6f);
            Assert.AreEqual(20000f, at.AlongMm, "바리케이드 조각은 제자리에서 흩어진다");
            Assert.AreEqual(0.5f, Obstacle(s, 0, 2000 + 0.175 / 0.02, 19000f).T01, 1e-4f);
            Assert.AreEqual(CardRushObstaclePhase.Smashed, Obstacle(s, 0, 2017, 19000f).Phase);
            Assert.AreEqual(CardRushObstaclePhase.Gone, Obstacle(s, 0, 2018, 19000f).Phase, "0.35초(17.5틱) 뒤엔 없다");
        }

        [Test]
        public void 다_올라간_바리케이드는_03초에_사라진다()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Lifted, ObstacleEventTick = 2000 };
            Assert.AreEqual(CardRushObstaclePhase.Lifted, Obstacle(s, 0, 2010).Phase);
            Assert.AreEqual(CardRushObstaclePhase.Gone, Obstacle(s, 0, 2015).Phase);
        }

        [Test]
        public void 굴러오는_것은_풀린_틱부터_틱마다_38mm씩_다가온다()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 1, ObstacleTick = 3000, PosMm = 23000 };
            var standing = Obstacle(s, 1, 2999);
            Assert.AreEqual(CardRushObstaclePhase.Standing, standing.Phase, "풀리기 전엔 제자리에 서 있다");
            Assert.AreEqual(30000f, standing.AlongMm);

            var rolling = Obstacle(s, 1, 3040);
            Assert.AreEqual(CardRushObstaclePhase.Rolling, rolling.Phase);
            Assert.AreEqual(30000f - 38f * 40f, rolling.AlongMm, 1e-3f);
            Assert.AreEqual(1, rolling.Variant);
            Assert.AreEqual(30000f - 38f * 40.5f, Obstacle(s, 1, 3040.5).AlongMm, 1e-3f, "그림 틱 사이도 이어진다");
        }

        [Test]
        public void 앞으로_올_것은_서_있고_두_개_앞에_치운_것은_없다()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 2, ObstacleEvent = (int)CardRushObstacleEvent.Blocked, ObstacleEventTick = 5000 };
            Assert.AreEqual(CardRushObstaclePhase.Gone, Obstacle(s, 0, 5005).Phase);
            Assert.AreEqual(CardRushObstaclePhase.Standing, Obstacle(new CardRushRunnerState(), 2, 100).Phase);
        }

        //  남의 레인: 상태는 최신 스냅이라 이미 치웠지만(결과 틱 5000) 그려지는 몸은 4990에 있다 — 그림에선 아직 살아 있다.
        [Test]
        public void 남의_레인은_결과_틱이_그림_틱보다_뒤면_아직_살아_있다()
        {
            var smashedBarricade = new CardRushRunnerState { ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 5000 };
            var barricade = Obstacle(smashedBarricade, 0, 4990, 18500f);
            Assert.AreEqual(CardRushObstaclePhase.Standing, barricade.Phase, "바리케이드는 제자리");
            Assert.AreEqual(20000f, barricade.AlongMm);
            Assert.AreEqual(CardRushObstaclePhase.Smashed, Obstacle(smashedBarricade, 0, 5000, 18500f).Phase, "결과 틱에 와장창");

            var tumbledRoller = new CardRushRunnerState { ObstacleIndex = 2, ObstacleEvent = (int)CardRushObstacleEvent.Tumbled, ObstacleEventTick = 5000 };
            var roller = Obstacle(tumbledRoller, 1, 4990, 26000f);
            Assert.AreEqual(CardRushObstaclePhase.Rolling, roller.Phase);
            Assert.AreEqual(26000f + 420f + 38f * 10f, roller.AlongMm, 1e-3f, "결과 틱에 접촉 거리였다 — 거꾸로 잡은 자리");
            Assert.AreEqual(CardRushObstaclePhase.Tumbled, Obstacle(tumbledRoller, 1, 5000, 26000f).Phase);

            //  아직 안 풀렸을 만큼 이른 그림 틱이면(거꾸로 잡은 자리가 놓인 자리보다 멀다) 제자리에 서 있다.
            var early = Obstacle(tumbledRoller, 1, 4800, 22000f);
            Assert.AreEqual(CardRushObstaclePhase.Standing, early.Phase);
            Assert.AreEqual(30000f, early.AlongMm);

            //  올라감으로 치운 바리케이드는 닿은 틱(결과 − 기다림)을 알아 올라가는 모습까지 그린다.
            var lifted = new CardRushRunnerState { ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Lifted, ObstacleEventTick = 5000 };
            var lifting = Obstacle(lifted, 0, 4970, 19510f);
            Assert.AreEqual(CardRushObstaclePhase.Lifting, lifting.Phase);
            Assert.AreEqual(30f / 60f, lifting.T01, 1e-5f);
        }

        [Test]
        public void 구역은_켜진_틱부터_경고와_남은_초()
        {
            var s = new CardRushRunnerState { ZoneIndex = 0, ZoneTick = 1000, PosMm = 14100 };
            Assert.AreEqual(CardRushZonePhase.Hidden, CardRushHazardLayout.ZoneLook(s, 0, 999, track).Phase);
            var z = CardRushHazardLayout.ZoneLook(s, 0, 1010, track);
            Assert.AreEqual(CardRushZonePhase.Warning, z.Phase);
            Assert.AreEqual(15000, z.FromMm);
            Assert.AreEqual(18000, z.ToMm);
            Assert.AreEqual((1000 + 70 - 1010) * 0.02f, z.SecondsLeft, 1e-5f);
            Assert.AreEqual(10f / 70f, z.Warn01, 1e-5f);
            Assert.Less(z.RockHeight, 0f, "바위는 마지막 15틱에만");
            Assert.AreEqual(CardRushZonePhase.Hidden, CardRushHazardLayout.ZoneLook(s, 1, 1010, track).Phase, "다음 구역은 아직");
        }

        [Test]
        public void 바위는_15틱_전_6m에서_떨어져_터지는_틱에_바닥()
        {
            var s = new CardRushRunnerState { ZoneIndex = 0, ZoneTick = 1000 };
            Assert.AreEqual(6f, CardRushHazardLayout.ZoneLook(s, 0, 1070 - 15, track).RockHeight, 1e-5f);
            Assert.Less(CardRushHazardLayout.ZoneLook(s, 0, 1070 - 16, track).RockHeight, 0f);
            float half = CardRushHazardLayout.ZoneLook(s, 0, 1070 - 7.5, track).RockHeight;
            Assert.AreEqual(6f * 0.75f, half, 1e-4f, "가속 낙하 — 반 시간에 1/4만 떨어졌다");

            //  터진 틱: 상태는 다음 구역으로 넘어갔고 결과 틱이 1070.
            var after = new CardRushRunnerState { ZoneIndex = 1, ZoneEvent = (int)CardRushZoneEvent.Hit, ZoneEventTick = 1070 };
            var impact = CardRushHazardLayout.ZoneLook(after, 0, 1070, track);
            Assert.AreEqual(CardRushZonePhase.Impact, impact.Phase);
            Assert.AreEqual(0f, impact.RockHeight, 1e-6f);
            Assert.AreEqual(CardRushZonePhase.Gone, CardRushHazardLayout.ZoneLook(after, 0, 1095, track).Phase, "먼지는 0.5초");
        }

        [Test]
        public void 남의_레인_구역도_결과_틱이_그림_틱보다_뒤면_아직_경고_중()
        {
            var s = new CardRushRunnerState { ZoneIndex = 1, ZoneEvent = (int)CardRushZoneEvent.Dodged, ZoneEventTick = 1070 };
            var z = CardRushHazardLayout.ZoneLook(s, 0, 1060, track);
            Assert.AreEqual(CardRushZonePhase.Warning, z.Phase);
            Assert.AreEqual(10 * 0.02f, z.SecondsLeft, 1e-5f);
            Assert.AreEqual(CardRushZonePhase.Hidden, CardRushHazardLayout.ZoneLook(s, 0, 990, track).Phase, "켜지기 전(1070 − 70)");
        }
    }
}
