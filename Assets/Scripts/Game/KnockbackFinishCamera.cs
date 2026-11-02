using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 판이 끝나는 탈락 때 카메라를 그 몸이 떨어진 자리로 0.5초에 걸쳐 당긴다.
    /// 카메라 대상을 임시 점으로 바꾸고 거리만 줄인다 — 시간 배율·틱은 건드리지 않는다.
    /// </summary>
    public class KnockbackFinishCamera : ITickable, IDisposable
    {
        private const float Duration = 0.5f;
        private const float DistanceScale = 0.55f;

        private readonly KnockbackKoBroadcaster broadcaster;
        private readonly CameraController camera;

        private GameObject focus;
        private Vector3 from;
        private Vector3 to;
        private float startDistance;
        private float elapsed;

        /// <summary>당기는 중이거나 당겨 둔 상태. 이때는 관전 대상 전환이 카메라를 뺏지 않는다.</summary>
        public bool Active { get; private set; }

        public KnockbackFinishCamera(KnockbackKoBroadcaster broadcaster, CameraController camera)
        {
            this.broadcaster = broadcaster;
            this.camera = camera;
            broadcaster.Finished += OnFinished;
        }

        public void Dispose()
        {
            broadcaster.Finished -= OnFinished;
            if (Active && camera != null) camera.DistanceOverride = null;
            if (focus != null) UnityEngine.Object.Destroy(focus);
            focus = null;
        }

        private void OnFinished(Vector3? position)
        {
            //  떨어진 자리를 모르면 엉뚱한 곳으로 당기지 않는다.
            if (position == null || Active || camera == null || camera.Target == null) return;

            focus = new GameObject("KnockbackFinishFocus");
            from = camera.Target.position;
            to = position.Value;
            focus.transform.position = from;
            startDistance = camera.DistanceOverride ?? camera.Distance;
            camera.SetTarget(focus.transform);   // 지금 카메라 자리에서 이어받는다 — 튀지 않는다
            elapsed = 0f;
            Active = true;
        }

        public void Tick()
        {
            if (!Active || elapsed >= Duration || focus == null) return;

            elapsed = Mathf.Min(Duration, elapsed + Time.unscaledDeltaTime);
            float s = Mathf.SmoothStep(0f, 1f, elapsed / Duration);
            focus.transform.position = Vector3.Lerp(from, to, s);
            camera.DistanceOverride = Mathf.Lerp(startDistance, startDistance * DistanceScale, s);
        }
    }
}
