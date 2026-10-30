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

        static KnockbackWorld world;

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
            world = new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(),
                Config.CreateMoveSystem(floor, FlappyCorrectionFixture.NeverHit, ~0),
                new NoopMotionBridge(), floor, new KnockbackFireSystem(Config),
                new BodyCollisionSystem(BodySizes.CharacterRadius, BodySizes.CharacterHeight,
                    Config.BodyRestitution, new Vector3(1, 0, 1)),
                Config) { GameplayStartTick = 0 };
            world.SaveState(Tick);
            return new KnockbackServerCorrectionHandler(world);
        }

        //  총 값은 새 총(가득, 충전 시계 멈춤, 연사 대기 없음)과 같게 둔다 — 날림·피해 시험이 총 차이로 갈리지 않게.
        static EntitySnap Snap(long endTick, int percent, int ammo = 3, long nextReload = long.MaxValue, long cooldownEnd = 0)
            => new EntitySnap
            {
                entityId = "body-1", tick = Tick, knockbackEndTick = endTick, damagePercent = percent,
                knockbackAmmo = ammo, knockbackNextReloadTick = nextReload, knockbackCooldownEndTick = cooldownEnd,
            };

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
        public void 탄_수가_다르면_되감는다()
        {
            //  위치·날림·피해는 같다 — 서버는 입력을 못 받아 안 쐈고(가득) 나는 쐈다(2발).
            var handler = Make(long.MinValue, 0, out var body);
            var gun = body.Get<KnockbackGun>();
            Assert.AreEqual(3, gun.Ammo);
            Assert.IsTrue(handler.Matches(Tick, Snap(long.MinValue, 0)), "양성 대조 — 총이 같으면 그대로");

            gun.Ammo = 2;
            gun.NextReloadTick = Tick + 40;
            gun.CooldownEndTick = Tick + 13;
            world.SaveState(Tick);
            Assert.IsFalse(handler.Matches(Tick, Snap(long.MinValue, 0)), "탄 수가 갈리면 되감는다");
        }

        [Test]
        public void 충전_시계나_연사_대기가_다르면_되감는다()
        {
            var handler = Make(long.MinValue, 0, out var body);
            var gun = body.Get<KnockbackGun>();
            gun.Ammo = 2;
            gun.NextReloadTick = Tick + 40;
            gun.CooldownEndTick = Tick + 13;
            world.SaveState(Tick);
            Assert.IsTrue(handler.Matches(Tick, Snap(long.MinValue, 0, 2, Tick + 40, Tick + 13)), "양성 대조");
            Assert.IsFalse(handler.Matches(Tick, Snap(long.MinValue, 0, 2, Tick + 41, Tick + 13)));
            Assert.IsFalse(handler.Matches(Tick, Snap(long.MinValue, 0, 2, Tick + 40, Tick + 14)));
        }

        [Test]
        public void 적용하면_총도_서버_값이_되고_재생이_다시_쏜다()
        {
            //  나는 탄 0이라 못 쐈는데 서버는 1발이 있어 쐈다 — 서버 총을 받고 같은 입력(Fire)을 재생하면 쏘아져야 한다.
            var handler = Make(long.MinValue, 0, out var body);
            var gun = body.Get<KnockbackGun>();
            gun.Ammo = 0;
            gun.NextReloadTick = Tick + 40;

            handler.ApplyAuthoritative(body, Snap(long.MinValue, 0, 1, Tick + 20, Tick - 5), 0.02f);
            Assert.AreEqual(1, gun.Ammo);
            Assert.AreEqual(Tick + 20, gun.NextReloadTick);
            Assert.AreEqual(Tick - 5, gun.CooldownEndTick);

            body.Get<InputBuffer>().Current = new InputCommand { Fire = true, AimYaw = 90f };
            world.Tick(Tick + 1, 0.02f);
            Assert.AreEqual(1, world.Shots.Shots.Count, "재생이 같은 입력으로 쏘아야 한다");
            Assert.AreEqual(0, gun.Ammo);
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
