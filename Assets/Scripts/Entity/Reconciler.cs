using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 롤백 재조정(호스트 서비스). 서버 스냅이 도착하면 그 틱으로(위치·어빌리티 상태 모두) 하드
    /// 복원하고, 저장된 입력으로 발동→이동→어빌리티페이즈→상태이상→효과구동→물리를 현재 직전 틱까지
    /// 재생해 예측 오차를 보정한다.
    /// </summary>
    public class Reconciler
    {
        private const float Threshold = 0.06f;     // 이 이하 오차는 롤백 스킵(예측 정확)
        private const long MaxReplayTicks = 128;   // 격차가 이보다 크면 텔레포트 폴백(재생 생략)

        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private PredictedAbilityStateHistory predictedAbilityStateHistory;
        [Inject] private InputHistory inputHistory;
        [Inject] private MovementSystem movementSystem;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private StatusEffectSystem statusEffectSystem;
        [Inject] private AbilityEffectExecutor abilityEffectExecutor;
        [Inject] private AbilityDataProvider abilityDataProvider;
        [Inject] private GameFramework.IPhysicsSimulator physicsSimulator;
        [Inject] private ReconciliationStats reconciliationStats;

        private EntitySnap latestSnap;
        private bool hasPending;

        /// <summary>서버 스냅 수신(내 캐릭). 가장 최신 틱만 남긴다.</summary>
        public void AddServerSnap(EntitySnap snap)
        {
            if (!hasPending || snap.tick > latestSnap.tick)
            {
                latestSnap = snap;
                hasPending = true;
            }
        }

        /// <summary>틱 앞에서 호출. 대기 스냅이 있고 예측이 어긋났으면 복원+재생.</summary>
        public void Reconcile(long currentTick, float deltaTime, GameFramework.IEntityManager entityManager)
        {
            if (!hasPending)
            {
                return;
            }
            hasPending = false;

            EntitySnap snap = latestSnap;
            long anchorTick = snap.tick;

            LOPEntity entity = playerContext.entity;
            if (entity == null)
            {
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            if (worldEntity == null)
            {
                return;
            }

            // errorGate: 예측이 서버와 충분히 가까우면 아무것도 안 함.
            // 그 전에 예측-서버 거리를 항상 기록해 Recon HUD(ReconciliationStats)가 계속 갱신되게 한다.
            if (snapshotHistory.TryGet(anchorTick, out var predicted))
            {
                var authoritative = snap.position.ToNumerics();
                reconciliationStats.Record(System.Numerics.Vector3.Distance(predicted.Position, authoritative));
                // 게이트는 위치만 본다(의도적): 어빌리티/상태이상은 서버 권위 wire 값이 없고 입력으로
                // 결정론적 재생되므로 별도 게이트 불필요.
                if (!GameFramework.Netcode.ReconcileGate.ShouldReconcile(predicted.Position, authoritative, Threshold))
                {
                    return;
                }
            }

            // 하드 복원: 내 캐릭을 서버 스냅(anchorTick) 상태로. reactive 경로가 rigidbody에 반영되므로
            // PhysX가 새 포즈를 보도록 수동 SyncTransforms(autoSyncTransforms=false).
            entity.position = snap.position;
            entity.rotation = snap.rotation;
            entity.velocity = snap.velocity;
            entity.PushMotionToPhysics();
            Physics.SyncTransforms();

            // 넉백 등 외부 이동 기여는 서버 권위 → 스냅에서 복원한다. 내 예측 히스토리(PredictedAbilityState)엔
            // 없다: 서버가 가한 것이라 클라가 예측·생성하지 않기 때문. position/velocity와 같은 권위 축.
            var motionContributions = worldEntity.Get<MotionContributions>();
            if (motionContributions != null)
            {
                motionContributions.Items.Clear();
                motionContributions.Items.AddRange(snap.contributions);
            }

            // 어빌리티/상태이상/스탯/마나도 앵커 틱 상태로 복원 — 재생이 대시 등을 정확히 재현하려면 필요.
            // 두 히스토리가 어긋나면(정상 경로엔 없음 — 엔티티 일시 null 등 엣지) 재생을 생략한다.
            // 위치는 이미 서버 스냅으로 복원됐고, stale 어빌리티 기준으로 재생하지 않기 위함(두 링 대칭 복원).
            if (!predictedAbilityStateHistory.TryGet(anchorTick, out var abilityState))
            {
                return;
            }
            abilityState.RestoreTo(worldEntity);

            // 격차가 과도하면 재생 생략(텔레포트) — 입력/스냅 히스토리 밖이라 재생 불가.
            if (currentTick - anchorTick > MaxReplayTicks)
            {
                return;
            }

            // 재생: 이미 예측했던 과거 틱(anchor+1 ~ currentTick-1)을 이동+물리로 재구성.
            var buffer = worldEntity.Get<InputBuffer>();
            if (buffer == null)
            {
                return;
            }
            for (long t = anchorTick + 1; t < currentTick; t++)
            {
                var cmd = inputHistory.TryGet(t, out var recorded) ? recorded : null;
                buffer.Current = cmd;

                // 발동 재현: 입력에 어빌리티가 있으면 그 틱에 다시 발동한다. AbilityActivator가 아니라
                // AbilitySystem.TryActivate를 직접 부른다 — 연출 cue 이벤트(AbilityActivatedEvent)를 재생 때
                // 중복 송출하지 않기 위해(cue는 원래 라이브 틱에 이미 발화됨).
                if (cmd != null && cmd.AbilityId != 0 &&
                    abilityDataProvider.TryGet(cmd.AbilityId, out var data))
                {
                    // target=self(worldEntity) — 현재 자기시전 어빌리티 기준(AbilityActivator와 동일).
                    // 향후 타깃 예측 어빌리티 추가 시 AbilityActivator의 타깃 해석을 여기서도 맞출 것.
                    abilitySystem.TryActivate(worldEntity, data, worldEntity, t);
                }

                movementSystem.Tick(worldEntity, t, deltaTime);
                abilitySystem.Tick(worldEntity, t);
                statusEffectSystem.Tick(worldEntity, t);
                abilityEffectExecutor.DriveActiveEntity(worldEntity, entityManager, t);

                entity.PushMotionToPhysics();
                physicsSimulator.Simulate(deltaTime);
                entity.SyncPhysics();

                // 보정값으로 두 히스토리 갱신(다음 비교/재생이 stale값을 안 보도록).
                var transform = worldEntity.Get<GameFramework.World.Transform>();
                var velocity = worldEntity.Get<GameFramework.World.Velocity>();
                snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                    t, transform.Position, transform.Rotation, velocity.Linear));
                predictedAbilityStateHistory.Record(t, PredictedAbilityState.Capture(worldEntity));
            }
        }
    }
}
