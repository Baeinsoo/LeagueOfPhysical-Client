using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>Archery의 플레이어 몸(클라). 걷지 않으므로 이동 부품이 없다.</summary>
    public class ArcheryPlayerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ArcheryCourse course;

        public ArcheryPlayerCreator(IGameDataStore gameDataStore, IPlayerContext playerContext,
                                    GameFramework.World.EntityRegistry entityRegistry, ArcheryCourse course)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.course = course;
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
            worldEntity.Add(new ArcheryAim());
            worldEntity.Add(new ArcheryScore());
            //  사거리 맵은 화살이 과녁 수만큼이다. 웨이브 맵은 0을 돌려주므로 그릇을 안 붙인다 —
            //  붙이는 순간 한 발도 못 쏘게 되므로 이 조건이 곧 "원형 맵은 안 바뀐다"의 보증이다.
            int arrows = course.ArrowsPerStand;
            if (arrows > 0)
            {
                worldEntity.Add(new ArcheryQuiver { Remaining = arrows });
            }

            // 남의 몸도 입력 버퍼를 갖는다 — 서버가 남의 입력을 되뿌려 주고(EntityInputBroadcastSystem)
            // RemoteInputSystem이 여기에 채운다. 이 게임은 남도 굴리므로(CharactersPredictedSyncPolicy)
            // 그 입력이 실제로 읽혀서 남의 화살도 내 화면에서 같은 규칙으로 날아간다.
            worldEntity.Add(new InputBuffer());

            // 걷지는 않지만 EntityBinder가 모든 엔티티에 물리 몸을 붙인다 — 모양과 종류가 없으면
            // PhysicsBodyFactory가 거기서 예외를 던진다. 다른 캐릭터와 같은 치수를 쓴다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(
                BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            entityRegistry.Add(worldEntity);

            if (gameDataStore.userEntityId == creationData.entityId)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered archer {worldEntity.Id}");
        }
    }
}
