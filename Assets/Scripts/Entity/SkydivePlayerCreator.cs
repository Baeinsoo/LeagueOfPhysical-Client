using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Skydive의 플레이어 몸(클라). 체력·마나·레벨·어빌리티가 없다 — 이 게임에 그런 개념이 없다.
    /// 자세·스태미나 컴포넌트는 슬라이스 2가 여기에 더한다.
    /// </summary>
    public class SkydivePlayerCreator : ICharacterCreator
    {
        // 몸 크기. 서버(SkydivePlayerCreator)도 같은 값을 상수로 든다 — 슬라이스 2에서
        // TbSkydiveConfig로 옮길 때 한쪽만 옮기면 클·서 캡슐 크기가 갈라진다(컴파일도 테스트도
        // 못 잡는다). 옮길 땐 반드시 같이 옮길 것.
        private const float BodyRadius = 0.4f;
        private const float BodyHeight = 1.8f;

        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public SkydivePlayerCreator(
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
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodyRadius, BodyHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                // 입력은 내 몸만 갖는다. Simulated는 EntityBinder가 동기화 정책을 보고 붙인다.
                worldEntity.Add(new InputBuffer());
            }
            entityRegistry.Add(worldEntity);

            if (isUserEntity)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered skydive body {worldEntity.Id}");
        }
    }
}
