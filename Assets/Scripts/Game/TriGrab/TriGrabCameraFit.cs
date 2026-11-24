using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 경기장 전체를 바로 위에서 내려다보는 고정 카메라(스펙 §3). 화면 위 = 월드 +z라 조준 스틱의 화면 방향이 곧 경기장 방향이다.
    /// 화면비에 맞춰 경기장 원(바깥 벽 윗면까지)이 위 주문 띠 아래로 다 들어오는 높이까지만 올린다 — 세로 화면이면 더 높아진다.
    /// </summary>
    public class TriGrabCameraFit : ITickable
    {
        /// <summary>담아야 할 경기장 반지름(m) — 바깥 벽 안쪽 면.</summary>
        public const float ArenaRadius = LOP.TriGrab.TriGrabConfig.ArenaOuterRadius;
        public const float WallHeight = 1.2f;
        /// <summary>화면 위쪽 이만큼(뷰포트 몫)은 주문 띠 자리라 비운다.</summary>
        public const float TopReserve = 0.12f;
        public const float SideMargin = 0.02f;
        private const float MinDistance = 8f;
        private const float MaxDistance = 150f;

        /// <summary>바로 위에서, 화면 위가 +z.</summary>
        public static readonly Quaternion TopDown = Quaternion.Euler(90f, 0f, 0f);

        private readonly CameraController cameraController;
        private float fittedAspect = -1f;

        public TriGrabCameraFit(CameraController cameraController)
        {
            this.cameraController = cameraController;
        }

        public void Tick()
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            if (camera == null || Mathf.Approximately(camera.aspect, fittedAspect)) return;
            fittedAspect = camera.aspect;
            camera.transform.rotation = TopDown;
            camera.transform.position = FitPosition(camera);
        }

        /// <summary>지금 각도·화면비에서 경기장이 다 들어오는 가장 가까운 자리. 경기장 가운데(원점)를 본다.</summary>
        public static Vector3 FitPosition(Camera camera)
        {
            var forward = camera.transform.forward;
            float lo = MinDistance, hi = MaxDistance;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) * 0.5f;
                camera.transform.position = -forward * mid;
                if (Fits(camera)) hi = mid; else lo = mid;
            }
            return -forward * hi;
        }

        /// <summary>경기장 원의 바닥·벽 윗면 점들이 모두 화면 안(위 띠 자리 빼고)에 있나.</summary>
        public static bool Fits(Camera camera)
        {
            const int samples = 32;
            for (int i = 0; i < samples; i++)
            {
                float a = i * Mathf.PI * 2f / samples;
                var ground = new Vector3(Mathf.Cos(a) * ArenaRadius, 0f, Mathf.Sin(a) * ArenaRadius);
                if (!Inside(camera.WorldToViewportPoint(ground))) return false;
                if (!Inside(camera.WorldToViewportPoint(ground + Vector3.up * WallHeight))) return false;
            }
            return true;
        }

        private static bool Inside(Vector3 v)
            => v.z > 0f && v.x >= SideMargin && v.x <= 1f - SideMargin && v.y >= SideMargin && v.y <= 1f - TopReserve;
    }
}
