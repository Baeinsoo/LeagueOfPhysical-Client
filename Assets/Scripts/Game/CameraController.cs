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

        [Header("Limits")]
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 80f;
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
                Vector3 offset = mainCamera.transform.position - target.position;
                distance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
                yaw = mainCamera.transform.eulerAngles.y;
                pitch = mainCamera.transform.eulerAngles.x;
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
            Vector3 position = Target.position - (rotation * Vector3.forward * distance);

            mainCamera.transform.position = position;
            mainCamera.transform.rotation = rotation;
        }

        private float SmoothDamp(float current, float target, float damping, float deltaTime)
        {
            float factor = 1f - Mathf.Exp(-damping * deltaTime);
            return Mathf.Lerp(current, target, factor);
        }
    }
}
