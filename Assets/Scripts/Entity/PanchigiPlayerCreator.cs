using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 플레이어(클라). 아바타가 없어 돌아다니지 않지만, 누구 차례인지·누가 쳤는지를 잇는
    /// 신원이 필요해 엔티티는 만든다. 몸은 자리만 지키는 최소 크기다.
    /// </summary>
    public class PanchigiPlayerCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public PanchigiPlayerCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
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
            //  스냅샷 빌더가 모든 엔티티에서 속도를 읽는다(널 가드 없음) — 안 움직여도 필요하다.
            worldEntity.Add(new GameFramework.World.Velocity());
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new GameFramework.World.CapsuleShape(0.3f, 1.6f));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            //  클라 CharacterCreationData엔 userId가 없다(다른 클라 creator들과 동일 — "내 것"은
            //  gameDataStore.userEntityId 비교로 가린다). Ownership은 서버 전용 개념이라 여기선 안 붙인다.

            //  Simulated을 붙이지 않는다 — 우리 시뮬이 굴릴 것이 없다(아바타가 안 움직인다).
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered panchigi player (client) {worldEntity.Id}");
        }
    }
}
