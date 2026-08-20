using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 롤백 재조정(호스트 서비스). 서버 스냅이 도착하면 그 틱 상태로 하드 복원하고, 저장된 입력을
    /// 되먹이며 현재 직전 틱까지 <see cref="GameFramework.World.IWorld.Tick"/>를 재생해 예측 오차를 보정한다 —
    /// 재생이 곧 라이브와 같은 단일 진입점(수기 시퀀스 복제 없음). 무엇을 되돌릴지는 월드가 알고,
    /// 스냅 중 게임마다 다른 부분은 <see cref="IServerCorrectionHandler"/>가 맡는다.
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
        private readonly GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory;
        private readonly GameFramework.World.IWorld world;   // 재생 = 라이브와 같은 단일 진입점 world.Tick
        private readonly GameFramework.World.IMotionBridge motionBridge;
        private readonly ReconciliationStats reconciliationStats;
        private readonly InputTimingStats inputTimingStats;   // [진단용 임시] 스파이크 순간의 입력 도착 상태
        private readonly GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother;
        private readonly IServerCorrectionHandler correction;

        private EntitySnap latestSnap;
        private bool hasPending;

        public Reconciler(
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory,
            GameFramework.World.IWorld world,
            GameFramework.World.IMotionBridge motionBridge,
            ReconciliationStats reconciliationStats,
            InputTimingStats inputTimingStats,
            GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother,
            IServerCorrectionHandler correction)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.worldEventBuffer = worldEventBuffer;
            this.inputHistory = inputHistory;
            this.world = world;
            this.motionBridge = motionBridge;
            this.reconciliationStats = reconciliationStats;
            this.inputTimingStats = inputTimingStats;
            this.renderCorrectionSmoother = renderCorrectionSmoother;
            this.correction = correction;
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

            // 하드 보정으로 시뮬 위치가 튄 것을 렌더 스무더에 알린다. 스무더가 보이는 위치를
            // (보정 전 예측 → 보정 후 권위)만큼 부드럽게 흡수한다(시뮬 무영향). 크기별 스냅/무시는 스무더가 판단.
            // 권위 값을 덮은 뒤 빠져나가는 길은 재생을 하든 말든 전부 이걸 부른다 — 안 부르면 화면이 순간이동한다.
            void NotifyRenderCorrection()
            {
                renderCorrectionSmoother.OnCorrection(
                    preCorrectionPos.ToNumerics(),
                    GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity).ToNumerics());
            }

            // errorGate: 예측이 서버와 충분히 가까우면 아무것도 안 함.
            // 그 전에 예측-서버 거리를 항상 기록해 Recon HUD(ReconciliationStats)가 계속 갱신되게 한다.
            if (world.TryGetSavedMotion(anchorTick, entityId, out var predicted))
            {
                var authoritative = snap.position.ToNumerics();
                float error = System.Numerics.Vector3.Distance(predicted.Position, authoritative);
                reconciliationStats.Record(error);

                // [진단용 임시] 예측이 크게 어긋난 순간의 정황을 통째로 남긴다.
                // 얼마나 어긋났는지(통계)만으로는 원인을 못 가른다 — 그 틱의 입력·속도·접지가 필요하다.
                if (error > SpikeLogThreshold)
                {
                    // 무엇이 실렸는지는 커맨드가 스스로 찍는다 — 여기서 필드를 나열하면 넷코드가 게임 내용을 알게 된다.
                    string inputText = inputHistory.TryGet(anchorTick, out var spikeInput) && spikeInput != null
                        ? spikeInput.ToString()
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

                // 위치가 가까워도 게임 고유 상태가 서버와 다르면 게이트를 연다(무엇을 보는지는 게임이 안다).
                bool statusMatches = correction.Matches(anchorTick, snap);
                if (positionClose && statusMatches)
                {
                    return;
                }
            }

            // 게이트를 통과했다 = 실제로 되돌린다. 여기서 세야 "스킵"과 "보정"이 정확히 갈린다.
            reconciliationStats.RecordCorrection();

            // 예측 상태로 되돌린다(위치·속도·게임 상태 전부). 기록이 없는 두 경우를 가른다:
            //  · 앵커가 내 첫 기록보다 과거 = 내가 아직 매치에 없던 틱. 되돌릴 게 없을 뿐 재생은 정상으로 한다.
            //  · 그 외 = 살았던 틱인데 밀려남. 그때 상태를 알 수 없어 재생은 생략한다(권위 위치는 아래서 적용).
            bool restored = world.LoadState(anchorTick);
            bool tooOld = !restored
                && (world.FirstSavedTick is not long first || anchorTick >= first);

            // 권위 값을 그 위에 덮는다 — 서버가 진실인 축(위치·회전·속도·외력·게임 고유분).
            GameFramework.World.EntityMotionExtensions.SetPosition(worldEntity, snap.position);
            GameFramework.World.EntityMotionExtensions.SetRotation(worldEntity, snap.rotation);
            GameFramework.World.EntityMotionExtensions.SetVelocity(worldEntity, snap.velocity);

            var motionContributions = worldEntity.Get<MotionContributions>();
            if (motionContributions != null)
            {
                motionContributions.Items.Clear();
                motionContributions.Items.AddRange(snap.contributions);
            }

            correction.ApplyAuthoritative(worldEntity, snap);

            // World에 쓴 포즈를 MotionBridge가 rb에 밀고, PhysX가 새 포즈를 보도록 수동
            // SyncTransforms(autoSyncTransforms=false).
            motionBridge.PushMotion(worldEntity);
            Physics.SyncTransforms();

            if (tooOld || currentTick - anchorTick > MaxReplayTicks)
            {
                NotifyRenderCorrection();
                return;
            }

            // 재생: 이미 예측했던 과거 틱(anchor+1 ~ currentTick-1)을 이동+물리로 재구성.
            var inputBuffer = worldEntity.Get<InputBuffer>();   // 입력 버퍼 (WorldEventBuffer 아님 — 이름 구분)
            if (inputBuffer == null)
            {
                NotifyRenderCorrection();
                return;
            }
            // 재생이 만든 연출 이벤트(cue 등)는 이미 라이브 때 방출됐으므로 버린다.
            using (worldEventBuffer.Suppress())
            {
                for (long t = anchorTick + 1; t < currentTick; t++)
                {
                    inputBuffer.Current = inputHistory.TryGet(t, out var recorded) ? recorded : null;

                    // 재생 = 라이브와 같은 단일 진입점. 입력에 실린 발동도 월드가 알아서 한다.
                    world.Tick(t, deltaTime);

                    // 보정값으로 다시 보관한다(다음 비교·재생이 낡은 값을 안 보도록).
                    world.SaveState(t);
                }
            }

            NotifyRenderCorrection();
        }
    }
}
