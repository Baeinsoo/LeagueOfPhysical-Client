using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 롤백 재조정(호스트 서비스). 서버 스냅이 도착하면 그 틱으로 하드 복원하고,
    /// 저장된 입력으로 이동·물리를 현재 직전 틱까지 재생해 예측 오차를 보정한다.
    /// 어빌리티/상태이상은 재생하지 않는다(이동만) — 확장은 후속 슬라이스.
    /// </summary>
    public class Reconciler
    {
        private const float Threshold = 0.06f;     // 이 이하 오차는 롤백 스킵(예측 정확)
        private const long MaxReplayTicks = 128;   // 격차가 이보다 크면 텔레포트 폴백(재생 생략)

        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private InputHistory inputHistory;
        [Inject] private MovementSystem movementSystem;
        [Inject] private GameFramework.IPhysicsSimulator physicsSimulator;

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

            // errorGate: 예측이 서버와 충분히 가까우면 아무것도 안 함.
            if (snapshotHistory.TryGet(anchorTick, out var predicted) &&
                !GameFramework.Netcode.ReconcileGate.ShouldReconcile(
                    predicted.Position, snap.position.ToNumerics(), Threshold))
            {
                return;
            }

            // 하드 복원: 내 캐릭을 서버 스냅(anchorTick) 상태로. reactive 경로가 rigidbody에 반영되므로
            // PhysX가 새 포즈를 보도록 수동 SyncTransforms(autoSyncTransforms=false).
            entity.position = snap.position;
            entity.rotation = snap.rotation;
            entity.velocity = snap.velocity;
            entity.PushMotionToPhysics();
            Physics.SyncTransforms();

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
                buffer.Current = inputHistory.TryGet(t, out var cmd) ? cmd : null;

                movementSystem.Tick(worldEntity, deltaTime);
                entity.PushMotionToPhysics();
                physicsSimulator.Simulate(deltaTime);
                entity.SyncPhysics();

                // 보정값으로 스냅 히스토리 갱신(다음 비교가 stale값을 안 보도록).
                var transform = worldEntity.Get<GameFramework.World.Transform>();
                var velocity = worldEntity.Get<GameFramework.World.Velocity>();
                snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                    t, transform.Position, transform.Rotation, velocity.Linear));
            }
        }
    }
}
