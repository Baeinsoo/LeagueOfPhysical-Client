using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackServerCorrectionHandlerTests
    {
        const long Tick = 100;

        static readonly KnockbackConfig Config = new KnockbackConfig(120f, 6.2f, 14f, 17f, 1.5f, 0f, 5f, 11f,
            30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f,
            new System.Collections.Generic.List<MountedArcheryZoneStageConfig>());

        class NoopMotionBridge : GameFramework.World.IMotionBridge
        {
            public void SyncTransforms() { }
            public System.Numerics.Vector3 Depenetrate(GameFramework.World.Entity entity) => System.Numerics.Vector3.Zero;
            public void Separate(GameFramework.World.Entity entity) { }
            public void PushMotion(GameFramework.World.Entity entity) { }
        }

        static KnockbackServerCorrectionHandler Make(
            long predictedEnd, int predictedPercent, out GameFramework.World.Entity body)
        {
            body = new GameFramework.World.Entity("body-1");
            body.Add(new GameFramework.World.Transform());
            body.Add(new GameFramework.World.Velocity());
            body.Add(new GameFramework.World.CapsuleShape(BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            body.Add(new GameFramework.World.GroundState { IsGrounded = true });
            body.Add(new InputBuffer());
            body.Add(new GameFramework.World.Simulated());
            body.Add(new KnockbackGun(Config.AmmoMax));
            body.Add(new KnockbackBody { KnockbackEndTick = predictedEnd, DamagePercent = predictedPercent });
            body.Add(new EntityKind(EntityType.Character));

            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(body);
            var floor = Config.CreateFloor();
            var world = new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(),
                Config.CreateMoveSystem(floor, FlappyCorrectionFixture.NeverHit, ~0),
                new NoopMotionBridge(), floor, new KnockbackFireSystem(Config),
                new BodyCollisionSystem(BodySizes.CharacterRadius, BodySizes.CharacterHeight,
                    Config.BodyRestitution, new Vector3(1, 0, 1)),
                Config) { GameplayStartTick = 0 };
            world.SaveState(Tick);
            return new KnockbackServerCorrectionHandler(world);
        }

        static EntitySnap Snap(long endTick, int percent)
            => new EntitySnap { entityId = "body-1", tick = Tick, knockbackEndTick = endTick, damagePercent = percent };

        [Test]
        public void 날림이_다르면_되감는다()
        {
            var handler = Make(long.MinValue, 0, out _);
            Assert.IsFalse(handler.Matches(Tick, Snap(Tick + 10, 0)));
        }

        [Test]
        public void 날림이_같으면_그대로()
        {
            var handler = Make(Tick + 9, 0, out _);
            Assert.IsTrue(handler.Matches(Tick, Snap(Tick + 10, 0)));
        }

        [Test]
        public void 피해가_다르면_되감는다()
        {
            var handler = Make(long.MinValue, 10, out _);
            Assert.IsFalse(handler.Matches(Tick, Snap(long.MinValue, 20)));
        }

        [Test]
        public void 기록이_없으면_위치_판정에_맡긴다()
        {
            var handler = Make(long.MinValue, 0, out _);
            Assert.IsTrue(handler.Matches(Tick + 5000, Snap(Tick + 5010, 99)));
        }

        [Test]
        public void 적용하면_서버_값이_된다()
        {
            var handler = Make(long.MinValue, 0, out var body);
            handler.ApplyAuthoritative(body, Snap(Tick + 10, 35), 0.02f);
            var b = body.Get<KnockbackBody>();
            Assert.AreEqual(Tick + 10, b.KnockbackEndTick);
            Assert.AreEqual(35, b.DamagePercent);
        }
    }
}
