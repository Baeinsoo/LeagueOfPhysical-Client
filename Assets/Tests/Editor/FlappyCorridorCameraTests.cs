using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class FlappyCorridorCameraTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { Object.DestroyImmediate(go); }
            spawned.Clear();
        }

        private static FlappyConfig Config(float bodyRadius = 0.45f)
            => new FlappyConfig(forwardSpeed: 6.8f, flapImpulse: 10.125f, gravity: 59f, maxFallSpeed: 11.25f,
                                bodyRadius: bodyRadius, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 1.2f, invulnTime: 0.6f,
                                dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

        private FlappyCorridorLine SpawnLine(params Vector2[] points)
        {
            var go = new GameObject("CorridorLine");
            spawned.Add(go);
            var line = go.AddComponent<FlappyCorridorLine>();
            line.Points = points;
            return line;
        }

        private static EntityRegistry Registry(out Entity bird, string id, float x, float y)
        {
            var registry = new EntityRegistry();
            bird = new Entity(id);
            bird.Add(new EntityKind(EntityType.Character));
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, y, 0f) });
            registry.Add(bird);
            return registry;
        }

        private static FlappySpectate Watching(EntityRegistry registry, string id)
        {
            var spectate = new FlappySpectate(registry, new FakeGameDataStore { userEntityId = id });
            spectate.Refresh();
            return spectate;
        }

        private static void Run(FlappyCorridorCamera sut, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.02f) { sut.Tick(0.02f); }
        }

        [Test]
        public void 표시가_없으면_오프셋이_0으로_유지된다()
        {
            var registry = Registry(out _, "me", 5f, 0f);
            var spectate = Watching(registry, "me");
            var sut = new FlappyCorridorCamera(spectate, registry, null, Config());
            sut.applyPivot = _ => { };

            Run(sut, 3f);

            Assert.AreEqual(0f, sut.Offset, 1e-4f);
        }

        [Test]
        public void 충분한_틱_뒤_오프셋이_중심에서_새_중심을_뺀_값에_가까워진다()
        {
            SpawnLine(new Vector2(0f, 0f), new Vector2(10f, 10f));   // CenterAt(5) == 5
            var registry = Registry(out _, "me", 5f, 0f);            // 발밑 y=0 → 몸 중심 0.45
            var spectate = Watching(registry, "me");
            var sut = new FlappyCorridorCamera(spectate, registry, null, Config());
            var pivots = new List<Vector3>();
            sut.applyPivot = p => pivots.Add(p);

            Run(sut, 3f);

            Assert.AreEqual(5f - 0.45f, sut.Offset, 0.05f);
            Assert.AreEqual(sut.Offset, pivots[pivots.Count - 1].y, 1e-4f);
        }

        [Test]
        public void TargetOffset_중심에서_새_중심을_뺀다()
        {
            Assert.AreEqual(4.55f, FlappyCorridorCamera.TargetOffset(centerY: 5f, birdCenterY: 0.45f), 1e-4f);
            Assert.AreEqual(-2f, FlappyCorridorCamera.TargetOffset(centerY: 3f, birdCenterY: 5f), 1e-4f);
        }

        [Test]
        public void Dispose하면_카메라_중심을_제자리로()
        {
            SpawnLine(new Vector2(0f, 0f), new Vector2(10f, 10f));
            var registry = Registry(out _, "me", 5f, 0f);
            var spectate = Watching(registry, "me");
            var sut = new FlappyCorridorCamera(spectate, registry, null, Config());
            var pivots = new List<Vector3>();
            sut.applyPivot = p => pivots.Add(p);

            Run(sut, 1f);
            sut.Dispose();

            Assert.AreEqual(Vector3.zero, pivots[pivots.Count - 1]);
            Assert.AreEqual(0f, sut.Offset, 1e-4f);
        }
    }
}

