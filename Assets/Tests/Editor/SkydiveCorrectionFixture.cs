namespace LOP.Tests
{
    /// <summary>
    /// <see cref="SkydiveServerCorrectionHandler"/>를 EditMode에서 진짜로 돌리기 위한 조립 도구.
    /// 핸들러가 필요로 하는 건 "그 틱에 뭘 예측했나"(SkydiveWorld) 하나뿐이다 — 실물 월드를 써야
    /// 저장된 예측이 진짜 시뮬 결과가 된다. 틱 간격은 되감기를 구동하는 쪽이 넘기는 값이라
    /// 테스트가 <see cref="TickInterval"/>을 직접 준다. (선례: FlappyCorrectionFixture)
    /// </summary>
    internal static class SkydiveCorrectionFixture
    {
        public const float TickInterval = 0.02f;

        //  스냅이 말하는 틱(서버가 그 사진을 찍은 틱). 핸들러는 클라의 현재 틱을 받지 않으므로
        //  기준 틱을 잘못 고르는 실수 자체가 구조적으로 불가능하다 — Flappy 쪽 기록과 같은 이유.
        public const long SnapTick = 100;

        public static SkydiveConfig Config()
            => new SkydiveConfig(
                spreadFallSpeed: 60f, diveFallSpeed: 90f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 9f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 29f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f,
                groundMoveSpeed: 4f, groundAccel: 100f, jumpPower: 11f, poseClearance: 5f, fallBrake: 150f);

        /// <summary>다이버 한 명이 든 월드 + 그 월드를 보는 핸들러.</summary>
        public static SkydiveServerCorrectionHandler Handler(
            out SkydiveWorld world, out GameFramework.World.Entity diver)
        {
            var registry = new GameFramework.World.EntityRegistry();
            diver = Diver("diver-1");
            registry.Add(diver);
            world = new SkydiveWorld(registry, new GameFramework.World.WorldEventBuffer(),
                new SkydiveMoveSystem(), new StaminaSystem(), Config(),
                new EmptySky(), layerMask: ~0);
            world.GameplayStartTick = 0;   // 출발 게이트는 이 파일의 관심사가 아니다
            return new SkydiveServerCorrectionHandler(world);
        }

        public static EntitySnap Snap(string entityId, long tick, float postureAxis = 0f,
            bool gliding = false, float stamina = 0f, float emergencyRemaining = 0f)
            => new EntitySnap
            {
                entityId = entityId,
                tick = tick,
                postureAxis = postureAxis,
                gliding = gliding,
                stamina = stamina,
                emergencyRemaining = emergencyRemaining,
            };

        private static GameFramework.World.Entity Diver(string id)
        {
            var entity = new GameFramework.World.Entity(id);
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());
            entity.Add(new EntityKind(EntityType.Character));
            entity.Add(new Posture());
            entity.Add(new MotionState());
            entity.Add(new Stamina { Current = 100f });
            entity.Add(new InputBuffer());
            entity.Add(new GameFramework.World.Simulated());   // 이게 있어야 SaveState가 이 다이버를 담는다
            return entity;
        }

        //  이 파일이 재는 것은 자세·스태미나 보정뿐이라 맵이 필요 없다. 아무것도 없는 하늘로 두면
        //  다이버가 그냥 떨어지고, 지형 때문에 예측이 흔들리는 일이 생기지 않는다.
        private sealed class EmptySky : GameFramework.Physics.ICollisionQuery
        {
            public GameFramework.Physics.CollisionHit CapsuleCast(UnityEngine.Vector3 point1,
                UnityEngine.Vector3 point2, float radius, UnityEngine.Vector3 direction,
                float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit Raycast(UnityEngine.Vector3 origin,
                UnityEngine.Vector3 direction, float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit[] OverlapSphere(UnityEngine.Vector3 center,
                float radius, int layerMask)
                => System.Array.Empty<GameFramework.Physics.CollisionHit>();
        }
    }
}
