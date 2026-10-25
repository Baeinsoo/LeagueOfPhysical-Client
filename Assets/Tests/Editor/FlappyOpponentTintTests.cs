using System.Reflection;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>상대 새는 차분한 색, 내 새는 그대로(spec §2). 막대의 점 색과 같은 칸(FlappyOpponentColors)을 쓴다.</summary>
    public class FlappyOpponentTintTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private readonly System.Collections.Generic.List<Object> spawned = new System.Collections.Generic.List<Object>();
        private EntityRegistry registry;
        private ActorRegistry actors;
        private FlappyOpponentColors colors;
        private FlappyOpponentTint tint;
        private Material body;

        [SetUp]
        public void SetUp()
        {
            registry = new EntityRegistry();
            actors = new ActorRegistry();
            colors = new FlappyOpponentColors();
            body = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BodyMaterial", color = Color.yellow };
            spawned.Add(body);
            tint = new FlappyOpponentTint(registry, actors, new FakeGameDataStore { userEntityId = "me" }, colors);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in spawned) { if (obj != null) { Object.DestroyImmediate(obj); } }
            spawned.Clear();
        }

        private MeshRenderer Bird(string id, EntityType kind = EntityType.Character)
        {
            var e = new Entity(id);
            e.Add(new EntityKind(kind));
            registry.Add(e);

            var actorGo = new GameObject("Actor_" + id);
            spawned.Add(actorGo);
            var view = actorGo.AddComponent<LOPEntityView>();
            var actor = actorGo.AddComponent<LOPActor>();
            actor.SetEntityId(id);
            actor.SetView(view);
            actorGo.AddComponent<StunAppearance>().SetEntity(actor);
            var visual = new GameObject("Visual_" + id);
            visual.transform.SetParent(actorGo.transform, false);
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = body;
            typeof(LOPEntityView).GetField("_visualGameObject", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, visual);
            actors.Add(actor);
            return renderer;
        }

        [Test]
        public void 상대는_색_칸의_색으로_칠한다()
        {
            var other = Bird("a");

            tint.Tick();

            Assert.AreEqual(colors.ColorOf("a"), other.sharedMaterial.color);
        }

        [Test]
        public void 내_새는_그대로()
        {
            var mine = Bird("me");

            tint.Tick();

            Assert.AreSame(body, mine.sharedMaterial);
        }

        [Test]
        public void 한_번_칠한_새는_다시_복제하지_않는다()
        {
            var other = Bird("a");
            tint.Tick();
            var first = other.sharedMaterial;

            tint.Tick();

            Assert.AreSame(first, other.sharedMaterial);
        }

        [Test]
        public void 새가_아니면_건드리지_않는다()
        {
            var thing = Bird("crate", EntityType.Item);

            tint.Tick();

            Assert.AreSame(body, thing.sharedMaterial);
        }

        //  경기 시작 직후처럼 gameDataStore.userEntityId를 아직 모를 때(빈 문자열)는 "나"를 아무 id와도 구분 못 해
        //  내 새까지 칠하고 tinted에 넣어 버릴 수 있다 — 나중에 진짜 id를 알아도 tinted.Contains로 걸려 영원히 칠해진 채로 남는다.
        [Test]
        public void 내_아이디를_모르면_아무도_칠하지_않고_나중에_알아도_내_새는_그대로()
        {
            var store = new FakeGameDataStore { userEntityId = "" };
            tint = new FlappyOpponentTint(registry, actors, store, colors);
            var mine = Bird("me");

            tint.Tick();

            Assert.AreSame(body, mine.sharedMaterial, "아이디를 모르는 틱에선 내 새도 칠하면 안 된다");

            store.userEntityId = "me";
            tint.Tick();

            Assert.AreSame(body, mine.sharedMaterial, "나중에 내 아이디를 알게 된 뒤에도 내 새는 그대로여야 한다");
        }

        [Test]
        public void 몸_머티리얼만_칠_대상이다()
        {
            Assert.IsTrue(FlappyOpponentTint.IsBodyMaterial(new Material(body) { name = "BodyMaterial" }));
            Assert.IsFalse(FlappyOpponentTint.IsBodyMaterial(new Material(body) { name = "BeakMaterial" }));
            Assert.IsFalse(FlappyOpponentTint.IsBodyMaterial(null));
        }
    }
}
