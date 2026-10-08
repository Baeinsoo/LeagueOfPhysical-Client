using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 화면 중심을 통로 중심선에 고정한다 — 원조 Flappy처럼 플레이필드 높이가 일정하고, 새가 통로 안에서
    /// 날갯짓으로 오르내려도 구도는 그대로다.
    ///
    /// <para><b>매끄럽게 하는 것은 절대 높이다.</b> 중심 y(<c>smoothedCenter</c>)를 SmoothDamp로
    /// <see cref="FlappyCorridorLine.CenterAt"/>에 붙이고, 피벗 오프셋은 매 프레임
    /// <c>smoothedCenter − 카메라가 실제로 따라가는 y</c>로 그대로 계산한다. 새 기준 상대값(중심 − 새 y)을
    /// 매끄럽게 하면 날갯짓 박자(약 0.6 s)의 오르내림이 거의 그대로 카메라에 실린다(10-08 최종 리뷰 I1).
    /// 매끄럽게 하는 건 맵을 찾는 순간·통로가 크게 꺾일 때 덜컹이지 않으려는 것뿐이다.</para>
    ///
    /// <para><b>계산 시점.</b> 카메라가 따라가는 것은 sim이 아니라 보간된 몸(visual)이다. 그 몸은 보간기의
    /// LateUpdate가 옮기므로, <see cref="CameraController.BeforeFollow"/>(실행 순서 3000의 LateUpdate 안,
    /// 카메라를 놓기 직전)에서 계산한다 — 그래야 이번 프레임에 그려진 몸 위치와 같은 값을 뺀다. 관전 대상이
    /// 바뀌어도 중심은 새와 무관한 절대 높이라 튀지 않는다.</para>
    ///
    /// <para>표시(<see cref="FlappyCorridorLine"/>)가 없는 맵(전통 코스)도 있다 — 그동안은 아무것도
    /// 하지 않는다(<see cref="CameraController.PivotOffset"/>이 기본값 0에 머문다). 맵 로드가 늦을 수
    /// 있어 못 찾으면 1초에 한 번씩만 다시 찾는다 — 매 프레임 <c>FindFirstObjectByType</c>를 돌리지 않는다.</para>
    /// </summary>
    public class FlappyCorridorCamera : IInitializable, System.IDisposable
    {
        private const float SmoothTime = 0.12f;   // s
        private const float SearchInterval = 1f;   // s

        private readonly CameraController cameraController;

        private FlappyCorridorLine line;
        private float searchCooldown;
        private bool hasCenter;
        private float smoothedCenter;
        private float centerVelocity;

        //  시험 이음매 — 테스트가 열린 씬의 선이나 실제 카메라에 기대지 않도록 바꿔 끼운다.
        internal System.Func<FlappyCorridorLine> findLine = () => Object.FindFirstObjectByType<FlappyCorridorLine>();
        //  카메라가 실제로 따라가는 점(대상 visual 원점 + PivotHeight). 대상이 없으면 null.
        internal System.Func<Vector3?> followPoint;
        internal System.Action<Vector3> applyPivot;
        internal float Offset { get; private set; }

        public FlappyCorridorCamera(CameraController cameraController)
        {
            this.cameraController = cameraController;
            followPoint = () =>
            {
                if (cameraController == null || cameraController.Target == null) { return null; }
                return cameraController.Target.position + Vector3.up * cameraController.PivotHeight;
            };
            applyPivot = p => { if (cameraController != null) { cameraController.PivotOffset = p; } };
        }

        public void Initialize()
        {
            if (cameraController != null)
            {
                cameraController.BeforeFollow += OnBeforeFollow;
            }
        }

        private void OnBeforeFollow()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            if (line == null)
            {
                //  찾았던 선이 사라졌다(맵 다시 로드) — 다시 찾을 때까지 피벗을 제자리로.
                if (hasCenter)
                {
                    Reset();
                }
                searchCooldown -= deltaTime;
                if (searchCooldown > 0f)
                {
                    return;
                }
                searchCooldown = SearchInterval;
                line = findLine();
                if (line == null)
                {
                    return;
                }
            }

            Vector3? follow = followPoint();
            if (follow == null)
            {
                return;
            }
            Vector3 f = follow.Value;

            //  처음엔 지금 카메라가 보는 높이(오프셋 0)에서 출발한다 — 맵을 찾는 순간 덜컹이지 않는다.
            if (hasCenter == false)
            {
                smoothedCenter = f.y;
                centerVelocity = 0f;
                hasCenter = true;
            }
            smoothedCenter = Mathf.SmoothDamp(smoothedCenter, line.CenterAt(f.x), ref centerVelocity,
                                              SmoothTime, Mathf.Infinity, deltaTime);
            Offset = smoothedCenter - f.y;
            applyPivot(Vector3.up * Offset);
        }

        private void Reset()
        {
            hasCenter = false;
            centerVelocity = 0f;
            Offset = 0f;
            applyPivot(Vector3.zero);
        }

        public void Dispose()
        {
            if (cameraController != null)
            {
                cameraController.BeforeFollow -= OnBeforeFollow;
            }
            Reset();
        }
    }
}
