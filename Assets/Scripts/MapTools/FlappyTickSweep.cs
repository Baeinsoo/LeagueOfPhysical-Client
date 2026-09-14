using GameFramework.Physics;
using UnityEngine;

namespace LOP.MapTools
{
    /// <summary>
    /// "한 틱 나아가면 몸이 어디든 닿나"를 <b>게임의 진짜 이동 커널</b>(<see cref="KinematicMover"/>)로
    /// 그대로 답한다. 탐색(<see cref="CleanRunSearch"/>)의 자유공간 검사가 이것을 쓴다.
    ///
    /// <para><b>왜 판정을 새로 짜지 않고 커널을 부르나.</b> 예전 탐색은 도착점 하나를 0.1m 눈금에
    /// 붙여 <c>CheckCapsule</c>로 찍었다. 커널은 그러지 않는다 — 캡슐을 한 틱 동안 <i>쓸고</i>,
    /// 벽에서 0.02m 띄우고, 닿으면 미끄러진다. 그래서 탐색이 커널보다 <b>관대</b>했고, 탐색이
    /// 찾은 경로가 재생에서 멈췄다. 같은 규칙을 두 곳에 따로 적으면 그 어긋남이 또 생긴다 —
    /// 부동소수점 한 자리까지 같아야 하는데(한 식으로 쓰면 JIT이 FMA로 합쳐 버리는 일까지
    /// 있었다), 그건 "같은 모양으로 다시 적기"로는 지킬 수 없다. 그래서 <b>같은 코드를 부른다.</b></para>
    ///
    /// <para>돌려주는 것은 "닿았나"뿐이다. 미끄러진 결과 위치는 탐색에 필요 없다 — 한 번이라도
    /// 닿으면 그 갈래는 어차피 버린다(클린런 = 무접촉 주파). 닿지 않았을 때의 끝 위치는
    /// <see cref="FlappyTickMath.AdvanceHeight"/>와 정확히 같다.</para>
    /// </summary>
    public sealed class FlappyTickSweep
    {
        private readonly HitWatcher watcher;
        private readonly float forwardSpeed;
        private readonly float radius;
        private readonly float height;
        private readonly float tickSeconds;
        private readonly int layerMask;

        public FlappyTickSweep(ICollisionQuery query, float forwardSpeed, float radius, float height,
                               float tickSeconds, int layerMask)
        {
            if (query == null)
            {
                throw new System.ArgumentNullException(nameof(query));
            }
            watcher = new HitWatcher(query);
            this.forwardSpeed = forwardSpeed;
            this.radius = radius;
            this.height = height;
            this.tickSeconds = tickSeconds;
            this.layerMask = layerMask;
        }

        /// <summary>탐색에 그대로 넘길 수 있는 프로브. 델리게이트 타입이 박혀 있어 점 프로브와
        /// 뒤바꿔 넘기면 컴파일 에러다.</summary>
        public TickSweepProbe Probe => IsFree;

        public bool IsFree(float x, float y, float verticalSpeed)
            => IsFree(x, y, verticalSpeed, out _);

        /// <param name="endPosition">닿지 않았을 때의 도착 위치. 닿았으면 커널이 벽에서 멈추고
        /// 미끄러뜨린 자리다 — 진단에만 쓴다.</param>
        public bool IsFree(float x, float y, float verticalSpeed, out Vector3 endPosition)
        {
            watcher.Reset();
            //  검사기의 한 틱(FlappyMapPlayabilityCheck.Step)이 커널에 넣는 것과 같은 입력이다.
            //  stepOffset 0 — 나는 몸은 턱을 기어오르지 않는다.
            //  groundProbe 0 — 발밑 훑기는 걷는 몸의 것이라, 나는 몸에 켜면 닿지도 않은 바닥에
            //  붙어 버린다(KinematicMoveInput 주석 참고).
            var result = KinematicMover.Move(new KinematicMoveInput(
                new Vector3(x, y, 0f),
                new Vector3(forwardSpeed, verticalSpeed, 0f),
                radius, height, tickSeconds, layerMask,
                stepOffset: 0f, groundProbe: 0f), watcher);
            endPosition = result.position;
            return watcher.SawHit == false;
        }

        /// <summary>sweep 도중 한 번이라도 닿았는지만 기록하는 포트 덮개
        /// (FlappyWorld의 HitTrackingQuery·검사기의 HitWatcher와 같은 역할).</summary>
        private sealed class HitWatcher : ICollisionQuery
        {
            private readonly ICollisionQuery inner;
            public bool SawHit { get; private set; }

            public HitWatcher(ICollisionQuery inner) => this.inner = inner;

            public void Reset() => SawHit = false;

            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                                            Vector3 direction, float distance, int layerMask)
            {
                CollisionHit hit = inner.CapsuleCast(point1, point2, radius, direction, distance, layerMask);
                if (hit.HasHit)
                {
                    SawHit = true;
                }
                return hit;
            }

            public CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
                => inner.Raycast(origin, direction, distance, layerMask);

            public CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => inner.OverlapSphere(center, radius, layerMask);
        }
    }
}
