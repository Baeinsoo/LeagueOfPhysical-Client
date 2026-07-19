using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 원격 엔티티(남 캐릭·아이템)의 표준 스냅샷 보간. 공유 재생 시계의 renderTime을 감싸는 두 스냅 사이를
    /// Hermite(위치)+Slerp(회전)로 블렌드해 엔티티(월드 위치·kinematic 콜라이더)와 비주얼 메시에 쓴다.
    /// 예측·스프링 없음. 감쌀 쌍이 없으면 최신 스냅 hold(외삽 안 함).
    /// </summary>
    public class RemoteEntityInterpolator : MonoBehaviour
    {
        [Inject] private RemoteInterpolationClock clock;

        public LOPActor actor { get; set; }
        public GameFramework.World.Entity worldEntity { get; set; }
        public LOPEntityView entityView { get; set; }

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

                    Apply(pos, rot);
                    return;
                }
            }

            // 언더런(renderTime이 최신 스냅보다 앞 or 감쌀 쌍 없음) → 최신 스냅 hold.
            EntitySnap newest = snaps[snaps.Count - 1];
            Apply(newest.position, Quaternion.Euler(newest.rotation));
        }

        // 엔티티(월드 Transform → reactive로 kinematic 콜라이더 + 네임플레이트)는 항상 갱신 —
        // 비주얼 애셋이 async 로드 중이어도 콜라이더/위치가 얼어붙지 않게. 비주얼 메시는 로드된 뒤에만.
        private void Apply(Vector3 pos, Quaternion rot)
        {
            GameFramework.World.EntityMotionExtensions.SetPosition(worldEntity, pos);
            GameFramework.World.EntityMotionExtensions.SetRotation(worldEntity, rot.eulerAngles);
            if (entityView.visualGameObject != null)
            {
                entityView.visualGameObject.transform.position = pos;
                entityView.visualGameObject.transform.rotation = rot;
            }
        }
    }
}
