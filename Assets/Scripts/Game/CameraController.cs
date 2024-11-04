using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class CameraController : MonoBehaviour
    {
        [Header("[Camera]")]
        [SerializeField] private Camera targetCamera;

        [Header("[Settgins]")]
        [SerializeField] private float smoothSpeed = 0.125f;
        [SerializeField] private Vector3 offset;

        public Transform target { get; set; }
        public bool followTarget { get; set; }

        private Transform myTransform;

        private void Awake()
        {
            myTransform = transform;
        }

        private void LateUpdate()
        {
            if (followTarget && target != null)
            {
                var desiredPosition = target.position + offset;
                var smoothedPosition = Vector3.Lerp(myTransform.position, desiredPosition, smoothSpeed);

                myTransform.position = smoothedPosition;
            }
        }
    }
}
