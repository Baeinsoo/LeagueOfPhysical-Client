using UnityEngine;

namespace LOP
{
    [DefaultExecutionOrder(3000)]
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera mainCamera;

        [Header("Rotation")]
        [SerializeField] private float rotationAcceleration = 1000f;
        [SerializeField] private float rotationDamping = 10f;
        [SerializeField] private float rotationMaxSpeed = 500f;

        [Header("Zoom")]
        [SerializeField] private float zoomAcceleration = 1000f;
        [SerializeField] private float zoomDamping = 10f;
        [SerializeField] private float zoomMaxSpeed = 100f;

        [Header("Pivot")]
        // 카메라가 도는 중심을 대상보다 이만큼 위로 올린다(m). 0이면 대상의 발밑을 돈다.
        // 활쏘기처럼 보는 방향이 곧 겨눈 선인 게임은 이 중심이 눈높이여야 한다.
        [SerializeField] private float pivotHeight = 0f;

        // 켜면 대상이 보고 있는 쪽을 그대로 이어받아 시작한다. 끄면 씬에 놓인 카메라 방향에서
        // 시작한다(걸어다니는 모드는 곧 돌리게 되므로 상관없다). 활쏘기처럼 세워 둔 방향이
        // 곧 "겨누고 시작할 곳"인 게임은 켜야 한다 — 안 그러면 사대 반대편을 보고 시작한다.
        [SerializeField] private bool alignYawToTarget = false;

        [Header("Limits")]
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 80f;
        // 중심에서 뒤로 얼마나 떨어질지(m). 음수면 중심보다 **앞**에 선다 — 눈에서 조금 앞으로
        // 나와도 겨눈 선 위에 그대로 있으므로, 자기 몸에 시야가 가리지 않으면서 조준은 정직하다.
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 20f;

        public Camera MainCamera => mainCamera;
        public Transform Target { get; private set; }

        private float yaw;
        private float pitch;
        private float distance;

        // Smoothed velocities
        private float yawVelocity;
        private float pitchVelocity;
        private float zoomVelocity;

        public void SetTarget(Transform target)
        {
            Target = target;

            if (target != null)
            {
                Vector3 offset = mainCamera.transform.position - Pivot(target);
                distance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
                yaw = alignYawToTarget ? target.eulerAngles.y : mainCamera.transform.eulerAngles.y;

                // eulerAngles는 0~360으로 돌려준다 — 위를 보는 각(-10도)이 350으로 읽힌다.
                // 그대로 두면 제한 범위 밖이라 시작하자마자 카메라가 아래로 꺾인다.
                pitch = Mathf.DeltaAngle(0f, mainCamera.transform.eulerAngles.x);
            }
        }

        public void ProcessTouchInput(Vector2 deltaPosition)
        {
            yawVelocity += Mathf.Clamp(deltaPosition.x * rotationAcceleration * 0.001f, -rotationMaxSpeed, rotationMaxSpeed);
            pitchVelocity -= Mathf.Clamp(deltaPosition.y * rotationAcceleration * 0.001f, -rotationMaxSpeed, rotationMaxSpeed);
            zoomVelocity -= Mathf.Clamp(deltaPosition.y * zoomAcceleration * 0.0001f, -zoomMaxSpeed, zoomMaxSpeed);
        }

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            // Apply velocities
            yaw += yawVelocity * deltaTime;
            pitch += pitchVelocity * deltaTime;
            distance += zoomVelocity * deltaTime;

            // Clamp
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            // Damping (critical damping-like)
            yawVelocity = SmoothDamp(yawVelocity, 0, rotationDamping, deltaTime);
            pitchVelocity = SmoothDamp(pitchVelocity, 0, rotationDamping, deltaTime);
            zoomVelocity = SmoothDamp(zoomVelocity, 0, zoomDamping, deltaTime);

            // Apply transform
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 position = Pivot(Target) - (rotation * Vector3.forward * distance);

            mainCamera.transform.position = position;
            mainCamera.transform.rotation = rotation;
        }

        private Vector3 Pivot(Transform target)
        {
            return target.position + Vector3.up * pivotHeight;
        }

        private float SmoothDamp(float current, float target, float damping, float deltaTime)
        {
            float factor = 1f - Mathf.Exp(-damping * deltaTime);
            return Mathf.Lerp(current, target, factor);
        }
    }
}
