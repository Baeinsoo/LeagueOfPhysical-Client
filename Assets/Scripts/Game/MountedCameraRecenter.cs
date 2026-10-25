using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 말 기준 조작에서 카메라가 말 뒤로 천천히 돌아오게 한다. 오른손을 한동안 놓고 있으면
    /// (끌기도 당김도 없을 때) 말이 향한 쪽으로 카메라를 돌려, 스틱으로 말을 돌려도 앞이 보이게 한다.
    /// 손이 닿는 순간 멈춘다. 위아래 각도는 건드리지 않는다.
    /// </summary>
    public sealed class MountedCameraRecenter
    {
        /// <summary>손을 놓고 이만큼 지나야 돌아오기 시작한다(초).</summary>
        public const float IdleSeconds = 1f;
        /// <summary>돌아오는 빠르기(도/초).</summary>
        public const float DegreesPerSecond = 120f;

        private float idle;

        /// <returns>이번 프레임에 카메라 yaw를 더해야 할 각(도).</returns>
        public float Step(float cameraYaw, float horseYaw, bool aiming, float deltaTime)
        {
            if (aiming)
            {
                idle = 0f;
                return 0f;
            }
            idle += deltaTime;
            if (idle < IdleSeconds)
            {
                return 0f;
            }
            float target = Mathf.MoveTowardsAngle(cameraYaw, horseYaw, DegreesPerSecond * deltaTime);
            return Mathf.DeltaAngle(cameraYaw, target);
        }
    }
}
