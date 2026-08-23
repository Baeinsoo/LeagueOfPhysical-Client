using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 보간 모드 엔티티의 표준 스냅샷 보간. 공유 재생 시계의 renderTime을 감싸는 두 스냅 사이를
    /// Hermite(위치)+Slerp(회전)로 블렌드해 엔티티(월드 위치·kinematic 콜라이더)와 비주얼 메시에 쓴다.
    /// 예측·스프링 없음. 감쌀 쌍이 없으면 최신 스냅 hold(외삽 안 함).
    /// </summary>
    public class SnapshotEntityInterpolator : MonoBehaviour
    {
        [Inject] private RemoteInterpolationClock clock;

        public GameFramework.World.Entity worldEntity { get; set; }
        public LOPActor actor { get; set; }

        private readonly BoundedList<EntitySnap> snaps = new BoundedList<EntitySnap>(32);

        /// <summary>서버 스냅 수신. 타임스탬프 순으로만 추가, 최신보다 오래되거나 같은 건 무시(unreliable 순서역전 방지).</summary>
        public void AddServerEntitySnap(EntitySnap snap)
        {
            if (snaps.Count > 0 && snap.timestamp <= snaps[snaps.Count - 1].timestamp)
            {
                return;
            }
            snaps.Add(snap);
        }

        private void LateUpdate()
        {
            if (snaps.Count == 0 || clock.HasSnapshot == false)
            {
                return;
            }

            double renderTime = clock.RenderTime;

            for (int i = snaps.Count - 1; i >= 1; i--)
            {
                EntitySnap newer = snaps[i];
                EntitySnap older = snaps[i - 1];
                if (older.timestamp <= renderTime && renderTime <= newer.timestamp)
                {
                    float dt = (float)(newer.timestamp - older.timestamp);
                    float u = dt > 0f ? Mathf.Clamp01((float)((renderTime - older.timestamp) / dt)) : 0f;

                    Vector3 pos = GameFramework.Netcode.Hermite.Position(
                        older.position.ToNumerics(), older.velocity.ToNumerics(),
                        newer.position.ToNumerics(), newer.velocity.ToNumerics(), dt, u).ToUnity();
                    Quaternion rot = Quaternion.Slerp(
                        Quaternion.Euler(older.rotation), Quaternion.Euler(newer.rotation), u);
                    // 속도는 위치 곡선을 미분해 얻는다 — 화면에 보이는 움직임과 정확히 일치.
                    // 뷰가 걷기 애니를 이 값에서 파생하고, 발 미끄러짐 보정도 여기 붙는다.
                    Vector3 vel = GameFramework.Netcode.Hermite.Velocity(
                        older.position.ToNumerics(), older.velocity.ToNumerics(),
                        newer.position.ToNumerics(), newer.velocity.ToNumerics(), dt, u).ToUnity();

                    Apply(pos, rot, vel);
                    return;
                }
            }

            // 언더런(renderTime이 최신 스냅보다 앞 or 감쌀 쌍 없음) → 최신 스냅 hold.
            EntitySnap newest = snaps[snaps.Count - 1];
            Apply(newest.position, Quaternion.Euler(newest.rotation), newest.velocity);
        }

        // 엔티티(월드 Transform → reactive로 kinematic 콜라이더 + 네임플레이트)는 항상 갱신 —
        // 비주얼 애셋이 async 로드 중이어도 콜라이더/위치가 얼어붙지 않게. 비주얼 메시는 로드된 뒤에만.
        private void Apply(Vector3 pos, Quaternion rot, Vector3 velocity)
        {
            GameFramework.World.EntityMotionExtensions.SetPosition(worldEntity, pos);
            GameFramework.World.EntityMotionExtensions.SetRotation(worldEntity, rot.eulerAngles);
            // 원격은 클라 시뮬 대상이 아니라(Simulated 미부여) 이 값을 물리에 쓰는 코드가 없다 — 연출 전용.
            GameFramework.World.EntityMotionExtensions.SetVelocity(worldEntity, velocity);
            if (actor.visualGameObject != null)
            {
                actor.visualGameObject.transform.position = pos;
                actor.visualGameObject.transform.rotation = rot;
            }
        }
    }
}
