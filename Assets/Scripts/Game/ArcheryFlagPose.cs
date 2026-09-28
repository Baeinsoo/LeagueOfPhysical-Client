using UnityEngine;

namespace LOP
{
    public readonly struct ArcheryFlagPoseValue
    {
        /// <summary>천이 향하는 쪽: +1 = 사수 기준 오른쪽, −1 = 왼쪽, 0 = 처짐.</summary>
        public readonly int Side;
        /// <summary>0 = 막대에 늘어짐, 1 = 옆으로 다 펴짐.</summary>
        public readonly float Extend;
        public readonly float FlapDegrees;

        public ArcheryFlagPoseValue(int side, float extend, float flapDegrees)
        {
            Side = side;
            Extend = extend;
            FlapDegrees = flapDegrees;
        }
    }

    /// <summary>관중석 깃발이 바람(m/s², 양수 = 사수 기준 오른쪽)을 보여 준다 — 관중이 풍향계다.</summary>
    public static class ArcheryFlagPose
    {
        public const float CalmBelow = 0.3f;
        public const float FullAt = 5f;

        public static ArcheryFlagPoseValue At(float wind, float time, float phase)
        {
            float strength = Mathf.Abs(wind);
            if (strength < CalmBelow)
            {
                return new ArcheryFlagPoseValue(0, 0f, 0f);
            }
            float extend = Mathf.Clamp01(strength / FullAt);
            float flap = Mathf.Sin(time * (6f + strength) + phase) * 15f * extend;
            return new ArcheryFlagPoseValue(wind > 0f ? 1 : -1, extend, flap);
        }

        /// <summary>
        /// 천의 로컬 회전. 기준 좌표는 사수를 보는 깃발 뿌리(로컬 +X = 사수 기준 왼쪽, +Z = 사수 쪽)이고 천은 +X로 뻗는다.
        /// 오른쪽 바람이면 Y로 180° 돌려 좌우를 바꾸고, 약할수록 Z축으로 더 내려 늘어뜨린다(0~90°라 뒤집히지 않는다).
        /// 무풍은 늘어진 채 Y로 90° 더 돌려 사수 쪽에서 옆면만 보이게 한다 — 어느 쪽으로도 안 읽힌다.
        /// </summary>
        public static Quaternion LocalRotation(ArcheryFlagPoseValue pose)
        {
            if (pose.Side == 0)
            {
                return Quaternion.AngleAxis(90f, Vector3.up) * Quaternion.AngleAxis(-90f, Vector3.forward);
            }
            float droop = (1f - Mathf.Max(0.35f, pose.Extend)) * 90f;
            Quaternion mirror = pose.Side > 0 ? Quaternion.AngleAxis(180f, Vector3.up) : Quaternion.identity;
            return mirror * Quaternion.AngleAxis(-droop, Vector3.forward) * Quaternion.AngleAxis(pose.FlapDegrees, Vector3.right);
        }
    }
}
