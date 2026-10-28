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
        private FakeFlappyAudio audio;
        private FlappyStun stun;
        private FlappyFlapCombo combo;
        private FlappyMapRulesField rules;
        private FlappyFlapStreak streak;
        private FlappyFlapComboFx fx;
        private GameObject actorGo;

        [SetUp]
        public void SetUp()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            stun = new FlappyStun();
            combo = new FlappyFlapCombo();
            me.Add(stun);
            me.Add(new FinishState());
            me.Add(new FlappyDash { Charge = 0f });
            me.Add(combo);
            registry.Add(me);
            store = new RigGameDataStore { userEntityId = "me" };
            rules = new FlappyMapRulesField();
            rules.Set(chaser: false, manualDash: true);
            audio = new FakeFlappyAudio();
            streak = new FlappyFlapStreak(registry, store, rules,
                                          new MatchRecordSheet(() => (6, 2), new FakePersonalRecordStore()));
            streak.Tick();
            actors = new ActorRegistry();
            fx = new FlappyFlapComboFx(streak, actors, store, default, audio);
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            if (actorGo != null) { UnityEngine.Object.DestroyImmediate(actorGo); }
        }

        //  시뮬이 한 틱에 하나씩 받은 것처럼 — 장마다 읽힌다.
        private void Flaps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                combo.Count++;
                streak.Tick();
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
        public void 연출은_고비_화음만_내고_날갯짓_소리는_건드리지_않는다()
        {
            //  10-26 사용자: 올라가는 음 없음 — 날갯짓 소리는 FlappyFlapSound가 누를 때마다 같은 소리로 낸다. 연출은 고비 화음만.
            Flaps(30);

            Assert.AreEqual(2, audio.Played.Count, "10·25");
            Assert.IsTrue(audio.Played.All(s => s == FlappySfx.Milestone));
        }

        [Test]
        public void 기절하면_꼬리를_끈다()
        {
            Flaps(25);
            fx.Fx.Resolve(new Vector2(500f, 400f), 18f, 2f);
            fx.Fx.RecordTrail(Vector3.zero, racing: true);
            Assert.AreEqual(1, fx.Fx.Trail.Count);

            stun.StunRemaining = 0.8f;
            combo.Count = 0;   //  시뮬이 새로 기절하며 끊었다
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

        [Test]
        public void 게이지를_준_고비는_연출에_대시플러스로_넘긴다()
        {
            Flaps(10);
            fx.Fx.Resolve(new Vector2(500f, 400f), 18f, 2f);

            Assert.AreEqual("좋아!", fx.Fx.WordText);
            Assert.IsTrue(fx.Fx.WordDashCue);
        }

        [Test]
        public void 맵_고비에서_화음을_낸다()
        {
            FlappyFlapComboRules.TryCreate(new[] { 3 }, new[] { 1f }, out var custom);
            rules.Set(chaser: false, manualDash: true, flapCombo: custom);

            Flaps(3);
            Assert.AreEqual(1, audio.Played.Count(s => s == FlappySfx.Milestone), "3");
            Flaps(7);
            Assert.AreEqual(1, audio.Played.Count(s => s == FlappySfx.Milestone), "10은 이 맵의 고비가 아니다");
        }
    }
}
