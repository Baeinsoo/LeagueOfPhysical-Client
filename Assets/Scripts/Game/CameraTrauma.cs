using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 충돌 흔들림의 "트라우마"(spec §3, 시제품 <c>trauma</c>): 부딪힐 때마다 쌓이고(최대 1) 초당 1.4씩 빠진다.
    /// 흔들림 세기는 트라우마의 <b>제곱</b> — 막 부딪힌 순간만 크게 흔들리고 남은 꼬리는 금방 잦아든다.
    /// </summary>
    public sealed class CameraTrauma
    {
        public const float DecayPerSecond = 1.4f;

        public float Value { get; private set; }

        public void Add(float amount)
        {
            Value = Mathf.Min(1f, Value + Mathf.Max(0f, amount));
        }

        public void Decay(float deltaTime)
        {
            Value = Mathf.Max(0f, Value - DecayPerSecond * Mathf.Max(0f, deltaTime));
        }

        /// <summary>지금 흔들림 크기 = 트라우마² × max.</summary>
        public float Offset(float max) => Value * Value * max;
    }
}
