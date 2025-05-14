using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace LOP
{
    [DefaultExecutionOrder(3000)]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain cinemachineBrain;
        [SerializeField] private CinemachineCamera cinemachineCamera;

        private void Awake()
        {
            cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        }

        public void SetTarget(Transform target)
        {
            cinemachineCamera.Follow = target;
            cinemachineCamera.LookAt = target;
        }

        private void LateUpdate()
        {
            cinemachineBrain.ManualUpdate();
        }
    }
}
