using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [DefaultExecutionOrder(3000)]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float zoomSensitivity = 2f;
        [SerializeField] private float rotationSensitivity = 2f;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 20f;

        public Camera MainCamera => mainCamera;
        public Transform Target { get; private set; }

        private float yaw;
        private float pitch;
        private float distance;

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

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 position = Target.position - (rotation * Vector3.forward * distance);

            mainCamera.transform.position = position;
            mainCamera.transform.rotation = rotation;
        }

        public void ProcessTouchInput(Vector2 deltaPosition)
        {
            yaw += deltaPosition.x * rotationSensitivity * 0.1f;

            pitch -= deltaPosition.y * rotationSensitivity * 0.1f;
            pitch = Mathf.Clamp(pitch, -20f, 80f);

            distance -= deltaPosition.y * zoomSensitivity * 0.01f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }
}
