using UnityEngine;

namespace LOP
{
    /// <summary>달리기 애니를 켤지. 몸이 "발밑 땅에 대해" 움직이고 땅에 서 있을 때만 — 도는 판 위에 서 있기만 하면 안 달린다.</summary>
    public static class RunAnimation
    {
        private const float WalkThreshold = 0.01f;

        public static bool ShouldRun(Vector3 velocity, Vector3 groundVelocity, bool grounded)
        {
            float x = velocity.x - groundVelocity.x;
            float z = velocity.z - groundVelocity.z;
            return grounded && x * x + z * z > WalkThreshold * WalkThreshold;
        }
    }
}
