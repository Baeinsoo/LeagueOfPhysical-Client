using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 말 활쏘기의 어깨 뒤 3인칭 카메라. 카메라가 보는 방향을 조준으로 넘기고,
    /// 시위를 당기면 화각을 좁히고 카메라를 가까이 당긴다. 당긴 정도는 시뮬 상태를 읽는다.
    /// </summary>
    public class MountedAimView : ILateTickable, IDisposable
    {
        // 쏘는 순간 시뮬은 당김을 0으로 한 번에 떨어뜨린다 — 그대로 따라가면 화각이 튄다.
        private const float RisePerSecond = 6f;
        private const float FallPerSecond = 2.5f;

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private float shownDrawRatio;

        public MountedAimView(PlayerInputManager input, CameraController cameraController,
                              IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void LateTick()
        {
            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                return;
            }

            // 유니티의 x 회전은 양수가 아래를 보고 조준 각도는 양수가 위라 부호를 뒤집는다(ArcheryAimView와 같다).
            input.SetAim(cameraController.Yaw, -cameraController.Pitch);

            var aim = MyAim();
            float target = aim != null && aim.Drawing ? aim.DrawRatio : 0f;
            float rate = target > shownDrawRatio ? RisePerSecond : FallPerSecond;
            shownDrawRatio = Mathf.MoveTowards(shownDrawRatio, target, rate * Time.deltaTime);

            var (fov, distance) = MountedCameraZoom.For(shownDrawRatio);
            camera.fieldOfView = fov;
            cameraController.DistanceOverride = distance;
            // 카메라가 도는 중심을 오른쪽으로 비켜 기수가 화면 왼쪽 아래로 가게 한다.
            cameraController.PivotOffset = camera.transform.right * MountedCameraZoom.ShoulderRight;
        }

        public void Dispose()
        {
            if (cameraController != null)
            {
                cameraController.DistanceOverride = null;
                cameraController.PivotOffset = Vector3.zero;
            }
        }

        private ArcheryAim MyAim()
        {
            if (playerContext.entityId == null)
            {
                return null;
            }
            return entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
        }
    }
}
