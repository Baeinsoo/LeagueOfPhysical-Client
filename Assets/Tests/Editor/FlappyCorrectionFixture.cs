using System.Threading.Tasks;
using GameFramework.Physics;

namespace LOP.Tests
{
    /// <summary>
    /// <see cref="FlappyServerCorrectionHandler"/>를 EditMode에서 진짜로 돌리기 위한 조립 도구.
    /// 핸들러가 필요로 하는 건 "그 틱에 뭘 예측했나"(FlappyWorld)와 틱 간격(러너) 둘뿐이라,
    /// 월드는 실물을 쓰고 러너만 스텁으로 세운다 — 실물 월드를 써야 저장된 예측이 진짜 시뮬 결과가 된다.
    /// </summary>
    internal static class FlappyCorrectionFixture
    {
        public const float TickInterval = 0.02f;

        //  스냅이 말하는 틱(서버가 그 사진을 찍은 틱)과 클라의 현재 틱은 다르다 — 클라가 앞서 달린다.
        //  그 차이를 테스트가 재현해야, 기준 틱을 잘못 고른 코드가 실제로 빨간불이 된다.
        public const long SnapTick = 100;
        public const long ClientLeadTicks = 9;

        public static ICollisionQuery NeverHit => new NeverHitQuery();
        public static ICollisionQuery AlwaysHit => new AlwaysHitQuery();

        public static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f);

        /// <summary>새 한 마리가 든 월드 + 그 월드를 보는 핸들러.</summary>
        public static FlappyServerCorrectionHandler Handler(
            ICollisionQuery collisionQuery, out FlappyWorld world, out GameFramework.World.Entity bird)
        {
            var registry = new GameFramework.World.EntityRegistry();
            bird = Bird("bird-1");
            registry.Add(bird);
            world = new FlappyWorld(registry, new GameFramework.World.WorldEventBuffer(),
                new FlappyMoveSystem(Config()), new FlappyBodyCollisionSystem(Config()),
                new FlappyStunSystem(Config()), collisionQuery, new NoopMotionBridge(), layerMask: ~0);
            world.GameplayStartTick = 0;   // 출발 게이트는 이 파일의 관심사가 아니다
            return new FlappyServerCorrectionHandler(world, new StubRunner(TickInterval));
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

#pragma warning disable 0067   // 스텁이라 이벤트를 발생시키지 않는다
        private class StubTickUpdater : GameFramework.Runner.ITickUpdater
        {
            public StubTickUpdater(double interval, long tick)
            {
                this.interval = interval;
                this.tick = tick;
            }

            public event System.Action<long> onTick;

            public long tick { get; }
            public double interval { get; }
            public double elapsedTime => tick * interval;
            public long processibleTick => tick;
            public double deltaTime => interval;
            public int catchUpCappedCount => 0;
            public long maxTicksBehind => 0;

            public void Run(long tick, double interval, double elapsedTime) { }
            public void Stop() { }
        }

        /// <summary>핸들러가 실제로 읽는 건 tickUpdater.interval 하나뿐이라 나머지는 비워 둔다.</summary>
        private class StubRunner : GameFramework.Runner.IRunner
        {
            public StubRunner(double interval)
            {
                //  현재 틱을 일부러 스냅 틱보다 앞에 둔다 — 핸들러가 이 값을 기준으로 삼기 시작하면
                //  테스트가 즉시 빨간불이 되도록.
                tickUpdater = new StubTickUpdater(interval, SnapTick + ClientLeadTicks);
            }

            public event System.Action<GameFramework.Runner.RunnerState> onGameStateChanged;

            public GameFramework.Runner.RunnerState gameState => GameFramework.Runner.RunnerState.Playing;
            public GameFramework.Runner.ITickUpdater tickUpdater { get; }
            public GameFramework.Netcode.INetworkTime networkTime => null;
            public bool initialized => true;

            public void Run(long tick, double interval, double elapsedTime) { }
            public void Stop() { }
            public void RegisterSystem<TPhase>(GameFramework.Runner.ITickSystem system) { }
            public void UnregisterSystem(GameFramework.Runner.ITickSystem system) { }
            public Task InitializeAsync() => Task.CompletedTask;
            public Task DeinitializeAsync() => Task.CompletedTask;
        }
#pragma warning restore 0067
    }
}
