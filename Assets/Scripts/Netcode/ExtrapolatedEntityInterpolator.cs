using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 외삽 모드 엔티티. 남의 입력을 모르니 굴리지 않고, 마지막 스냅의 위치·속도에서 지금(추정 서버시각)까지
    /// 이어 그린다(중력 등 가속도 포함 — <see cref="GameFramework.Netcode.SnapshotExtrapolation"/>).
    /// 새 스냅이 오면 궤적이 바뀌어도 위치가 끊기지 않게 짧게 섞는다.
    /// </summary>
    public class ExtrapolatedEntityInterpolator : MonoBehaviour, ICleanup
    {
        private const float MaxExtrapolation = 0.25f;   // Source cl_extrapolate_amount 기본값과 같은 값 — 오래 못 받으면 그 자리에 세운다
        private const float BlendDuration = 0.1f;        // 새 스냅으로 옮겨 타는 시간

        [Inject] private GameFramework.Netcode.INetworkTime networkTime;

        public GameFramework.World.Entity worldEntity { get; set; }
        public LOPActor actor { get; set; }

        // 캐릭터가 아니면(아이템 등) null — StunAppearance는 캐릭터에만 붙는다.
        public StunAppearance stunAppearance { get; set; }

        /// <summary>중력 등 가속도. EntityBinder가 <see cref="IExtrapolationAcceleration"/>에서 꺼내 건넨다.</summary>
        public Vector3 acceleration { get; set; }

        private EntitySnap latest;
        private bool hasSnap;
        private Vector3 blendFrom;
        private float blendRemaining;

        // 이번 프레임에 실제로 화면/월드에 찍은 위치. 서버는 ~50Hz(20ms)로 스냅을 보내는데 블렌드는
        // 0.1초 걸리므로, 블렌드가 끝나기 전에 다음 스냅이 오는 게 정상 상황이다 — 그럴 때 새 블렌드의
        // 시작점은 "원래 궤적을 다시 계산한 값"이 아니라 "방금 화면에 그렸던 값"이어야 한다. 안 그러면
        // 화면에 없던 자리에서 출발해 오히려 튄다.
        private Vector3 lastRendered;
        private bool hasRendered;

        /// <summary>서버 스냅 수신. 타임스탬프 순으로만 반영, 최신보다 오래되거나 같은 건 무시(unreliable 순서역전 방지).</summary>
        public void AddServerEntitySnap(EntitySnap snap)
        {
            if (hasSnap && snap.timestamp <= latest.timestamp)
            {
                return;
            }
            if (hasSnap && hasRendered)
            {
                // 지금 실제로 그려지고 있던 자리(lastRendered)에서 새 궤적으로 갈아탄다.
                blendFrom = lastRendered;
                blendRemaining = BlendDuration;
            }
            latest = snap;
            hasSnap = true;
            stunAppearance?.SetStun(snap.ghost);
        }

        // ServerNow = 서버의 "지금"을 클라가 추정한 값(지연 없음, INetworkTime 계약) — 보간(RenderTime, 일부러
        // 늦춘 재생시각)과 달리 외삽은 "지금"을 목표로 그려야 하므로 이 시계를 쓴다.
        // 위치·속도를 같은 elapsed로 함께 계산한다 — 위치식 p(t)=p0+v0t+0.5at²을 미분하면 v(t)=v0+at라,
        // 다른 elapsed를 쓰면 화면은 이미 가속된 위치인데 속도는 그 순간과 안 맞는 값이 된다(몸싸움
        // 계산이 이 속도를 그대로 읽는다).
        private void CurrentState(out Vector3 position, out Vector3 velocity)
        {
            float elapsed = (float)(networkTime.ServerNow - latest.timestamp);
            // 스턴 상태(맵에 부딪혀 멈춘 새)는 서버에서 위치가 얼어붙어 있고 속도도 0이다 — 그 0.8초
            // 동안의 실제 가속도는 0인데 여기에 중력까지 계속 넣으면, 패킷 손실로 오래 못 받을수록
            // (최대 0.25초) 서 있어야 할 새가 수 m 아래로 꺼지고 가짜 낙하속도까지 생긴다.
            System.Numerics.Vector3 accel = latest.ghost ? System.Numerics.Vector3.Zero : acceleration.ToNumerics();
            position = GameFramework.Netcode.SnapshotExtrapolation.Position(
                latest.position.ToNumerics(), latest.velocity.ToNumerics(), accel, elapsed, MaxExtrapolation).ToUnity();
            velocity = GameFramework.Netcode.SnapshotExtrapolation.Velocity(
                latest.velocity.ToNumerics(), accel, elapsed, MaxExtrapolation).ToUnity();
        }

        private void LateUpdate()
        {
            if (hasSnap == false)
            {
                return;
            }

            CurrentState(out Vector3 rawPosition, out Vector3 velocity);

            Vector3 target = rawPosition;
            if (blendRemaining > 0f)
            {
                blendRemaining -= Time.deltaTime;
                float u = Mathf.Clamp01(1f - blendRemaining / BlendDuration);
                target = Vector3.Lerp(blendFrom, rawPosition, u);
            }

            // 월드 엔티티에도 쓴다 — 몸싸움(bird-vs-bird collision)이 이 값을 읽어 로컬 새와 부딪힌다.
            // 원격은 클라 시뮬 대상이 아니라(Simulated 미부여) 시뮬이 이 값을 덮어쓰지 않는다.
            if (worldEntity != null)
            {
                // 위치는 블렌드된 target을 쓴다 — 몸싸움은 "화면에 보이는 자리"에서 일어나야 플레이어가
                // 납득한다. blend 도중엔 실제 궤적(rawPosition)과 다르지만, 그 차이가 바로 눈에 보이는 값이다.
                GameFramework.World.EntityMotionExtensions.SetPosition(worldEntity, target);
                // 속도는 블렌드하지 않은 값을 쓴다 — blend는 "그리는 자리"를 부드럽게 잇기 위한 연출용
                // 보정일 뿐 실제 움직임이 아니다. 여기에 블렌드 오프셋을 섞으면 FlappyBounce.ResolveVy가
                // 읽는 속도가 물리적으로 말이 안 되는 값이 된다.
                GameFramework.World.EntityMotionExtensions.SetVelocity(worldEntity, velocity);
            }
            if (actor?.visualGameObject != null)
            {
                actor.visualGameObject.transform.position = target;
                actor.visualGameObject.transform.rotation = Quaternion.Euler(latest.rotation);
            }

            lastRendered = target;
            hasRendered = true;
        }

        public void Cleanup()
        {
            hasSnap = false;
            hasRendered = false;
        }
    }
}
