using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 카드 러시의 몸(클라). 서버와 같은 부품이되 소유자·시뮬 표식은 없다 — 굴릴지(내 몸만)는 EntityBinder가
    /// 동기화 정책을 보고 Simulated를 붙인다. 출발 자리가 곧 레인 출발점이다(서버 룰이 레인 출발점에 스폰한다).
    /// </summary>
    public class CardRushRunnerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public CardRushRunnerCreator(IGameDataStore gameDataStore, IPlayerContext playerContext,
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
            worldEntity.Add(new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP });
            worldEntity.Add(new InputBuffer());

            //  결승 틱은 월드가 상태에서 다시 적고, 등수는 스냅이 적는다.
            worldEntity.Add(new FinishState());
            worldEntity.Add(new FinishPlacement());
            worldEntity.Add(new CardRushRunner { LaneOrigin = creationData.position });

            //  시뮬엔 물리가 없지만 클라는 모든 몸에 PhysX 대리 몸을 세운다(EntityBinder) — 모양과 종류가 있어야 한다.
            //  키네마틱 트리거라 아무것도 밀지 않는다. 대리 몸은 매 틱 월드 자리를 따라갈 뿐이다(PhysicsSimulationSystem).
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: true));

            entityRegistry.Add(worldEntity);

            if (gameDataStore.userEntityId == creationData.entityId)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered card rush runner {worldEntity.Id}");
        }
    }
}
