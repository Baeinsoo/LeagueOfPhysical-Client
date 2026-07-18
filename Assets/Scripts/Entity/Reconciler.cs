using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 롤백 재조정(호스트 서비스). 서버 스냅이 도착하면 그 틱으로(위치·어빌리티 상태 모두) 하드
    /// 복원하고, 저장된 입력을 되먹이며 현재 직전 틱까지 <see cref="GameFramework.World.IWorld.Tick"/>를
    /// 재생해 예측 오차를 보정한다 — 재생이 곧 라이브와 같은 단일 진입점(수기 시퀀스 복제 없음).
    /// </summary>
    public class Reconciler
    {
        private const float Threshold = 0.06f;     // 이 이하 오차는 롤백 스킵(예측 정확)
        private const long MaxReplayTicks = 128;   // 격차가 이보다 크면 텔레포트 폴백(재생 생략)
        // 렌더 보정 임계(minCorrection/teleport)는 RenderCorrectionSmoother가 소유 — 여기선 seed만 한다.

        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private AbilityActivator abilityActivator;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private GameFramework.Netcode.SequenceBuffer<PredictedAbilityState> predictedAbilityStateHistory;
        [Inject] private GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory;
        [Inject] private GameFramework.World.IWorld world;   // 재생 = 라이브와 같은 단일 진입점 world.Tick
        [Inject] private GameFramework.World.IMotionBridge motionBridge;
        [Inject] private ReconciliationStats reconciliationStats;
        [Inject] private GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother;

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
        public void Reconcile(long currentTick, float deltaTime)
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

            // 예측된 현재 위치 — 하드 보정 전. 재생 후와의 차이로 보정 크기를 판정(시각 신호용).
            Vector3 preCorrectionPos = entity.position;

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

            // 하드 복원: 내 캐릭을 서버 스냅(anchorTick) 상태로. World에 쓴 포즈를 MotionBridge가 rb에 밀고,
            // PhysX가 새 포즈를 보도록 수동 SyncTransforms(autoSyncTransforms=false).
            entity.position = snap.position;
            entity.rotation = snap.rotation;
            entity.velocity = snap.velocity;
            motionBridge.PushMotion(worldEntity);
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
            var inputBuffer = worldEntity.Get<InputBuffer>();   // 입력 버퍼 (WorldEventBuffer 아님 — 이름 구분)
            if (inputBuffer == null)
            {
                return;
            }
            // 재생이 만든 연출 이벤트(cue 등)는 이미 라이브 때 방출됐으므로 버린다.
            using (worldEventBuffer.Suppress())
            {
                for (long t = anchorTick + 1; t < currentTick; t++)
                {
                    var cmd = inputHistory.TryGet(t, out var recorded) ? recorded : null;
                    inputBuffer.Current = cmd;

                    // 발동 재현: 라이브와 같은 정식 통로(ProcessInput 위치). cue Append는 위 억제 스코프가 버린다.
                    if (cmd != null && cmd.AbilityId != 0)
                    {
                        abilityActivator.TryActivate(worldEntity.Id, cmd.AbilityId, t);
                    }

                    // 재생 = 라이브와 동일한 단일 진입점. 클라 Simulated=내 캐릭만이라 world.Tick이 내 캐릭만 재생.
                    // (이동→어빌리티→상태→효과구동→키네마틱 5페이즈. 수기 시퀀스 복제 제거 = #6 종결.)
                    world.Tick(t, deltaTime);

                    // 보정값으로 두 히스토리 갱신(다음 비교/재생이 stale값을 안 보도록).
                    var transform = worldEntity.Get<GameFramework.World.Transform>();
                    var velocity = worldEntity.Get<GameFramework.World.Velocity>();
                    snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                        t, transform.Position, transform.Rotation, velocity.Linear));
                    predictedAbilityStateHistory.Record(t, PredictedAbilityState.Capture(worldEntity));
                }
            }

            // 하드 보정으로 시뮬 위치가 튄 것을 렌더 스무더에 알린다. 스무더가 보이는 위치를
            // (보정 전 예측 → 보정 후 권위)만큼 부드럽게 흡수한다(시뮬 무영향). 크기별 스냅/무시는 스무더가 판단.
            renderCorrectionSmoother.OnCorrection(preCorrectionPos.ToNumerics(), entity.position.ToNumerics());
        }
    }
}
