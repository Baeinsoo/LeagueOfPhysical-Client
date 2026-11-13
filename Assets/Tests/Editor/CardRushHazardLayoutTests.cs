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
            var s = new CardRushRunnerState { ObstacleIndex = 2, ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 5000 };
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

        // ── 4차: 투사체·구덩이·넘음 ──

        //  0: 바리케이드 20 m, 1: 구덩이 30 m 폭 1.2 m, 2: 굴러오는 것 45 m.
        private static readonly CardRushTrack pitTrack = CardRushTrack.ForTests(140000,
            new[]
            {
                new CardRushObstacle(CardRushObstacleKind.Barricade, 20000, 0),
                new CardRushObstacle(CardRushObstacleKind.Pit, 30000, 1, 1200),
                new CardRushObstacle(CardRushObstacleKind.Roller, 45000, 2),
            },
            new CardRushZone[0], 9);

        private static CardRushObstacleLook Pit(CardRushRunnerState s, int index, double d, float bodyMm = 0f)
            => CardRushHazardLayout.ObstacleLook(s, index, d, bodyMm, pitTrack, tuning);

        [Test]
        public void 날고_있는_투사체는_던진_틱의_출발_자리에서_틱마다_300mm()
        {
            var s = new CardRushRunnerState { ShotTick = 1000, ShotFromMm = 5300 };
            Assert.AreEqual(CardRushShotPhase.None, CardRushHazardLayout.ShotLook(s, 999, track).Phase, "던지기 전");
            var at = CardRushHazardLayout.ShotLook(s, 1000, track);
            Assert.AreEqual(CardRushShotPhase.Flying, at.Phase);
            Assert.AreEqual(5300f, at.AlongMm, 1e-3f);
            Assert.AreEqual(5300f + 3000f, CardRushHazardLayout.ShotLook(s, 1010, track).AlongMm, 1e-3f);
            Assert.AreEqual(5300f + 300f * 10.5f, CardRushHazardLayout.ShotLook(s, 1010.5, track).AlongMm, 1e-3f, "그림 틱 사이도 이어진다");
            Assert.AreEqual(5300f + CardRushRules.ShotRangeMm, CardRushHazardLayout.ShotLook(s, 1100, track).AlongMm, 1e-3f, "사거리 끝을 넘지 않는다");
        }

        [Test]
        public void 헛방은_던진_틱부터_사거리_끝까지_날다_끝에서_03초_꺼진다()
        {
            var s = new CardRushRunnerState { WhiffTick = 2000, ShotFromMm = 5300 };
            int thrown = 2000 - CardRushHazardLayout.WhiffFlightTicks;
            Assert.AreEqual(27, CardRushHazardLayout.WhiffFlightTicks, "8000 / 300 올림");
            Assert.AreEqual(CardRushShotPhase.None, CardRushHazardLayout.ShotLook(s, thrown - 1, track).Phase);
            Assert.AreEqual(5300f, CardRushHazardLayout.ShotLook(s, thrown, track).AlongMm, 1e-3f);
            var flying = CardRushHazardLayout.ShotLook(s, 1990, track);
            Assert.AreEqual(CardRushShotPhase.Flying, flying.Phase);
            Assert.AreEqual(5300f + 300f * (1990 - thrown), flying.AlongMm, 1e-3f);

            var fizzle = CardRushHazardLayout.ShotLook(s, 2000, track);
            Assert.AreEqual(CardRushShotPhase.Fizzle, fizzle.Phase);
            Assert.AreEqual(5300f + 8000f, fizzle.AlongMm, 1e-3f, "꺼진 자리 = 출발 + 사거리");
            Assert.AreEqual(0f, fizzle.T01, 1e-6f);
            Assert.AreEqual(0.5f, CardRushHazardLayout.ShotLook(s, 2007.5, track).T01, 1e-5f);
            Assert.AreEqual(CardRushShotPhase.None, CardRushHazardLayout.ShotLook(s, 2015, track).Phase, "0.3초(15틱) 뒤엔 없다");
        }

        //  남의 레인: 상태는 이미 맞혔지만(결과 틱 3000) 그림 틱엔 아직 날고 있다 — 맞는 순간 와장창과 같이 닿는다.
        [Test]
        public void 남의_레인_맞힌_투사체는_결과_틱까지_날아가고_닿는_자리에서_멈춘다()
        {
            //  출발 18000, 앞면 20000 → 7틱째(18000 + 2100)에 닿는다 → 던진 틱 2993, 쓴 뒤 쿨다운 틱 3013.
            var s = new CardRushRunnerState { ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 3000, ShotFromMm = 18000, SpinStartTick = 3013 };
            var l = CardRushHazardLayout.ShotLook(s, 2996, pitTrack);
            Assert.AreEqual(CardRushShotPhase.Flying, l.Phase);
            Assert.AreEqual(18000f + 300f * 3f, l.AlongMm, 1e-3f);
            Assert.AreEqual(20000f, CardRushHazardLayout.ShotLook(s, 2999.9, pitTrack).AlongMm, 1e-3f, "바리케이드 앞면에서 멈춘다(넘지 않음)");
            Assert.AreEqual(CardRushShotPhase.None, CardRushHazardLayout.ShotLook(s, 3000, pitTrack).Phase, "닿은 틱부터는 조각이 대신한다");
            Assert.AreEqual(CardRushShotPhase.None, CardRushHazardLayout.ShotLook(s, 2992, pitTrack).Phase, "던지기 전");
            Assert.AreEqual(CardRushObstaclePhase.Standing, CardRushHazardLayout.ObstacleLook(s, 0, 2999, 18000f, pitTrack, tuning).Phase, "투사체가 닿을 때까지 바리케이드는 서 있다");
        }

        [Test]
        public void 투사체에_맞은_굴러오는_것은_맞은_자리에서_만난다()
        {
            //  출발 22300에서 10틱 → 25300. 굴러오는 것은 38 mm/틱으로 같은 자리에 온다.
            var s = new CardRushRunnerState { ObstacleIndex = 2, ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 4000, ShotFromMm = 22300, SpinStartTick = 4010, PosMm = 22000 };
            var before = CardRushHazardLayout.ObstacleLook(s, 1, 3995, 22000f, track, tuning);
            Assert.AreEqual(CardRushObstaclePhase.Rolling, before.Phase);
            Assert.AreEqual(25300f + 38f * 5f, before.AlongMm, 1e-3f);
            Assert.AreEqual(25300f, CardRushHazardLayout.ShotLook(s, 3999.999, track).AlongMm, 1f, "투사체도 같은 자리로");
            var after = CardRushHazardLayout.ObstacleLook(s, 1, 4000, 22000f, track, tuning);
            Assert.AreEqual(CardRushObstaclePhase.Smashed, after.Phase);
            Assert.AreEqual(25300f, after.AlongMm, 1e-3f, "조각이 맞은 자리에서 흩어진다");
        }

        [Test]
        public void 구덩이_사각형은_구멍은_폭만큼_경고_줄무늬는_앞_1m()
        {
            var r = CardRushHazardLayout.PitRect(pitTrack.Obstacles[1]);
            Assert.AreEqual(30000, r.HoleFromMm);
            Assert.AreEqual(31200, r.HoleToMm);
            Assert.AreEqual(29000, r.StripeFromMm);
            Assert.AreEqual(30000, r.StripeToMm);
        }

        [Test]
        public void 구덩이는_넘어도_그대로_남는다()
        {
            Assert.AreEqual(CardRushObstaclePhase.Pit, Pit(new CardRushRunnerState { ObstacleIndex = 0 }, 1, 100).Phase, "앞으로 올 구덩이도 보인다");
            Assert.AreEqual(30000f, Pit(new CardRushRunnerState { ObstacleIndex = 0 }, 1, 100).AlongMm);
            var jumped = new CardRushRunnerState { ObstacleIndex = 2, ObstacleEvent = (int)CardRushObstacleEvent.Jumped, ObstacleEventTick = 6000 };
            Assert.AreEqual(CardRushObstaclePhase.Pit, Pit(jumped, 1, 6100).Phase, "넘은 구덩이는 남는다");
            Assert.AreEqual(CardRushObstaclePhase.Gone, Pit(new CardRushRunnerState { ObstacleIndex = 3 }, 1, 6100).Phase, "두 개 앞은 지운다");
        }

        [Test]
        public void 빠짐은_구덩이_자리에_03초_먼지()
        {
            //  빠지면 번호가 안 올라 지금 장애물에 결과가 적힌다.
            var s = new CardRushRunnerState { ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Fell, ObstacleEventTick = 5000, PosMm = 28500 };
            Assert.AreEqual(CardRushObstaclePhase.Pit, Pit(s, 1, 4999).Phase, "남의 레인 — 몸이 아직 안 빠졌다");
            var at = Pit(s, 1, 5000);
            Assert.AreEqual(CardRushObstaclePhase.PitFell, at.Phase);
            Assert.AreEqual(0f, at.T01, 1e-6f);
            Assert.AreEqual(30000f, at.AlongMm, "먼지는 몸이 아니라 구덩이 자리");
            Assert.AreEqual(0.5f, Pit(s, 1, 5007.5).T01, 1e-5f);
            Assert.AreEqual(CardRushObstaclePhase.Pit, Pit(s, 1, 5015).Phase, "0.3초(15틱) 뒤엔 구멍만");
        }

        [Test]
        public void 굴러오는_것을_넘으면_몸_밑으로_지나가_035초에_사라진다()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 3, ObstacleEvent = (int)CardRushObstacleEvent.Jumped, ObstacleEventTick = 7000, PosMm = 44000 };
            var at = Pit(s, 2, 7000, 44000f);
            Assert.AreEqual(CardRushObstaclePhase.Jumped, at.Phase);
            Assert.AreEqual(0f, at.T01, 1e-6f);
            Assert.AreEqual(44000f + CardRushRules.RollerContactMm, at.AlongMm, 1e-3f, "닿은 순간엔 몸 앞 접촉 거리");
            var later = Pit(s, 2, 7010, 44000f);
            Assert.AreEqual(44000f - (CardRushRules.JumpMmPerTick + CardRushRules.RollerMmPerTick) * 10f + CardRushRules.RollerContactMm, later.AlongMm, 1e-3f,
                "몸은 앞으로 60, 굴러오는 것은 몸 쪽으로 38 — 서로 지나친다");
            Assert.Less(later.AlongMm, 44000f, "10틱 뒤엔 몸 뒤");
            Assert.AreEqual(CardRushObstaclePhase.Gone, Pit(s, 2, 7018, 44000f).Phase, "0.35초(17.5틱) 뒤엔 없다");

            //  남의 레인: 결과 틱 전엔 아직 굴러온다 — 결과 틱에 접촉 거리였다.
            var rolling = Pit(s, 2, 6990, 43500f);
            Assert.AreEqual(CardRushObstaclePhase.Rolling, rolling.Phase);
            Assert.AreEqual(43500f + CardRushRules.RollerContactMm + 38f * 10f, rolling.AlongMm, 1e-3f);
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

        //  최종 리뷰 m1: 경고 중에 완주하면 시뮬은 완주 뒤 멈춰 그 구역이 영영 안 터진다(ZoneTick 그대로).
        //  그림은 결승 틱부터 그 구역을 걷는다 — "0.0초"·바닥에 놓인 바위·화난 술래가 남지 않게. 상태는 그대로 둔다.
        [Test]
        public void 경고_중_완주하면_결승_틱부터_그_구역을_안_그린다()
        {
            var s = new CardRushRunnerState { ZoneIndex = 0, ZoneTick = 1000, FinishTick = 1030, PosMm = 140000 };
            Assert.AreEqual(CardRushZonePhase.Warning, CardRushHazardLayout.ZoneLook(s, 0, 1029, track).Phase, "결승 전엔 그대로 경고");
            var after = CardRushHazardLayout.ZoneLook(s, 0, 1100, track);
            Assert.AreNotEqual(CardRushZonePhase.Warning, after.Phase);
            Assert.Less(after.RockHeight, 0f, "바위도 없다");
            Assert.AreNotEqual(CardRushZonePhase.Warning, CardRushHazardLayout.ZoneLook(s, 0, 1030, track).Phase, "결승 틱부터");
        }
    }
}
