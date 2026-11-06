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
        public void 방패는_쓴_틱부터_50틱_커지며_나타나고_끝에_옅어진다()
        {
            var s = new CardRushRunnerState { BlockEndTick = 1050 };
            Assert.Less(CardRushRunnerProps.Look(s, 999).Shield01, 0f);
            var start = CardRushRunnerProps.Look(s, 1000);
            Assert.AreEqual(0f, start.Shield01, 1e-6f);
            Assert.AreEqual(0.3f, start.ShieldScale, 1e-5f);
            Assert.AreEqual(1f, CardRushRunnerProps.Look(s, 1003).ShieldScale, 1e-5f, "0.06초에 다 커진다");
            Assert.AreEqual(1f, CardRushRunnerProps.Look(s, 1020).ShieldAlpha, 1e-5f);
            Assert.AreEqual(0.4f, CardRushRunnerProps.Look(s, 1048).ShieldAlpha, 1e-5f, "끝 0.1초에 옅어진다");
            Assert.Less(CardRushRunnerProps.Look(s, 1050).Shield01, 0f);
        }

        [Test]
        public void 대시_줄_망치_말풍선은_결과_틱에서_잰다()
        {
            var dash = new CardRushRunnerState { DashStartTick = 1000 };
            Assert.AreEqual(0.5f, CardRushRunnerProps.Look(dash, 1007.5).Streak01, 1e-5f);
            Assert.Less(CardRushRunnerProps.Look(dash, 1015).Streak01, 0f, "0.3초에 사라진다");

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

        [Test]
        public void 소품도_콜라이더가_없다()
        {
            var parent = new GameObject("props-test");
            spawned.Add(parent);
            var fxMaterials = new CardRushGagFxMaterials();
            try
            {
                var props = new CardRushRunnerProps(parent.transform, "Props", fxMaterials);
                var look = CardRushRunnerProps.Look(new CardRushRunnerState { BlockEndTick = 1050, DashStartTick = 1010, ObstacleEvent = (int)CardRushObstacleEvent.Whiff, ObstacleEventTick = 1012 }, 1015);
                props.Place(look, Vector3.zero, Vector3.up * 1.2f, Vector3.forward, null, true, true);
                Assert.IsTrue(props.ShieldVisible);
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
