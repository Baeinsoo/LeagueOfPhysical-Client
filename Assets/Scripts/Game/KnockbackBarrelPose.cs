using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 눕힌 드럼통이 굴러가는 모습을 정하는 순수 식. 통의 축은 굴러가는 방향에 수직이고,
    /// 굴림 각도는 간 거리 ÷ 반지름만큼 쌓는다(미끄러지지 않고 구르는 통의 식).
    /// </summary>
    public static class KnockbackBarrelPose
    {
        private const float MinSpeed = 0.05f;

        /// <summary>
        /// 통의 축(가로로 누운 단위 벡터). 거의 서 있으면 직전 축을 그대로 둔다 — 멈출 때 축이 제멋대로 돌지 않게.
        /// </summary>
        public static Vector3 Axis(Vector3 velocity, Vector3 previousAxis)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.magnitude < MinSpeed)
            {
                return previousAxis;
            }
            flat.Normalize();
            return new Vector3(flat.z, 0f, -flat.x);
        }

        /// <summary>이번 프레임에 더할 굴림 각도(도). 축 방향 오른손 법칙으로 앞으로 구르는 쪽이 양수.</summary>
        public static float RollDegrees(Vector3 velocity, float radius, float deltaSeconds)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            return flat.magnitude * deltaSeconds / radius * Mathf.Rad2Deg;
        }

        public static Quaternion Rotation(Vector3 axis, float rollDegrees)
        {
            return Quaternion.AngleAxis(rollDegrees, axis) * Quaternion.FromToRotation(Vector3.up, axis);
        }
    }
}
