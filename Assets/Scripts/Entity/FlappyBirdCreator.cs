using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race의 플레이어 몸(새)을 만든다. 캐릭터와 달리 체력·마나·레벨·어빌리티가 없다 —
    /// 이 게임에는 그런 개념이 없으므로 안 붙인다.
    /// </summary>
    public class FlappyBirdCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public FlappyBirdCreator(
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
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
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            // EntityBinder가 뷰·보간을 붙일 때 보는 값이라 Character로 둔다 — 새도 플레이어가 조종하는 몸이다.
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());

            // 클라의 CharacterCreationData엔 userId가 없다 — "내 몸인가"는 gameDataStore.userEntityId로만 판단한다(기존 CharacterCreator와 동일 관례). Ownership은 서버에서만 붙인다.
            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                worldEntity.Add(new InputBuffer());
            }
            entityRegistry.Add(worldEntity);

            if (isUserEntity)
            {
                playerContext.entityId = creationData.entityId;   // .actor는 EntityBinder가 뷰 생성 후 세팅
            }

            Debug.Log($"[World] Registered flappy bird {worldEntity.Id}");
        }
    }
}
