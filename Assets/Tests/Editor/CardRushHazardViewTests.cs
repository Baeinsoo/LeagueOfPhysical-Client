using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 장애물·구역·트랙·몸 소품 그림(spec 2026-11-05 §11) — 실제 GameObject로 한 레인을 깔아 자리·숨김·콜라이더를 본다.
    /// 모습 자체(언제 무엇)는 <see cref="CardRushHazardLayoutTests"/>가 본다. 여기선 그 모습이 오브젝트에 옮겨지는지만.
    /// </summary>
    public class CardRushHazardViewTests
    {
        private static readonly CardRushTrack track = CardRushTrack.ForTests(60000,
            new[]
            {
                new CardRushObstacle(CardRushObstacleKind.Barricade, 20000, 1),
                new CardRushObstacle(CardRushObstacleKind.Roller, 30000, 0),
                new CardRushObstacle(CardRushObstacleKind.Roller, 52000, 2),
            },
            new[] { new CardRushZone(14000, 15000, 18500, 70) }, 5);

        private static readonly CardRushTuning tuning = CardRushServerCorrectionHandlerTests.Config.Tuning;
        private static readonly Vector3 Origin = new Vector3(2f, 0f, -3f);

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CardRushStageMaterials materials;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { if (go != null) Object.DestroyImmediate(go); }
            spawned.Clear();
            materials?.Dispose();
            materials = null;
        }

        private CardRushLaneHazards Lane()
        {
            materials ??= new CardRushStageMaterials();
            var parent = new GameObject("hazard-test");
            spawned.Add(parent);
            return new CardRushLaneHazards(parent.transform, "Lane", Origin, Vector3.right, 2.6f, materials);
        }

        [Test]
        public void 몸_앞_40m_안의_장애물만_레인_위에_콜라이더_없이_선다()
        {
            var lane = Lane();
            var s = new CardRushRunnerState { PosMm = 10000 };
            lane.Apply(s, 1000, 10000f, track, tuning, null);

            Assert.AreEqual(2, lane.ObstacleCount, "20·30 m — 52 m는 몸 앞 40 m 너머");
            Assert.That(Vector3.Distance(Origin + Vector3.right * 20f, lane.ObstacleRoot(0).position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(Origin + Vector3.right * 30f, lane.ObstacleRoot(1).position), Is.LessThan(1e-4f));
            Assert.IsEmpty(lane.Root.GetComponentsInChildren<Collider>(true), "그림 전용 — 콜라이더가 남으면 PhysX 대리 몸과 부딪친다");
            Assert.AreEqual(0, lane.ZoneCount, "켜지기 전 구역은 없다");
        }

        [Test]
        public void 지나온_장애물은_지우고_다음_것을_만든다()
        {
            var lane = Lane();
            lane.Apply(new CardRushRunnerState { PosMm = 10000 }, 1000, 10000f, track, tuning, null);
            var passed = new CardRushRunnerState
            {
                PosMm = 33000, ObstacleIndex = 2, ObstacleEvent = (int)CardRushObstacleEvent.Blocked, ObstacleEventTick = 1500,
            };
            lane.Apply(passed, 1600, 33000f, track, tuning, null);
            Assert.IsNull(lane.ObstacleRoot(0), "두 개 앞에 치운 바리케이드");
            Assert.IsNull(lane.ObstacleRoot(1), "막아 낸 지 0.35초가 지난 굴러오는 것");
            Assert.IsNotNull(lane.ObstacleRoot(2));
        }

        [Test]
        public void 바리케이드는_기다린_몫만큼_올라가고_와장창이면_조각만_남는다()
        {
            var lane = Lane();
            var touching = new CardRushRunnerState { PosMm = 19510, ObstacleTick = 1000 };
            lane.Apply(touching, 1030, 19510f, track, tuning, null);
            var body = lane.ObstacleRoot(0).Find("Body");
            Assert.AreEqual(CardRushHazardLayout.BarricadeLiftHeight * 0.5f, body.localPosition.y, 1e-4f);

            var smashed = new CardRushRunnerState { PosMm = 19600, ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 1040 };
            lane.Apply(smashed, 1045, 19600f, track, tuning, null);
            Assert.IsFalse(body.gameObject.activeSelf, "부서진 판은 숨고");
            int shards = 0;
            foreach (Transform child in lane.ObstacleRoot(0)) if (child.name == "Shard" && child.gameObject.activeSelf) shards++;
            Assert.AreEqual(CardRushLaneHazards.ShardCount, shards, "조각이 흩어진다");
        }

        [Test]
        public void 빨간_바닥은_From부터_To까지_레인_폭으로_깔린다()
        {
            var lane = Lane();
            var warning = new CardRushRunnerState { PosMm = 14100, ZoneTick = 2000 };
            lane.Apply(warning, 2010, 14100f, track, tuning, null);
            Assert.AreEqual(1, lane.ZoneCount);
            var zone = lane.ZoneRoot(0);
            Assert.That(Vector3.Distance(Origin + Vector3.right * 16.75f, zone.position), Is.LessThan(1e-4f), "가운데");
            var floor = zone.Find("Floor");
            Assert.AreEqual(3.5f, floor.localScale.z, 1e-4f, "길이 = To − From");
            Assert.Greater(floor.position.y, Origin.y, "맵 바닥과 같은 평면이 아니다(깜빡임)");
            Assert.IsFalse(zone.Find("Rock").gameObject.activeSelf, "바위는 마지막 15틱에만");

            lane.Apply(warning, 2062, 14100f, track, tuning, null);
            Assert.IsTrue(zone.Find("Rock").gameObject.activeSelf);
            Assert.IsEmpty(lane.Root.GetComponentsInChildren<Collider>(true));
        }

        [Test]
        public void 결승_문은_코스_길이_자리에_8레인_폭으로()
        {
            materials ??= new CardRushStageMaterials();
            var parent = new GameObject("finish-test");
            spawned.Add(parent);
            var finish = CardRushTrackDecor.BuildFinish(parent.transform, Origin, Vector3.right, 2.6f, 60000, materials, HideFlags.None, out var sullae);
            Assert.That(Vector3.Distance(Origin + Vector3.right * 60f, finish.transform.position), Is.LessThan(1e-4f));
            Assert.AreEqual(2.6f * 8f, finish.transform.Find("CheckerBand").localScale.x, 1e-4f);
            Assert.IsEmpty(finish.GetComponentsInChildren<Collider>(true));
            Assert.IsFalse(sullae.Angry);
            sullae.Update(null, true);
            Assert.IsTrue(sullae.Angry, "내 구역이 켜지면 술래 눈이 빨갛다");

            var laneGo = CardRushTrackDecor.BuildLane(parent.transform, "Lane", Origin, Vector3.right, 2.6f, 60000, materials);
            int ticks = 0;
            foreach (Transform child in laneGo.transform) if (child.name.StartsWith("Tick")) ticks++;
            Assert.AreEqual(5, ticks, "10 m마다 — 10·20·30·40·50 m");
            Assert.IsEmpty(laneGo.GetComponentsInChildren<Collider>(true));
        }

        // ── 몸 소품 ──

        [Test]
        public void 대시_줄_망치_말풍선은_결과_틱에서_잰다()
        {
            var dash = new CardRushRunnerState { DashStartTick = 1000 };
            Assert.AreEqual(0.5f, CardRushRunnerProps.Look(dash, 1000 + CardRushRunnerFxView.StreakTicks * 0.5).Streak01, 1e-5f);
            Assert.Less(CardRushRunnerProps.Look(dash, 1000 + CardRushRunnerFxView.StreakTicks).Streak01, 0f, "대시 안에 사라진다");

            var smash = new CardRushRunnerState { ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 2000 };
            var look = CardRushRunnerProps.Look(smash, 2005);
            Assert.AreEqual(0.5f, look.Hammer01, 1e-5f);
            Assert.AreEqual("와장창!", look.Bubble);
            Assert.IsTrue(look.BubbleGood);
            Assert.Less(CardRushRunnerProps.Look(smash, 2010).Hammer01, 0f);
            Assert.IsNull(CardRushRunnerProps.Look(smash, 2040).Bubble, "말풍선은 0.8초");

            var both = new CardRushRunnerState
            {
                ObstacleEvent = (int)CardRushObstacleEvent.Tumbled, ObstacleEventTick = 3000,
                ZoneEvent = (int)CardRushZoneEvent.Hit, ZoneEventTick = 3010,
            };
            Assert.AreEqual("으악", CardRushRunnerProps.Look(both, 3012).Bubble, "늦은 결과 하나");
            Assert.AreEqual("꽈당", CardRushRunnerProps.Look(both, 3005).Bubble);
            Assert.IsNull(CardRushRunnerProps.Look(new CardRushRunnerState { ObstacleEvent = (int)CardRushObstacleEvent.Lifted, ObstacleEventTick = 10 }, 12).Bubble,
                "기다려 올라간 건 말풍선 없음");
        }

        //  리뷰 I1: 막음 직후 헛방 — 막음 그림(장애물 결과)은 0.35초 그대로 가고, 망치·말풍선은 헛방을 따른다.
        [Test]
        public void 막음_직후_헛방해도_막음_그림은_간다()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 2, ObstacleEvent = (int)CardRushObstacleEvent.Blocked, ObstacleEventTick = 5000, WhiffTick = 5005 };
            Assert.AreEqual(CardRushObstaclePhase.Blocked, CardRushHazardLayout.ObstacleLook(s, 1, 5010, 26000f, track, tuning).Phase);
            var look = CardRushRunnerProps.Look(s, 5010);
            Assert.AreEqual("헛방", look.Bubble, "늦은 결과 하나");
            Assert.AreEqual(0.5f, look.Hammer01, 1e-5f);
            Assert.AreEqual("막음!", CardRushRunnerProps.Look(s, 5003).Bubble);
            Assert.Less(CardRushRunnerProps.Look(s, 5003).Hammer01, 0f, "헛방 전엔 망치 없음");
        }

        //  남의 레인: 올라감 결과가 그림 틱 뒤이고 그 뒤에 헛방이 있어도 그림 틱엔 올라가는 중이다.
        [Test]
        public void 남의_레인_올라감_뒤_헛방이_있어도_올라가는_중()
        {
            var s = new CardRushRunnerState { ObstacleIndex = 1, ObstacleEvent = (int)CardRushObstacleEvent.Lifted, ObstacleEventTick = 5000, WhiffTick = 5002 };
            Assert.AreEqual(CardRushObstaclePhase.Lifting, CardRushHazardLayout.ObstacleLook(s, 0, 4997, 19510f, track, tuning).Phase);
        }

        //  리뷰 m2: 대시 줄은 대시 틱 안에 다 옅어진다 — 끝 틱에 시작 틱이 0이 되며 꺼질 때 이미 투명.
        [Test]
        public void 대시_줄은_대시_안에_다_옅어진다()
        {
            Assert.LessOrEqual(CardRushRunnerFxView.StreakTicks, CardRushRules.DashTicks - 2);
            var s = new CardRushRunnerState { DashStartTick = 1000 };
            Assert.Less(CardRushRunnerProps.Look(s, 1000 + CardRushRules.DashTicks - 2).Streak01, 0f, "시작 틱이 지워지기 전 마지막 그림 틱엔 이미 없다");
        }

        //  리뷰 I2: 내 바닥의 남은 초가 머리 위 카드에 가렸다 — 레인 오른쪽 가장자리, 몸 앞 1 m(바닥 안으로 자름).
        [Test]
        public void 구역_남은_초는_레인_오른쪽_몸_앞_1m()
        {
            Assert.AreEqual(1.3f, CardRushLaneHazards.ZoneLabelRight(2.6f), 1e-5f);
            Assert.AreEqual(16000f, CardRushLaneHazards.ZoneLabelAlongMm(15000, 18500, 15000f), 1e-3f);
            Assert.AreEqual(15000f, CardRushLaneHazards.ZoneLabelAlongMm(15000, 18500, 13000f), 1e-3f, "바닥 앞이면 바닥 시작");
            Assert.AreEqual(18500f, CardRushLaneHazards.ZoneLabelAlongMm(15000, 18500, 18000f), 1e-3f, "바닥 끝 너머로 안 간다");
        }

        [Test]
        public void 소품도_콜라이더가_없다()
        {
            var parent = new GameObject("props-test");
            spawned.Add(parent);
            var fxMaterials = new CardRushGagFxMaterials();
            try
            {
                var props = new CardRushRunnerProps(parent.transform, "Props", fxMaterials);
                var look = CardRushRunnerProps.Look(new CardRushRunnerState { DashStartTick = 1010, WhiffTick = 1012 }, 1015);
                props.Place(look, Vector3.zero, Vector3.up * 1.2f, Vector3.forward, null, true, true);
                Assert.IsTrue(props.StreaksVisible);
                Assert.IsTrue(props.HammerVisible, "헛방도 망치");
                Assert.IsEmpty(parent.GetComponentsInChildren<Collider>(true));
                props.Place(CardRushRunnerProps.Look(new CardRushRunnerState(), 5000), Vector3.zero, Vector3.up, Vector3.forward, null, true, true);
                Assert.IsFalse(props.ShieldVisible || props.StreaksVisible || props.HammerVisible);
            }
            finally
            {
                fxMaterials.Dispose();
            }
        }
    }
}
