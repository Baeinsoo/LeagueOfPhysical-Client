using UnityEngine;

namespace LOP
{
    /// <summary>시상대 한 자리 — 선수가 설 곳·방향·크기·단 번호 글자(단 위가 아니면 빈칸).</summary>
    public readonly struct PodiumSlot
    {
        public readonly Vector3 Position;
        public readonly float Yaw;
        public readonly float Scale;
        public readonly string StepLabel;
        public readonly bool OnStep;

        public PodiumSlot(Vector3 position, float yaw, float scale, string stepLabel, bool onStep)
        {
            Position = position;
            Yaw = yaw;
            Scale = scale;
            StepLabel = stepLabel;
            OnStep = onStep;
        }
    }
}
