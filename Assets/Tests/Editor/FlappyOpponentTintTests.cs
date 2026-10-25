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

        [Test]
        public void 몸_머티리얼만_칠_대상이다()
        {
            Assert.IsTrue(FlappyOpponentTint.IsBodyMaterial(new Material(body) { name = "BodyMaterial" }));
            Assert.IsFalse(FlappyOpponentTint.IsBodyMaterial(new Material(body) { name = "BeakMaterial" }));
            Assert.IsFalse(FlappyOpponentTint.IsBodyMaterial(null));
        }
    }
}
