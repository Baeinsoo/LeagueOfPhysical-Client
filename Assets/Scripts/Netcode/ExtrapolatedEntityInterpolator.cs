using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 외삽 모드 엔티티. 남의 입력을 모르니 굴리지 않고, 마지막 스냅의 위치·속도에서 지금(추정 서버시각)까지
    /// 이어 그린다(중력 등 가속도 포함 — <see cref="GameFramework.Netcode.SnapshotExtrapolation"/>).
    /// 새 스냅이 오면 궤적이 바뀌는데, 그 튐은 <see cref="renderCorrectionSmoother"/>가 흡수한다 —
    /// <b>화면 = 궤적 + 남은 오차</b>이고 그 오차만 0으로 몰아가므로, 화면이 궤적에 올라탄 채 같은
    /// 속도로 간다.
    ///
    /// <para>(예전엔 "붙잡아 둔 옛 자리에서 목표 쪽으로 0.1초에 걸쳐 Lerp"였다. 목표가 계속
    /// 도망가는데 0.02초마다 스냅이 와서 그 0.1초가 매번 리셋돼, 격차의 20%만 좁히다 다시
    /// 시작하기를 반복했다. 그래서 화면이 <b>항상 0.1초어치(약 1.1m) 뒤에서 끌려왔다</b> — 실측:
    /// 전진 11m/s에서 1.1m, 종단낙하 32m/s에서 3.0m로 둘 다 정확히 0.1초어치였다.)</para>
    /// </summary>
    public class ExtrapolatedEntityInterpolator : MonoBehaviour, ICleanup
    {
        private const float MaxExtrapolation = 0.25f;   // Source cl_extrapolate_amount 기본값과 같은 값 — 오래 못 받으면 그 자리에 세운다

        [Inject] private GameFramework.Netcode.INetworkTime networkTime;

        public GameFramework.World.Entity worldEntity { get; set; }
        public LOPActor actor { get; set; }

        // 캐릭터가 아니면(아이템 등) null — StunAppearance는 캐릭터에만 붙는다.
        public StunAppearance stunAppearance { get; set; }

        /// <summary>중력 등 가속도. EntityBinder가 <see cref="IExtrapolationAcceleration"/>에서 꺼내 건넨다.</summary>
        public Vector3 acceleration { get; set; }

        /// <summary>궤적이 바뀔 때의 튐을 흡수한다. EntityBinder가 붙여 준다.</summary>
        public GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother { get; set; }

        private EntitySnap latest;
        private bool hasSnap;

        // 이번 프레임에 실제로 화면/월드에 찍은 위치. 새 궤적으로 갈아탈 때의 "이음매 시작점"이
        // 이 값이어야 한다 — 궤적을 다시 계산한 값에서 출발하면 화면에 없던 자리에서 시작해 튄다.
        private Vector3 lastRendered;
        private bool hasRendered;

        /// <summary>서버 스냅 수신. 타임스탬프 순으로만 반영, 최신보다 오래되거나 같은 건 무시(unreliable 순서역전 방지).</summary>
        public void AddServerEntitySnap(EntitySnap snap)
        {
            if (hasSnap && snap.timestamp <= latest.timestamp)
            {
                return;
            }
            bool switching = hasSnap && hasRendered;
            latest = snap;
            hasSnap = true;
            RemoteSyncProbe.Arrived(worldEntity?.Id ?? name, Time.unscaledTimeAsDouble);   // [진단용 임시]

            if (switching)
            {
                //  새 궤적의 "지금" 자리와, 화면에 그리고 있던 자리의 차이 = 흡수할 튐.
                //  스무더는 이걸 오차로 잡아 0으로 몰아간다 — 화면은 새 궤적 위에 그 오차를
                //  얹어 그리므로, 궤적과 같은 속도로 가면서 오차만 줄어든다.
                CurrentState(out Vector3 fresh, out Vector3 freshVelocity);
                RemoteSyncProbe.Corrected(worldEntity?.Id ?? name, (fresh - lastRendered).magnitude);   // [진단용 임시]
                renderCorrectionSmoother?.OnCorrection(
                    lastRendered.ToNumerics(), fresh.ToNumerics(), freshVelocity.ToNumerics(), Time.deltaTime);
            }
            stunAppearance?.SetState(StunVisuals.Of(snap));
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
            // 멈춤 여부도 스냅이 스스로 들고 있는 틱으로 판단한다 — 클라 시계는 서버보다 앞서
            // 달리므로 그쪽과 비교하면 아직 얼어 있어야 할 새가 먼저 풀린 것으로 읽힌다.
            bool isStunned = latest.stunEndTick > latest.tick;
            System.Numerics.Vector3 accel = isStunned ? System.Numerics.Vector3.Zero : acceleration.ToNumerics();
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

            //  화면 = 궤적 + 남은 오차. 오차가 0이면 궤적 그 자리다(뒤처지지 않는다).
            Vector3 target = renderCorrectionSmoother != null
                ? renderCorrectionSmoother.Target(rawPosition.ToNumerics()).ToUnity()
                : rawPosition;
            renderCorrectionSmoother?.Advance(Time.deltaTime);

            // 월드 엔티티에도 쓴다 — 몸싸움(bird-vs-bird collision)이 이 값을 읽어 로컬 새와 부딪힌다.
            // 원격은 클라 시뮬 대상이 아니라(Simulated 미부여) 시뮬이 이 값을 덮어쓰지 않는다.
            if (worldEntity != null)
            {
                // 위치는 오차를 얹은 target을 쓴다 — 판정은 "화면에 보이는 자리"에서 일어나야
                // 플레이어가 납득한다. 흡수 중엔 순수 궤적(rawPosition)과 다르지만, 그 차이가
                // 바로 눈에 보이는 값이다.
                GameFramework.World.EntityMotionExtensions.SetPosition(worldEntity, target);
                // 속도는 오차를 안 섞은 값을 쓴다 — 흡수는 "그리는 자리"를 부드럽게 잇기 위한 연출용
                // 보정일 뿐 실제 움직임이 아니다. 여기에 블렌드 오프셋을 섞으면 VerticalBounce.ResolveVy가
                // 읽는 속도가 물리적으로 말이 안 되는 값이 된다.
                GameFramework.World.EntityMotionExtensions.SetVelocity(worldEntity, velocity);
            }
            if (actor?.visualGameObject != null)
            {
                actor.visualGameObject.transform.position = target;
                actor.visualGameObject.transform.rotation = Quaternion.Euler(latest.rotation);
            }

            // [진단용 임시] 외삽은 "지금"을 그리므로 시각 오프셋은 음수(미래)로 기록한다.
            RemoteSyncProbe.Rendered(worldEntity?.Id ?? name, target, velocity, bracketed: true,
                                     behind: -(networkTime.ServerNow - latest.timestamp));

            lastRendered = target;
            hasRendered = true;
        }

        public void Cleanup()
        {
            hasSnap = false;
            hasRendered = false;
            renderCorrectionSmoother?.Reset();
        }
    }
}
