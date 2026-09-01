using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>Skydive의 플레이어 몸(클라). 체력·마나·레벨·어빌리티가 없다 — 이 게임에 그런 개념이 없다.</summary>
    public class SkydivePlayerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly SkydiveConfig config;

        public SkydivePlayerCreator(
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            SkydiveConfig config)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());
            worldEntity.Add(new GameFramework.World.CapsuleShape(config.BodyRadius, config.BodyHeight));
            // 발 딛고 있는지는 이동 커널이 매 틱 다시 계산해 여기 적는다 — 스태미나 회복이 이 값을 읽는다.
            worldEntity.Add(new GameFramework.World.GroundState());
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;

            //  남의 몸도 입력 버퍼를 갖는다 — 서버가 남의 입력을 되뿌려 주고(EntityInputBroadcastSystem)
            //  RemoteInputSystem이 여기에 채운다. 이 게임은 남도 굴리므로(CharactersPredictedSyncPolicy)
            //  그 입력이 실제로 읽힌다: 남의 자세와 좌우 이동이 내 화면에서도 서버와 같은 규칙으로 나온다.
            //  버퍼가 없으면 RemoteInputSystem이 그 몸을 건너뛰어, 남이 아무 입력도 안 넣은 것처럼 굴러간다.
            //  Simulated는 EntityBinder가 동기화 정책을 보고 붙인다.
            worldEntity.Add(new InputBuffer());
            worldEntity.Add(new Posture());
            worldEntity.Add(new MotionState());
            worldEntity.Add(new Stamina { Current = config.StaminaMax });
            entityRegistry.Add(worldEntity);

            if (isUserEntity)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered skydive body {worldEntity.Id}");
        }
    }
}
