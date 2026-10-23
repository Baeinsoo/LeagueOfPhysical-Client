using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// MountedArchery의 기수 몸(클라). 서버와 같은 부품이되 소유자·시뮬 표식은 없다 —
    /// 누구 몸인지는 서버가 알고, 굴릴지는 동기화 정책이 정한다.
    /// </summary>
    public class MountedArcheryPlayerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public MountedArcheryPlayerCreator(IGameDataStore gameDataStore, IPlayerContext playerContext,
                                           GameFramework.World.EntityRegistry entityRegistry)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity());
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            if (creationData.look != null)
            {
                worldEntity.Add(creationData.look);
            }
            worldEntity.Add(new ArcheryAim());
            worldEntity.Add(new Mount());
            worldEntity.Add(new InputBuffer());

            // 말 몸 폭·키 근사. 서버와 같은 값이어야 예측이 안 어긋난다. 이 모양이 없으면 키네마틱 이동이 몸을 조용히 건너뛴다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(0.8f, 2.6f));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            entityRegistry.Add(worldEntity);

            if (gameDataStore.userEntityId == creationData.entityId)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered mounted archer {worldEntity.Id}");
        }
    }
}
