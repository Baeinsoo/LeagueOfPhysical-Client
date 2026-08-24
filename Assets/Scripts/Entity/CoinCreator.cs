using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 동전(클라). 서버가 다이나믹 몸으로 굴리는 결과를 그대로 받아 보여줄 뿐이라
    /// 이쪽은 kinematic이다(우리가 굴리지 않는다) — 위치는 보간기가 채운다.
    /// Simulated을 붙이지 않는다 — 우리 시뮬이 굴리는 것이 아니다.
    /// </summary>
    public class CoinCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public CoinCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(CoinCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Coin));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new GameFramework.World.DiscShape(0.15f, 0.04f));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: false, isTrigger: false));

            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered coin {worldEntity.Id}");
        }
    }
}
