using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 롤백 재조정(호스트 서비스). 서버 스냅이 도착하면 그 틱으로(위치·어빌리티 상태 모두) 하드
    /// 복원하고, 저장된 입력을 되먹이며 현재 직전 틱까지 <see cref="GameFramework.World.IWorld.Tick"/>를
    /// 재생해 예측 오차를 보정한다 — 재생이 곧 라이브와 같은 단일 진입점(수기 시퀀스 복제 없음).
    /// </summary>
    public class Reconciler
    {
        // 이 이하 오차는 롤백 스킵. 문턱은 "스냅이냐 점진이냐"가 아니라 "고치느냐 마느냐"를 가르므로
        // 그 아래는 영원히 안 고쳐진다 — 잡음을 거를 만큼만 작게 잡는다.
        // 6cm였을 때, 입력 한 틱 누락이 만든 4cm 오차가 정지 중에도 45틱 넘게 그대로 남는 것이 관측됐다.
        // 클·서가 같은 코드를 돌아 정상 구간 오차는 정확히 0이므로 거를 잡음 자체가 거의 없다.
        private const float Threshold = 0.01f;
        private const float SpikeLogThreshold = 0.02f;   // [진단용 임시] 이 이상 어긋나면 정황을 로그로 남긴다
        private const long MaxReplayTicks = 128;   // 격차가 이보다 크면 텔레포트 폴백(재생 생략)
        // 렌더 보정 임계(minCorrection/teleport)는 RenderCorrectionSmoother가 소유 — 여기선 seed만 한다.

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly AbilityActivator abilityActivator;
        private readonly GameFramework.Netcode.SnapshotHistory snapshotHistory;
        private readonly GameFramework.Netcode.SequenceBuffer<LOPSavedState> predictedAbilityStateHistory;
        private readonly GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory;
        private readonly GameFramework.World.IWorld world;   // 재생 = 라이브와 같은 단일 진입점 world.Tick
        private readonly GameFramework.World.IMotionBridge motionBridge;
        private readonly ReconciliationStats reconciliationStats;
        private readonly InputTimingStats inputTimingStats;   // [진단용 임시] 스파이크 순간의 입력 도착 상태
        private readonly GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother;
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly StatusEffectDataProvider statusEffectDataProvider;

        private EntitySnap latestSnap;
        private bool hasPending;

        public Reconciler(
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            AbilityActivator abilityActivator,
            GameFramework.Netcode.SnapshotHistory snapshotHistory,
            GameFramework.Netcode.SequenceBuffer<LOPSavedState> predictedAbilityStateHistory,
            GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory,
            GameFramework.World.IWorld world,
            GameFramework.World.IMotionBridge motionBridge,
            ReconciliationStats reconciliationStats,
            InputTimingStats inputTimingStats,
            GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother,
            StatusEffectSystem statusEffectSystem,
            StatusEffectDataProvider statusEffectDataProvider)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.worldEventBuffer = worldEventBuffer;
            this.abilityActivator = abilityActivator;
            this.snapshotHistory = snapshotHistory;
            this.predictedAbilityStateHistory = predictedAbilityStateHistory;
            this.inputHistory = inputHistory;
            this.world = world;
            this.motionBridge = motionBridge;
            this.reconciliationStats = reconciliationStats;
            this.inputTimingStats = inputTimingStats;
            this.renderCorrectionSmoother = renderCorrectionSmoother;
            this.statusEffectSystem = statusEffectSystem;
            this.statusEffectDataProvider = statusEffectDataProvider;
        }

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

            string entityId = playerContext.entityId;
            if (entityId == null)
            {
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entityId);
            if (worldEntity == null)
            {
                return;
            }

            // 예측된 현재 위치 — 하드 보정 전. 재생 후와의 차이로 보정 크기를 판정(시각 신호용).
            Vector3 preCorrectionPos = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);

            // errorGate: 예측이 서버와 충분히 가까우면 아무것도 안 함.
            // 그 전에 예측-서버 거리를 항상 기록해 Recon HUD(ReconciliationStats)가 계속 갱신되게 한다.
            if (snapshotHistory.TryGet(anchorTick, out var predicted))
            {
                var authoritative = snap.position.ToNumerics();
                float error = System.Numerics.Vector3.Distance(predicted.Position, authoritative);
                reconciliationStats.Record(error);

                // [진단용 임시] 예측이 크게 어긋난 순간의 정황을 통째로 남긴다.
                // 얼마나 어긋났는지(통계)만으로는 원인을 못 가른다 — 그 틱의 입력·속도·접지가 필요하다.
                if (error > SpikeLogThreshold)
                {
                    string inputText = inputHistory.TryGet(anchorTick, out var spikeInput) && spikeInput != null
                        ? $"h={spikeInput.Horizontal:F2} v={spikeInput.Vertical:F2} jump={spikeInput.Jump} ability={spikeInput.AbilityId}"
                        : "(없음)";
                    var delta = authoritative - predicted.Position;
                    Debug.LogWarning(
                        $"[ReconSpike] tick={anchorTick} cur={currentTick} err={error:F3}" +
                        $" delta=({delta.X:F3},{delta.Y:F3},{delta.Z:F3})" +
                        $" predPos=({predicted.Position.X:F2},{predicted.Position.Y:F2},{predicted.Position.Z:F2})" +
                        $" srvPos=({authoritative.X:F2},{authoritative.Y:F2},{authoritative.Z:F2})" +
                        $" predVel=({predicted.Velocity.X:F2},{predicted.Velocity.Y:F2},{predicted.Velocity.Z:F2})" +
                        $" srvVel=({snap.velocity.x:F2},{snap.velocity.y:F2},{snap.velocity.z:F2})" +
                        $" srvGrounded={snap.grounded} input[{inputText}]" +
                        // 입력이 서버에 늦게 닿았는지 — d가 음수면 미리 도착(정상), 0 이상이면 아슬아슬하거나 지각.
                        // 서버 피드백이 아직 한 번도 안 왔을 때 0을 실제 값으로 오해하지 않도록 구분해 찍는다.
                        (inputTimingStats.HasData
                            ? $" timing[dAvg={inputTimingStats.AvgD:F1} dMax={inputTimingStats.MaxD}" +
                              $" prune={inputTimingStats.PruneCount} seqGap={inputTimingStats.SeqGapCount}]"
                            : " timing[아직 서버 피드백 없음]") +
                        // 스냅이 얼마나 뒤처져 왔는지. 이 값이 계속 커지면 서버가 밀리는 중이라 비교 자체가 무의미하다.
                        $" snapAge={currentTick - anchorTick}");
                }
                bool positionClose = !GameFramework.Netcode.ReconcileGate.ShouldReconcile(predicted.Position, authoritative, Threshold);

                // 위치가 가까워도 서버 상태이상 목록이 다르면 게이트를 연다: 남이 나에게 건 효과(슬로우 등)는
                // 내가 예측할 수 없어서, 가만히 서 있다 슬로우가 걸려도 위치 오차는 0으로 남기 때문이다.
                // 비교는 반드시 같은 시점끼리 해야 한다 — 앵커 틱에 "내가 그때 예측했던" 목록 vs 서버가 앵커
                // 틱에 갖고 있던 목록. (지금 살아있는 목록과 비교하면 클라가 서버보다 앞서 달리는 리드 구간
                // 내내 시점이 어긋나 보여, 효과가 걸리거나 끝날 때마다 매 스냅에서 불필요한 롤백이 발생한다.)
                // id 집합뿐 아니라 만료틱도 봐야 한다 — 몬스터가 쿨다운 없이 계속 때리면 서버가 슬로우를
                // 계속 재적용해 만료틱만 밀리는데, id 집합은 그대로라 id만 비교하면 이 발산을 놓친다.
                bool statusMatches = true;
                if (predictedAbilityStateHistory.TryGet(anchorTick, out var predictedAtAnchor))
                {
                    statusMatches = !StatusEffectReconcileGate.ShouldReconcile(predictedAtAnchor.StatusEffects, snap.statusEffects, statusEffectDataProvider.Get);
                }
                // 앵커 틱 예측 기록이 없으면(정상 경로엔 없는 엣지) 비교 불가 — 불일치로 단정하지 않고
                // 위치 판정에만 맡긴다(statusMatches=true 기본값).
                if (positionClose && statusMatches)
                {
                    return;
                }
            }

            // 게이트를 통과했다 = 실제로 되돌린다. 여기서 세야 "스킵"과 "보정"이 정확히 갈린다.
            reconciliationStats.RecordCorrection();

            // 하드 복원: 내 캐릭을 서버 스냅(anchorTick) 상태로. World에 쓴 포즈를 MotionBridge가 rb에 밀고,
            // PhysX가 새 포즈를 보도록 수동 SyncTransforms(autoSyncTransforms=false).
            GameFramework.World.EntityMotionExtensions.SetPosition(worldEntity, snap.position);
            GameFramework.World.EntityMotionExtensions.SetRotation(worldEntity, snap.rotation);
            GameFramework.World.EntityMotionExtensions.SetVelocity(worldEntity, snap.velocity);
            motionBridge.PushMotion(worldEntity);
            Physics.SyncTransforms();

            // 넉백 등 외부 이동 기여는 서버 권위 → 스냅에서 복원한다. 내 예측 히스토리(LOPSavedState)엔
            // 없다: 서버가 가한 것이라 클라가 예측·생성하지 않기 때문. position/velocity와 같은 권위 축.
            var motionContributions = worldEntity.Get<MotionContributions>();
            if (motionContributions != null)
            {
                motionContributions.Items.Clear();
                motionContributions.Items.AddRange(snap.contributions);
            }

            // 어빌리티/상태이상/스탯/마나도 앵커 틱 상태로 복원 — 재생이 대시 등을 정확히 재현하려면
            // 필요하다. 지금 상태로 재생하면 그때 없던 대시가 켜진 채 굴러 위치가 틀어진다.
            //
            // 기록이 없을 때가 문제인데, 이유가 둘이고 대응이 다르다:
            //  · 앵커가 내 첫 기록보다 과거 = 내가 아직 매치에 없던 틱. 그 뒤로 내가 굴린 틱이 없으니
            //    지금 상태가 곧 그때 상태다 — 복원할 게 없을 뿐, 재생은 정상으로 해야 한다.
            //    (시드 직후엔 스냅이 snapAge만큼 과거를 가리키며 계속 오므로 매 매치 초반이 이 경우다.)
            //  · 그 외 = 살았던 틱인데 링 밖으로 밀려남. 그때 상태를 알 수 없어 재생하면 위험하므로
            //    생략한다. 다만 이 경우 위치는 서버 스냅에 남으니, 재생 생략은 최후 수단이다.
            if (predictedAbilityStateHistory.TryGet(anchorTick, out var abilityState))
            {
                abilityState.RestoreTo(worldEntity);
            }
            else if (snapshotHistory.FirstRecordedTick is not long first || anchorTick >= first)
            {
                return;
            }

            // 남이 나에게 건 효과(슬로우 등)는 내가 예측할 수 없다 → 서버 목록이 진실.
            // 위 RestoreTo가 되돌린 예측값 위에 덮는다(넉백 기여를 스냅에서 복원하는 것과 같은 축).
            // 앵커에서 맞춰두면 이어지는 재생이 현재 틱까지 밀어 올린다.
            statusEffectSystem.ApplyAuthoritativeState(worldEntity, snap.statusEffects, statusEffectDataProvider.Get);

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
                    predictedAbilityStateHistory.Record(t, LOPSavedState.Capture(worldEntity));
                }
            }

            // 하드 보정으로 시뮬 위치가 튄 것을 렌더 스무더에 알린다. 스무더가 보이는 위치를
            // (보정 전 예측 → 보정 후 권위)만큼 부드럽게 흡수한다(시뮬 무영향). 크기별 스냅/무시는 스무더가 판단.
            renderCorrectionSmoother.OnCorrection(preCorrectionPos.ToNumerics(), GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity).ToNumerics());
        }
    }
}
