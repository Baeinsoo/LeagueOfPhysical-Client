using UnityEngine;

namespace LOP
{
    /// <summary>시위를 당길수록 화각이 좁아지고 카메라가 어깨 쪽으로 다가온다. 화면만 바뀌고 조준·판정과는 무관하다.</summary>
    public static class MountedCameraZoom
    {
        public const float WideFov = 62f;
        public const float DrawnFov = 42f;
        public const float WideDistance = 5.6f;
        public const float DrawnDistance = 3.4f;

        /// <summary>카메라를 기수의 오른쪽 어깨 쪽으로 비키는 폭(m).</summary>
        public const float ShoulderRight = 0.9f;

        /// <summary>시위가 이만큼(0~1) 당겨지면 화면은 이미 끝까지 확대된 것으로 본다 — 눌렀을 때 바로 겨누는 느낌.</summary>
        public const float FullZoomDraw = 0.4f;

        /// <summary>확대가 따라붙는 속도(초당). 시위가 차오르는 속도보다 훨씬 빨라 거의 지연이 없다.</summary>
        public const float ZoomRisePerSecond = 20f;

        /// <summary>풀릴 때 속도(초당). 쏘는 순간 시위가 0으로 떨어져도 화각이 튀지 않게 천천히 간다.</summary>
        public const float ZoomFallPerSecond = 2.5f;

        public static float ZoomTarget(float drawRatio) => Mathf.Clamp01(drawRatio / FullZoomDraw);

        /// <summary>지금 보이는 확대 정도(0~1)를 시위 상태에 맞춰 한 프레임 옮긴다.</summary>
        public static float Step(float shownZoom, float drawRatio, float deltaTime)
        {
            float target = ZoomTarget(drawRatio);
            float rate = target > shownZoom ? ZoomRisePerSecond : ZoomFallPerSecond;
            return Mathf.MoveTowards(shownZoom, target, rate * deltaTime);
        }

        public static (float fov, float distance) For(float drawRatio)
        {
            float t = Mathf.Clamp01(drawRatio);
            return (Mathf.Lerp(WideFov, DrawnFov, t), Mathf.Lerp(WideDistance, DrawnDistance, t));
        }
    }
}
