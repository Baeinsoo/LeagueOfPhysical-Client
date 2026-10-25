using System.Reflection;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 판의 큰 순간: 내 새가 생기면 배경음, 결승하면 음악을 끄고(결승음은 도장 창이 낸다) 0.25초 0.4배 느려짐 + 꽃가루 한 번,
    /// 탈락하면 음악만 끈다. 이미 결승한 채로 들어오면(재접속) 아무것도 안 한다.
    /// </summary>
    public class FlappyRaceCuesTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private EntityRegistry registry;
        private ActorRegistry actors;
        private FakeGameDataStore store;
        private FakeFlappyAudio audio;
        private FlappyVisualTimeWarp warp;
        private FlappyBurstFx bursts;
        private FlappyRaceCues cues;
        private FinishState finish;
        private GameObject actorGo;

        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 4.5f, flapImpulse: 10.125f, gravity: 33.75f, maxFallSpeed: 11.25f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f, stunTime: 1.2f, invulnTime: 0.6f,
                                dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

        [SetUp]
        public void SetUp()
        {
            registry = new EntityRegistry();
            actors = new ActorRegistry();
            store = new FakeGameDataStore { userEntityId = "me" };
            audio = new FakeFlappyAudio();
            warp = new FlappyVisualTimeWarp(actors, store);
            warp.applyDelay = _ => { };
            bursts = new FlappyBurstFx();
            cues = new FlappyRaceCues(registry, actors, store, audio, warp, bursts, Config());
        }

        [TearDown]
        public void TearDown()
        {
            bursts.Dispose();
            if (actorGo != null) { UnityEngine.Object.DestroyImmediate(actorGo); }
        }

        private void MyBird(bool finished = false)
        {
            var me = new Entity("me");
            finish = new FinishState();
            if (finished) { finish.FinishedTick = 500; }
            me.Add(finish);
            registry.Add(me);

            actorGo = new GameObject("Actor_me");
            var view = actorGo.AddComponent<LOPEntityView>();
            var actor = actorGo.AddComponent<LOPActor>();
            actor.SetEntityId("me");
            actor.SetView(view);
            var visual = new GameObject("Visual_me");
            visual.transform.SetParent(actorGo.transform, false);
            typeof(LOPEntityView).GetField("_visualGameObject", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, visual);
            actors.Add(actor);
        }

        [Test]
        public void 내_새가_생기면_음악을_한_번_튼다()
        {
            store.userEntityId = null;
            Assert.DoesNotThrow(() => cues.Tick(), "판 시작 직후엔 내 id가 비어 있다");
            store.userEntityId = "me";
            cues.Tick();
            Assert.AreEqual(0, audio.MusicStarts, "몸이 아직 없다");

            MyBird();
            cues.Tick();
            cues.Tick();

            Assert.AreEqual(1, audio.MusicStarts);
        }

        [Test]
        public void 결승하면_음악을_끄고_느리게_꽃가루()
        {
            MyBird();
            cues.Tick();

            finish.FinishedTick = 500;
            cues.Tick();
            cues.Tick();

            Assert.AreEqual(1, audio.MusicStops);
            Assert.AreEqual(4 * FlappyRaceCues.ConfettiPerColor, bursts.Count, "4색 × 14조각, 한 번만");
            Assert.AreEqual(0.15f, warp.Warp.Advance(0.25f), 1e-4f, "0.25초 동안 그림 0.4배");
        }

        [Test]
        public void 탈락하면_음악만_끈다()
        {
            MyBird();
            cues.Tick();

            registry.Remove("me");   //  추격자에게 잡혔다
            cues.Tick();

            Assert.AreEqual(1, audio.MusicStops);
            Assert.AreEqual(0, bursts.Count);
            Assert.AreEqual(0f, warp.Warp.Advance(0.1f));
        }

        [Test]
        public void 이미_결승한_채로_들어오면_연출_없다()
        {
            MyBird(finished: true);
            cues.Tick();
            cues.Tick();

            Assert.AreEqual(0, audio.MusicStarts);
            Assert.AreEqual(0, bursts.Count);
        }

        [Test]
        public void 치우면_틀던_음악을_끈다()
        {
            MyBird();
            cues.Tick();

            cues.Dispose();
            cues.Dispose();

            Assert.AreEqual(1, audio.MusicStops, "두 번 치워도 한 번");
        }
    }
}
