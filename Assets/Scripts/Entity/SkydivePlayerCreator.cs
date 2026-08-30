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
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                // 입력은 내 몸만 갖는다. Simulated는 EntityBinder가 동기화 정책을 보고 붙인다.
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new Posture());
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
