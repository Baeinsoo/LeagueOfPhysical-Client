using GameFramework.Physics;

namespace LOP.Tests
{
    /// <summary>
    /// <see cref="FlappyServerCorrectionHandler"/>를 EditMode에서 진짜로 돌리기 위한 조립 도구.
    /// 핸들러가 필요로 하는 건 "그 틱에 뭘 예측했나"(FlappyWorld) 하나뿐이다 — 실물 월드를 써야
    /// 저장된 예측이 진짜 시뮬 결과가 된다. 틱 간격은 되감기를 구동하는 쪽이 넘기는 값이라
    /// 테스트가 <see cref="TickInterval"/>을 직접 준다.
    /// </summary>
    internal static class FlappyCorrectionFixture
    {
        public const float TickInterval = 0.02f;

        //  스냅이 말하는 틱(서버가 그 사진을 찍은 틱). 클라의 현재 틱은 이보다 ~9틱 앞서지만,
        //  핸들러는 이제 그 값에 손이 닿지 않는다(러너를 안 받는다) — 기준 틱을 잘못 고르는
        //  실수 자체가 구조적으로 불가능해졌다.
        public const long SnapTick = 100;

        public static ICollisionQuery NeverHit => new NeverHitQuery();
        public static ICollisionQuery AlwaysHit => new AlwaysHitQuery();

        public static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f);

        /// <summary>새 한 마리가 든 월드 + 그 월드를 보는 핸들러.</summary>
        public static FlappyServerCorrectionHandler Handler(
            ICollisionQuery collisionQuery, out FlappyWorld world, out GameFramework.World.Entity bird)
        {
            var registry = new GameFramework.World.EntityRegistry();
            bird = Bird("bird-1");
            registry.Add(bird);
            world = new FlappyWorld(registry, new GameFramework.World.WorldEventBuffer(),
                new FlappyMoveSystem(Config()),
                new FlappyStunSystem(Config()),
                new FlappyDashSystem(Config()), collisionQuery, new NoopMotionBridge(), layerMask: ~0);
            world.GameplayStartTick = 0;   // 출발 게이트는 이 파일의 관심사가 아니다
            return new FlappyServerCorrectionHandler(world);
        }

        public static EntitySnap Snap(string entityId, long tick, long stunEndTick = 0, long invulnEndTick = 0)
            => new EntitySnap
            {
                entityId = entityId,
                tick = tick,
                stunEndTick = stunEndTick,
                invulnEndTick = invulnEndTick,
            };

        private static GameFramework.World.Entity Bird(string id)
        {
            var entity = new GameFramework.World.Entity(id);
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());
            entity.Add(new GameFramework.World.CapsuleShape(Config().BodyRadius, Config().BodyHeight));
            entity.Add(new EntityKind(EntityType.Character));
            entity.Add(new FlappyStun());
            entity.Add(new InputBuffer());
            entity.Add(new GameFramework.World.Simulated());   // 이게 있어야 SaveState가 이 새를 담는다
            return entity;
        }

        private class NeverHitQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2, float radius,
                UnityEngine.Vector3 direction, float distance, int layerMask) => CollisionHit.None;
            public CollisionHit Raycast(UnityEngine.Vector3 origin, UnityEngine.Vector3 direction, float distance, int layerMask)
                => CollisionHit.None;
            public CollisionHit[] OverlapSphere(UnityEngine.Vector3 center, float radius, int layerMask)
                => System.Array.Empty<CollisionHit>();
        }

        private class AlwaysHitQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2, float radius,
                UnityEngine.Vector3 direction, float distance, int layerMask)
                => new CollisionHit(true, 0f, UnityEngine.Vector3.up, p1, null);
            public CollisionHit Raycast(UnityEngine.Vector3 origin, UnityEngine.Vector3 direction, float distance, int layerMask)
                => CollisionHit.None;
            public CollisionHit[] OverlapSphere(UnityEngine.Vector3 center, float radius, int layerMask)
                => System.Array.Empty<CollisionHit>();
        }

        /// <summary>물리 바디가 없는 EditMode 테스트라 아무 일도 하지 않는다.</summary>
        private class NoopMotionBridge : GameFramework.World.IMotionBridge
        {
            public void SyncTransforms() { }
            public System.Numerics.Vector3 Depenetrate(GameFramework.World.Entity entity) => System.Numerics.Vector3.Zero;
            public void Separate(GameFramework.World.Entity entity) { }
            public void PushMotion(GameFramework.World.Entity entity) { }
        }
    }
}
