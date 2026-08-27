using UnityEngine;

namespace LOP
{
    /// <summary>Flappy Race의 중력을 외삽 가속도로 공급한다(아래 방향 — 새는 계속 떨어진다).</summary>
    public class FlappyExtrapolationAcceleration : IExtrapolationAcceleration
    {
        private readonly FlappyConfig config;

        public FlappyExtrapolationAcceleration(FlappyConfig config)
        {
            this.config = config;
        }

        public Vector3 Acceleration => new Vector3(0f, -config.Gravity, 0f);
    }
}
