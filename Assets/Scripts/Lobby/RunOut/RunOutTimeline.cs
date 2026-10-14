using UnityEngine;

namespace LOP
{
    public readonly struct RunOutPose
    {
        public readonly float PlayerZ;
        public readonly float PlayerYaw;
        public readonly bool Running;
        public readonly bool Done;

        public RunOutPose(float playerZ, float playerYaw, bool running, bool done)
        {
            PlayerZ = playerZ; PlayerYaw = playerYaw; Running = running; Done = done;
        }
    }

    /// <summary>
    /// 출전 연출 시간표. 0~0.3초 카메라 쪽(180°)에서 출구 쪽(0°)으로 돌아서고, 0.3초부터 점점 빨라지며 달린다.
    /// 1.15초에 로딩판이 하얗게 덮기 시작하고 1.5초에 끝난다.
    /// </summary>
    public static class RunOutTimeline
    {
        public const float TurnEnd = 0.3f;
        public const float CoverStart = 1.15f;
        public const float Duration = 1.5f;
        public const float Accel = 12f;
        public const float MaxSpeed = 9f;

        public static RunOutPose Evaluate(float t, float startZ)
        {
            t = Mathf.Max(0f, t);
            float yaw = Mathf.Lerp(180f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / TurnEnd)));
            float run = Mathf.Max(0f, Mathf.Min(t, Duration) - TurnEnd);
            float accelTime = MaxSpeed / Accel;
            float dist = run <= accelTime
                ? 0.5f * Accel * run * run
                : 0.5f * Accel * accelTime * accelTime + MaxSpeed * (run - accelTime);
            return new RunOutPose(startZ + dist, yaw, t >= TurnEnd && t < Duration, t >= Duration);
        }
    }
}
