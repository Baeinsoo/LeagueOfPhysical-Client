using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 넉백 생존전의 몸(클라). 서버와 같은 부품이되 소유자·시뮬 표식은 없다 —
    /// 누구 몸인지는 서버가 알고, 굴릴지는 동기화 정책이 정한다.
    /// </summary>
    public class KnockbackPlayerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly KnockbackConfig config;

        public KnockbackPlayerCreator(IGameDataStore gameDataStore, IPlayerContext playerContext,
                                      GameFramework.World.EntityRegistry entityRegistry, KnockbackConfig config)
        {
            this.config = config;
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
            worldEntity.Add(new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP });
            worldEntity.Add(new GameFramework.World.GroundState { IsGrounded = true });
            worldEntity.Add(new InputBuffer());
            // 서버와 같은 부품 — 탄 수 계산(예측)과 피해 %가 이 둘에 산다. 충전 시계는 처음 쏠 때 시작한다.
            worldEntity.Add(new KnockbackGun(config.AmmoMax) { Ammo = config.AmmoMax, NextReloadTick = long.MaxValue });
            worldEntity.Add(new KnockbackBody());

            // 서버와 같은 값이어야 예측이 안 어긋난다. 이 모양이 없으면 키네마틱 이동이 몸을 조용히 건너뛴다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            entityRegistry.Add(worldEntity);

            if (gameDataStore.userEntityId == creationData.entityId)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered knockback body {worldEntity.Id}");
        }
    }
}
