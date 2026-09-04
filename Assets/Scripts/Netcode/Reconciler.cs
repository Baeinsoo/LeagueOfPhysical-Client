using System.Collections.Generic;
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 서버 스냅 한 틱 배치 전체(예측 대상 엔티티들)를 롤백 재조정하는 호스트 서비스. 서버 스냅이 도착하면
    /// 그 틱 상태로 하드 복원하고, 저장된 입력을 되먹이며 현재 직전 틱까지
    /// <see cref="GameFramework.World.IWorld.Tick"/>를 재생해 예측 오차를 보정한다 —
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
        // 렌더 보정 임계(minCorrection/noSmoothDistance)는 RenderCorrectionSmoother가 소유 — 여기선 seed만 한다.

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory;
        private readonly GameFramework.World.IWorld world;   // 재생 = 라이브와 같은 단일 진입점 world.Tick
        private readonly GameFramework.World.IMotionBridge motionBridge;
        private readonly ReconciliationStats reconciliationStats;
        private readonly InputTimingStats inputTimingStats;   // [진단용 임시] 스파이크 순간의 입력 도착 상태
        private readonly ActorRegistry actorRegistry;
        private readonly IServerCorrectionHandler correction;
        private readonly RemoteInputSystem remoteInput;

        // 가장 새 틱의 스냅 배치(엔티티 id → 스냅). 서버가 한 틱 분을 한 메시지로 보내므로
        // 틱이 올라가면 앞 배치는 이미 처리됐거나 낡은 것이다.
        // 전제: 서버가 한 틱의 스냅을 EntitySnapsToC 한 메시지로 함께 보낸다. 엔티티마다 다른 틱으로
        // 온다면 이 전제가 깨져 오래된 스냅이 버려지므로, 그때는 틱별 큐로 바꿔야 한다.
        private readonly Dictionary<string, EntitySnap> pendingSnaps = new Dictionary<string, EntitySnap>();
        private long pendingTick = long.MinValue;

        // 이미 처리(Reconcile 완료)한 가장 최신 틱. unreliable 중복 등으로 같은 틱 스냅이 한 번 더 오면
        // 그때 다시 쌓인 배치는 원래 있어야 할 다른 엔티티 없이 반쪽짜리라, 그걸로 또 롤백하면 그 엔티티들이
        // 보정 전 값으로 되돌아간다 — 그래서 이미 처리한 틱은 애초에 pendingSnaps에 넣지 않는다.
        private long lastReconciledTick = long.MinValue;

        // Reconcile마다 새로 만들지 않고 재사용(매 틱 도는 경로의 할당을 줄인다 — 실측상 약 44%의 틱에서 열린다).
        private readonly Dictionary<string, System.Numerics.Vector3> preCorrectionPositions = new Dictionary<string, System.Numerics.Vector3>();

        // 카운터가 바뀐 엔티티를 추적해 텔레포트를 판별한다(거리 기반 errorGate로는 짧은 텔레포트를 못 잡음).
        private readonly GameFramework.Netcode.TeleportTracker teleportTracker
            = new GameFramework.Netcode.TeleportTracker();
        private readonly HashSet<string> teleportedThisBatch = new HashSet<string>();

        public Reconciler(
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory,
            GameFramework.World.IWorld world,
            GameFramework.World.IMotionBridge motionBridge,
            ReconciliationStats reconciliationStats,
            InputTimingStats inputTimingStats,
            ActorRegistry actorRegistry,
            IServerCorrectionHandler correction,
            RemoteInputSystem remoteInput)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.worldEventBuffer = worldEventBuffer;
            this.inputHistory = inputHistory;
            this.world = world;
            this.motionBridge = motionBridge;
            this.reconciliationStats = reconciliationStats;
            this.inputTimingStats = inputTimingStats;
            this.actorRegistry = actorRegistry;
            this.correction = correction;
            this.remoteInput = remoteInput;
        }

        /// <summary>서버 스냅 수신(예측 대상 전부). 가장 새 틱의 배치만 남긴다.</summary>
        public void AddServerSnap(EntitySnap snap)
        {
            if (snap.tick <= lastReconciledTick)
            {
                return;
            }
            if (snap.tick < pendingTick)
            {
                return;
            }
            if (snap.tick > pendingTick)
            {
                pendingSnaps.Clear();
                pendingTick = snap.tick;
            }
            pendingSnaps[snap.entityId] = snap;
        }

        /// <summary>틱 앞에서 호출. 대기 스냅 배치가 있고 예측이 어긋났으면 복원+재생.</summary>
        public void Reconcile(long currentTick, float deltaTime)
        {
            if (pendingSnaps.Count == 0)
            {
                return;
            }
            long anchorTick = pendingTick;

            string entityId = playerContext.entityId;
            if (entityId == null)
            {
                pendingSnaps.Clear();
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entityId);
            if (worldEntity == null)
            {
                pendingSnaps.Clear();
                return;
            }

            // 보정 전 예측 위치를 엔티티마다 기억해 둔다(렌더 보정 통지가 "전→후" 차이를 알아야 한다).
            preCorrectionPositions.Clear();

            // 하드 보정으로 시뮬 위치가 튄 것을 렌더 스무더에 알린다. 스무더가 보이는 위치를
            // (보정 전 예측 → 보정 후 권위)만큼 부드럽게 흡수한다(시뮬 무영향). 크기별 스냅/무시는 스무더가 판단.
            // 권위 값을 덮은 뒤 빠져나가는 길은 재생을 하든 말든 전부 이걸 부른다 — 안 부르면 화면이 순간이동한다.
            void NotifyRenderCorrections()
            {
                foreach (var pair in preCorrectionPositions)
                {
                    if (actorRegistry.TryGet(pair.Key, out var actor) == false)
                    {
                        continue;
                    }
                    var target = entityRegistry.Get(pair.Key);
                    if (target == null)
                    {
                        continue;
                    }
                    var after = GameFramework.World.EntityMotionExtensions.GetPosition(target).ToNumerics();
                    if (pair.Key != entityId)
                    {
                        // [진단용 임시] 되감기 재생이 "지금 화면 자리"를 얼마나 옮겼나 = 눈에 보이는 튐.
                        // 앵커 틱의 오차와는 다른 값이다 — 거긴 입력이 다 도착해 늘 0에 가깝다.
                        RemoteSyncProbe.Corrected(pair.Key, System.Numerics.Vector3.Distance(pair.Value, after));
                    }
                    actor.GetComponent<PredictedEntityInterpolator>()?.OnCorrection(
                        pair.Value, after,
                        GameFramework.World.EntityMotionExtensions.GetVelocity(target).ToNumerics(),
                        deltaTime,
                        teleportedThisBatch.Contains(pair.Key));
                }
            }

            // 텔레포트 관측은 아래 errorGate보다 먼저다. 게이트는 "예측이 서버와 얼마나 먼가"로
            // 판단하는데, 짧은 텔레포트는 그 문턱 아래라 게이트가 닫히고 스냅을 적용하는 루프가
            // 통째로 안 돈다 — 그러면 신호가 사라진다.
            teleportedThisBatch.Clear();
            foreach (var pair in pendingSnaps)
            {
                if (teleportTracker.Observe(pair.Key, pair.Value.teleportCount))
                {
                    teleportedThisBatch.Add(pair.Key);
                }
            }

            // errorGate: 배치의 모든 엔티티가 서버와 충분히 가까우면 아무것도 안 함(아무도 어긋나지 않은 틱은
            // 여전히 건너뛴다). 그 전에 내 엔티티의 예측-서버 거리를 항상 기록해 Recon HUD가 계속 갱신되게 한다.
            bool allClose = true;
            foreach (var pair in pendingSnaps)
            {
                if (!world.TryGetSavedMotion(anchorTick, pair.Key, out var predicted))
                {
                    allClose = false;   // 기록이 없으면 비교할 수 없다 — 되돌린다
                    continue;
                }
                var authoritative = pair.Value.position.ToNumerics();
                float error = System.Numerics.Vector3.Distance(predicted.Position, authoritative);

                if (pair.Key != entityId)
                {
                    RemoteSyncProbe.RemoteError(error);   // [진단용 임시]
                }

                if (pair.Key == entityId)
                {
                    EntitySnap snap = pair.Value;
                    // HUD가 읽는 값은 내 엔티티 하나뿐이지만, 아래 RecordCorrection()은 배치 전체 기준(엔티티
                    // 하나라도 어긋나면 카운트)이라 같은 HUD 줄의 "평균 오차"와 "보정 횟수"가 서로 다른 대상을 센다.
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
                }

                if (GameFramework.Netcode.ReconcileGate.ShouldReconcile(predicted.Position, authoritative, Threshold))
                {
                    allClose = false;
                }
            }

            // 위치가 다 가까워도 게임 고유 상태가 다르면 되돌린다(무엇을 보는지는 게임이 안다).
            // 내 엔티티 스냅이 배치에 없으면 비교할 수 없으니 안전한 쪽(되돌림)으로 간다.
            //
            // 여기서 보는 건 내 엔티티 하나뿐이다 — 남의 게임 고유 상태를 잘못 예측해도 이 문은
            // 그것만으로는 열리지 않는다. 그런데도 남이 제때 고쳐지는 이유는 Flappy에서 그 상태
            // (스턴)가 곧 몸의 움직임이기 때문이다: 서버는 멈춘 새의 속도를 0으로 고정하는데 내
            // 예측은 그 새를 전속 전진시키므로, 위치 차이가 한 틱 만에 위 문턱(1cm)을 넘어 위치
            // 게이트가 대신 열린다.
            // 그러니 이건 이 게임에서만 성립하는 우연한 안전망이다. 몸을 안 움직이는 게임 고유
            // 상태(예: 자원·쿨다운만 바뀌는 것)를 가진 게임이 오면, 그 게임의 남의 새는 조용히
            // 안 고쳐진다 — 그때는 배치 전체를 correction.Matches에 물어야 한다.
            bool statusMatches = pendingSnaps.TryGetValue(entityId, out var mySnap) && correction.Matches(anchorTick, mySnap);
            //  텔레포트는 정의상 이어지지 않는 이동이라, 거리가 작아도 그대로 채택해야 한다.
            if (allClose && statusMatches && teleportedThisBatch.Count == 0)
            {
                lastReconciledTick = anchorTick;
                pendingSnaps.Clear();
                return;
            }

            // 게이트를 통과했다 = 실제로 되돌린다. 여기서 세야 "스킵"과 "보정"이 정확히 갈린다.
            reconciliationStats.RecordCorrection();
            // 이 배치(anchorTick)는 여기서부터 끝까지 확정 처리된다(아래에서 무조건 권위 값을 덮음) —
            // 이후 같은 틱 스냅이 중복 도착해도 AddServerSnap이 걸러낸다.
            lastReconciledTick = anchorTick;

            foreach (var pair in pendingSnaps)
            {
                var target = entityRegistry.Get(pair.Key);
                if (target == null)
                {
                    continue;
                }
                preCorrectionPositions[pair.Key] =
                    GameFramework.World.EntityMotionExtensions.GetPosition(target).ToNumerics();
            }

            // 예측 상태로 되돌린다(위치·속도·게임 상태 전부). 기록이 없는 두 경우를 가른다:
            //  · 앵커가 내 첫 기록보다 과거 = 내가 아직 매치에 없던 틱. 되돌릴 게 없을 뿐 재생은 정상으로 한다.
            //  · 그 외 = 살았던 틱인데 밀려남. 그때 상태를 알 수 없어 재생은 생략한다(권위 위치는 아래서 적용).
            bool restored = world.LoadState(anchorTick);
            bool tooOld = !restored
                && (world.FirstSavedTick is not long first || anchorTick >= first);

            // 권위 값을 배치의 각 엔티티에 덮는다 — 서버가 진실인 축(위치·회전·속도·외력·게임 고유분).
            foreach (var pair in pendingSnaps)
            {
                var target = entityRegistry.Get(pair.Key);
                if (target == null)
                {
                    continue;
                }
                EntitySnap snap = pair.Value;
                GameFramework.World.EntityMotionExtensions.SetPosition(target, snap.position);
                GameFramework.World.EntityMotionExtensions.SetRotation(target, snap.rotation);
                GameFramework.World.EntityMotionExtensions.SetVelocity(target, snap.velocity);

                //  판단은 teleportTracker가 이미 했다 — 여기선 클라 쪽 카운터를 서버 값에 맞춰
                //  두 사이드가 같은 값을 들고 있게만 한다(안 맞추면 나중에 읽는 쪽이 헷갈린다).
                var targetTransform = target.Get<GameFramework.World.Transform>();
                if (targetTransform != null)
                {
                    targetTransform.TeleportCount = snap.teleportCount;
                }

                var motionContributions = target.Get<MotionContributions>();
                if (motionContributions != null)
                {
                    motionContributions.Items.Clear();
                    motionContributions.Items.AddRange(snap.contributions);
                }

                correction.ApplyAuthoritative(target, snap, deltaTime);

                // World에 쓴 포즈를 MotionBridge가 rb에 밀고, PhysX가 새 포즈를 보도록 수동
                // SyncTransforms(autoSyncTransforms=false).
                motionBridge.PushMotion(target);
            }
            Physics.SyncTransforms();

            if (tooOld || currentTick - anchorTick > MaxReplayTicks)
            {
                NotifyRenderCorrections();
                pendingSnaps.Clear();
                return;
            }

            // 재생: 이미 예측했던 과거 틱(anchor+1 ~ currentTick-1)을 이동+물리로 재구성.
            // 내 입력은 inputHistory에서, 남의 입력은 서버가 되뿌려 준 것에서 꺼내 매 틱 다시
            // 먹인다 — 라이브와 재생이 같은 입력을 봐야 같은 답이 나온다. 남의 입력이 늦게
            // 도착하면 바로 이 재생이 그 구간을 정확한 궤적으로 고쳐 준다.
            var inputBuffer = worldEntity.Get<InputBuffer>();   // 입력 버퍼 (WorldEventBuffer 아님 — 이름 구분)
            if (inputBuffer == null)
            {
                NotifyRenderCorrections();
                pendingSnaps.Clear();
                return;
            }
            // 재생이 만든 연출 이벤트(cue 등)는 이미 라이브 때 방출됐으므로 버린다.
            using (worldEventBuffer.Suppress())
            {
                for (long t = anchorTick + 1; t < currentTick; t++)
                {
                    inputBuffer.Current = inputHistory.TryGet(t, out var recorded) ? recorded : null;
                    remoteInput.ApplyAll(t);

                    // 재생 = 라이브와 같은 단일 진입점. 입력에 실린 발동도 월드가 알아서 한다.
                    world.Tick(t, deltaTime);

                    // 보정값으로 다시 보관한다(다음 비교·재생이 낡은 값을 안 보도록).
                    world.SaveState(t);
                }
            }

            NotifyRenderCorrections();
            pendingSnaps.Clear();
        }
    }
}
