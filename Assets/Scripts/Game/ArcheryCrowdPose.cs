using UnityEngine;

namespace LOP
{
    public enum ArcheryCrowdMood { Idle, Cheer, Gasp, Laugh, Boo, Hush, Chant }

    public readonly struct ArcheryCrowdPoseValue
    {
        public readonly float Lift;
        public readonly float Sway;
        public readonly float LeftArmDegrees;
        public readonly float RightArmDegrees;
        public readonly bool Gray;

        public ArcheryCrowdPoseValue(float lift, float sway, float leftArmDegrees, float rightArmDegrees, bool gray)
        {
            Lift = lift;
            Sway = sway;
            LeftArmDegrees = leftArmDegrees;
            RightArmDegrees = rightArmDegrees;
            Gray = gray;
        }
    }

    /// <summary>관중 한 명의 자세. 뼈대 없이 몸 높이·좌우 흔들림·팔 각도(0 = 아래, 180 = 위)만 준다. 값은 시제품 동작을 3D 크기로 옮긴 것.</summary>
    public static class ArcheryCrowdPose
    {
        public static ArcheryCrowdPoseValue At(ArcheryCrowdMood mood, float time, float phase)
        {
            switch (mood)
            {
                case ArcheryCrowdMood.Cheer:
                    return new ArcheryCrowdPoseValue(Mathf.Abs(Mathf.Sin(time * 9f + phase)) * 0.25f, 0f, 150f, 150f, false);
                case ArcheryCrowdMood.Gasp:
                    return new ArcheryCrowdPoseValue(0f, 0f, 120f, 120f, false);
                case ArcheryCrowdMood.Laugh:
                    return new ArcheryCrowdPoseValue(0f, Mathf.Sin(time * 30f + phase) * 0.04f, 20f, 140f, false);
                case ArcheryCrowdMood.Boo:
                    return new ArcheryCrowdPoseValue(-0.05f, 0f, 20f, 20f, true);
                case ArcheryCrowdMood.Hush:
                    return new ArcheryCrowdPoseValue(-0.05f, 0f, 0f, 0f, false);
                case ArcheryCrowdMood.Chant:
                    //  연호는 다 같이 — 위상을 쓰지 않는다.
                    return new ArcheryCrowdPoseValue(Mathf.Max(0f, Mathf.Sin(time * 6f)) * 0.2f, 0f, 160f, 160f, false);
                default:
                    return new ArcheryCrowdPoseValue(Mathf.Sin(time * 2f + phase) * 0.02f, 0f, 10f, 10f, false);
            }
        }
    }
}
