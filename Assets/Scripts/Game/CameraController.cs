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

        /// <summary>
        /// 회전에 덧붙이는 각도(도). x는 좌우, y는 위아래이며 <b>유니티 부호</b>다(양수가 아래).
        /// 활쏘기의 손떨림이 이걸 쓴다 — 카메라 주인이 직접 더해야 <see cref="LateUpdate"/>가
        /// 회전을 덮어쓰는 것과 실행 순서로 다투지 않는다. 기본 0이라 안 쓰는 모드는 영향이 없다.
        /// </summary>
        public Vector2 AimSwayDegrees { get; set; }

        /// <summary>
        /// 플레이어가 직접 조작한(흔들림을 더하기 전) 각도(도) — <see cref="LateUpdate"/>가
        /// 렌더링에 쓰는 <c>yaw + AimSwayDegrees.x</c>와 다르다. 활쏘기 조준은 반드시 이 값을
        /// 읽어야 한다 — 화면에 그려진 각(흔들림 포함)을 읽으면 흔들림이 조준에도 얹혀
        /// 시뮬이 같은 흔들림을 또 더하게 된다(이중 적용).
        /// </summary>
        public float Yaw => yaw;

        /// <summary>Pitch도 Yaw와 같은 이유로 흔들림을 더하기 전 값이다. 유니티 부호(양수가 아래).</summary>
        public float Pitch => pitch;

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

        /// <summary>
        /// 겨눈 각을 <b>그 자리에서</b> 이만큼 돌린다(도). x는 좌우, y는 위아래이며 <b>양수가 위</b>다
        /// — <see cref="Pitch"/>의 유니티 부호와 반대이니 주의.
        ///
        /// <para><see cref="ProcessTouchInput"/>와 달리 속도를 쌓지 않는다. 부른 만큼만 돌고, 안
        /// 부르면 그 자리에 선다. 둘러보는 모드는 튕기면 미끄러지는 쪽이 편하지만 <b>겨누는 모드는
        /// 멈출 수 있어야 한다</b> — 속도를 쌓는 방식은 손을 떼도 5도쯤 더 흘러가서 겨눈 곳에 설 수가
        /// 없었다(90m 과녁의 10점 링이 0.25도다). 카메라 거리(줌)도 건드리지 않는다.</para>
        /// </summary>
        public void AimBy(Vector2 degrees)
        {
            yaw += degrees.x;
            //  유니티의 x 회전은 양수가 아래를 본다 — 위로 올리라는 뜻이면 빼야 한다.
            pitch = Mathf.Clamp(pitch - degrees.y, minPitch, maxPitch);
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
            Quaternion rotation = Quaternion.Euler(pitch + AimSwayDegrees.y, yaw + AimSwayDegrees.x, 0);
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
