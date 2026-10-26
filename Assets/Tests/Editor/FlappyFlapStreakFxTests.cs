using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 머리 위 "+N"의 그림 주인(spec §7, final-review F1): 판 시작 직후(앱을 켠 뒤 첫 판) 내 id가 아직 비어 있을 때
    /// <see cref="ActorRegistry.Get"/>(null 키)이 <c>ArgumentNullException</c>을 던지면 안 된다 — <see cref="FlappyFinishRecorder"/>·
    /// <see cref="FlappyFlapStreak"/>과 같은 가드를 <see cref="FlappyFlapStreakFx.MyVisual"/>에도 둔다.
    /// </summary>
    public class FlappyFlapStreakFxTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private ActorRegistry actors;
        private FakeGameDataStore store;
        private GameObject actorGo;

        [SetUp]
        public void SetUp()
        {
            actors = new ActorRegistry();
            store = new FakeGameDataStore();
        }

        [TearDown]
        public void TearDown()
        {
            if (actorGo != null) { Object.DestroyImmediate(actorGo); }
        }

        //  streak·config는 MyVisual()이 쓰지 않는다(값형 FlappyConfig라 기본값으로).
        private FlappyFlapStreakFx Fx() => new FlappyFlapStreakFx(null, actors, store, default);

        [Test]
        public void 내_id가_비어_있으면_예외_없이_null을_돌려준다()
        {
            store.userEntityId = null;
            Transform result = null;
            Assert.DoesNotThrow(() => result = Fx().MyVisual(), "앱을 켠 뒤 첫 판에서는 GameInfoToC가 오기 전까지 id가 null이다");
            Assert.IsNull(result);

            store.userEntityId = string.Empty;
            Assert.DoesNotThrow(() => result = Fx().MyVisual());
            Assert.IsNull(result);
        }

        [Test]
        public void 등록된_내_새의_그림을_돌려준다()
        {
            store.userEntityId = "me";
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

            Assert.AreSame(visual.transform, Fx().MyVisual());
        }
    }
}
