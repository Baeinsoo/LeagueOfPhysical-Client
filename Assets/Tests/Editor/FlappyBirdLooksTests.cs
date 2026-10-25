using System.Collections.Generic;
using System.Reflection;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 새마다 "그림 자리 + 기절 중인가"를 한 곳에서 읽는다. 내 새는 예측 FlappyStun(서버 교정도 여기 들어온다),
    /// 남의 새는 스냅이 FlappyStun을 안 채우므로 StunAppearance가 그리는 상태(스냅의 기절 끝 틱)로 읽는다.
    /// </summary>
    public class FlappyBirdLooksTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        private readonly List<StunAppearance> appearances = new List<StunAppearance>();
        private ActorRegistry actors;
        private EntityRegistry registry;
        private FakeGameDataStore store;
        private FlappyBirdLooks looks;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            actors = new ActorRegistry();
            registry = new EntityRegistry();
            store = new FakeGameDataStore { userEntityId = "me" };
            looks = new FlappyBirdLooks(actors, registry, store);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            spawned.Add(material);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var a in appearances) { if (a != null) { a.Cleanup(); } }
            appearances.Clear();
            foreach (var o in spawned) { if (o != null) { UnityEngine.Object.DestroyImmediate(o); } }
            spawned.Clear();
        }

        //  로드가 끝나 그림이 붙은 새(그림엔 렌더러 하나) — LOPEntityView의 private 필드에 꽂는다(StunAppearanceTintTests와 같다).
        private StunAppearance Bird(string id, bool withVisual = true)
        {
            var go = new GameObject("Actor_" + id);
            spawned.Add(go);
            var view = go.AddComponent<LOPEntityView>();
            var actor = go.AddComponent<LOPActor>();
            actor.SetEntityId(id);
            actor.SetView(view);
            var appearance = go.AddComponent<StunAppearance>();
            appearance.SetEntity(actor);
            appearances.Add(appearance);
            if (withVisual)
            {
                var visual = new GameObject("Visual_" + id);
                visual.transform.SetParent(go.transform, false);
                visual.AddComponent<MeshRenderer>().sharedMaterial = material;
                typeof(LOPEntityView).GetField("_visualGameObject", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(view, visual);
            }
            actors.Add(actor);
            return appearance;
        }

        private FlappyBirdLook Only(string id)
        {
            var list = new List<FlappyBirdLook>();
            looks.Fill(list);
            return list.Find(b => b.Id == id);
        }

        [Test]
        public void 내_새는_예측_기절을_읽는다()
        {
            Bird("me");
            var entity = new Entity("me");
            var stun = new FlappyStun();
            entity.Add(stun);
            registry.Add(entity);

            Assert.IsFalse(Only("me").Stunned);
            stun.StunRemaining = 1.2f;
            var me = Only("me");
            Assert.IsTrue(me.Mine);
            Assert.IsTrue(me.Stunned, "StunAppearance를 안 거쳐도 — 예측·교정 값이 바로 보인다");
        }

        [Test]
        public void 남의_새는_그리는_상태로_읽는다()
        {
            var other = Bird("other");
            Assert.IsFalse(Only("other").Stunned);

            other.SetState(StunVisual.Stunned);   //  스냅이 도착하면 보간기가 이렇게 부른다

            var look = Only("other");
            Assert.IsFalse(look.Mine);
            Assert.IsTrue(look.Stunned);
            Assert.IsNotNull(look.Visual);
        }

        [Test]
        public void 그림이_없는_새는_빼고_내_id가_없어도_안_깨진다()
        {
            Bird("loading", withVisual: false);
            Bird("other");
            store.userEntityId = null;

            var list = new List<FlappyBirdLook>();
            Assert.DoesNotThrow(() => looks.Fill(list));

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("other", list[0].Id);
            Assert.IsFalse(list[0].Mine);
        }

        [Test]
        public void 그림이_붙기_전엔_기절_상태도_없다()
        {
            var bare = Bird("bare", withVisual: false);

            bare.SetState(StunVisual.Stunned);

            Assert.AreEqual(StunVisual.None, bare.State, "그림이 없으면 아무것도 칠하지 않았다 — 칠한 것만 말한다");
        }
    }
}
