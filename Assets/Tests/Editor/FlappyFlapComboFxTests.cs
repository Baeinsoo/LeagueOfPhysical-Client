using System.Linq;
using System.Reflection;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 연출의 주인(spec §7, 사용자 결정 10-26): 먹힌 날갯짓마다 연출 흐름(<see cref="FlapComboFx"/>)에 수를 넣고,
    /// 고비(10·25·50·100)엔 조용한 화음을 한 번 낸다. 날갯짓 소리는 원래 소리 그대로 — 콤보마다 음이 오르지 않는다.
    /// 내 새가 기절에 들어가면 꼬리를 끈다. 판 시작 직후 내 id가 비어 있어도 예외가 없어야 한다(final-review F1).
    /// </summary>
    public class FlappyFlapComboFxTests
    {
        private ActorRegistry actors;
        private RigGameDataStore store;
        private RigTickUpdater clock;
        private FlappyFlapSound sound;
        private FakeFlappyAudio audio;
        private FlappyStun stun;
        private FlappyFlapStreak streak;
        private FlappyFlapComboFx fx;
        private GameObject actorGo;

        [SetUp]
        public void SetUp()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            stun = new FlappyStun();
            me.Add(stun);
            me.Add(new FinishState());
            registry.Add(me);
            store = new RigGameDataStore { userEntityId = "me" };
            var start = new MatchStartState();
            start.Update(100, 2, 2);
            clock = new RigTickUpdater { interval = 0.02d, tick = 150 };
            var runner = new RigRunner { tickUpdater = clock };
            audio = new FakeFlappyAudio();
            sound = new FlappyFlapSound(registry, store, start, runner, audio);
            streak = new FlappyFlapStreak(registry, store, runner, sound,
                                          new MatchRecordSheet(() => (6, 2), new FakePersonalRecordStore()));
            streak.Tick();
            actors = new ActorRegistry();
            fx = new FlappyFlapComboFx(streak, actors, store, default, audio);
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            streak.Dispose();
            if (actorGo != null) { Object.DestroyImmediate(actorGo); }
        }

        private void Flaps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                clock.tick++;
                sound.OnFlap();
            }
        }

        [Test]
        public void 날갯짓마다_연출에_수를_넣는다()
        {
            Flaps(3);
            fx.Fx.Resolve(new Vector2(500f, 400f), 18f, 2f);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, fx.Fx.Numbers.Select(n => n.Count).ToArray());
        }

        [Test]
        public void 고비에만_조용한_화음을_한_번()
        {
            Flaps(9);
            Assert.AreEqual(0, audio.Played.Count(s => s == FlappySfx.Milestone));
            Flaps(1);
            Assert.AreEqual(1, audio.Played.Count(s => s == FlappySfx.Milestone), "10");
            Flaps(15);
            Assert.AreEqual(2, audio.Played.Count(s => s == FlappySfx.Milestone), "25");
            Flaps(75);
            Assert.AreEqual(4, audio.Played.Count(s => s == FlappySfx.Milestone), "50·100");
        }

        [Test]
        public void 날갯짓_소리는_콤보와_무관하게_같은_소리()
        {
            //  10-26 사용자: 올라가는 음 없음 — 날갯짓마다 같은 Flap 한 번.
            Flaps(30);
            Assert.AreEqual(30, audio.Played.Count(s => s == FlappySfx.Flap));
            Assert.IsTrue(audio.Played.All(s => s == FlappySfx.Flap || s == FlappySfx.Milestone));
        }

        [Test]
        public void 기절하면_꼬리를_끈다()
        {
            Flaps(25);
            fx.Fx.Resolve(new Vector2(500f, 400f), 18f, 2f);
            fx.Fx.RecordTrail(Vector3.zero, racing: true);
            Assert.AreEqual(1, fx.Fx.Trail.Count);

            stun.StunRemaining = 0.8f;
            streak.Tick();

            Assert.AreEqual(0, fx.Fx.Trail.Count);
            Assert.AreEqual(0, fx.Fx.Count);
        }

        [Test]
        public void 내_id가_비어_있으면_예외_없이_null을_돌려준다()
        {
            store.userEntityId = null;
            UnityEngine.Transform result = null;
            Assert.DoesNotThrow(() => result = fx.MyVisual(), "앱을 켠 뒤 첫 판에서는 GameInfoToC가 오기 전까지 id가 null이다");
            Assert.IsNull(result);

            store.userEntityId = string.Empty;
            Assert.DoesNotThrow(() => result = fx.MyVisual());
            Assert.IsNull(result);
        }

        [Test]
        public void 등록된_내_새의_그림을_돌려준다()
        {
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

            Assert.AreSame(visual.transform, fx.MyVisual());
        }
    }
}
