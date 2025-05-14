using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [DefaultExecutionOrder(3000)]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain cinemachineBrain;
        [SerializeField] private CinemachineVirtualCamera cinemachineVirtualCamera;

        private void Awake()
        {
            cinemachineBrain.m_UpdateMethod = CinemachineBrain.UpdateMethod.ManualUpdate;
        }

        public void SetTarget(Transform target)
        {
            cinemachineVirtualCamera.Follow = target;
            cinemachineVirtualCamera.LookAt = target;
        }

        private void LateUpdate()
        {
            cinemachineBrain.ManualUpdate();
        }
    }
}
