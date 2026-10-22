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

        public static (float fov, float distance) For(float drawRatio)
        {
            float t = Mathf.Clamp01(drawRatio);
            return (Mathf.Lerp(WideFov, DrawnFov, t), Mathf.Lerp(WideDistance, DrawnDistance, t));
        }
    }
}
