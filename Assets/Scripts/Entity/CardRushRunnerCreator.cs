using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 카드 러시의 몸(클라). 서버와 같은 부품이되 소유자·시뮬 표식은 없다 — 굴릴지(내 몸만)는 EntityBinder가
    /// 동기화 정책을 보고 Simulated를 붙인다. 레인 출발점은 받은 자리를 출발선으로 내려 규칙에서 다시 구한다 —
    /// 받은 자리는 "지금" 자리라 판 도중 재접속하면 칸 20쯤에 있다(최종 리뷰 I1).
    /// </summary>
    public class CardRushRunnerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CardRushMapRulesField rules;

        public CardRushRunnerCreator(IGameDataStore gameDataStore, IPlayerContext playerContext,
                                     GameFramework.World.EntityRegistry entityRegistry, CardRushMapRulesField rules)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.rules = rules;
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
            worldEntity.Add(new CardRushRunner { LaneOrigin = LaneOriginFor(rules, creationData.position) });
            worldEntity.Add(new CardRushServerStreak());

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

        /// <summary>
        /// 자리 → 그 레인의 출발점. 옆 방향 거리로 레인 번호를 찾아 서버(<c>CardRushRuleSystem</c>)가 스폰한 것과 같은
        /// <see cref="CardRushMapRulesField.LaneOrigin"/>을 돌려준다 — 앞뒤(몇 칸 갔나)·높이는 버린다.
        /// 규칙이 아직 없으면(맵이 몸보다 늦은 비정상 순서) 받은 자리를 그대로 쓴다.
        /// </summary>
        public static Vector3 LaneOriginFor(CardRushMapRulesField rules, Vector3 position)
        {
            if (rules == null || rules.HasRules == false || rules.LaneSpacing <= 0f)
            {
                Debug.LogWarning("[CardRushRunnerCreator] 맵 규칙 없이 몸이 생겼다 — 받은 자리를 레인 출발점으로 쓴다");
                return position;
            }
            var right = Vector3.Cross(Vector3.up, CardRushLanes.Flat(rules.Forward));
            float offset = Vector3.Dot(position - rules.Origin, right) / rules.LaneSpacing;
            int lane = Mathf.Clamp(Mathf.RoundToInt(offset + (CardRushLanes.MaxLanes - 1) * 0.5f), 0, CardRushLanes.MaxLanes - 1);
            return rules.LaneOrigin(lane);
        }
    }
}
