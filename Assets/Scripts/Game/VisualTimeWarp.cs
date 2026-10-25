using UnityEngine;

namespace LOP
{
    /// <summary>
    /// "그림만" 시간 늦추기(spec §3 멈칫 0.06초·결승 0.4배 0.25초). 시뮬·판정·서버는 그대로 가고, 내 새 그림(과 그걸 따라가는
    /// 카메라)을 잠깐 늦게 그렸다가 2배속으로 따라잡는다. <see cref="Delay"/> = 지금 그림이 시뮬보다 몇 초 늦나.
    /// <para>시제품은 세상 시계를 멈췄지만 여기선 못 한다 — 시계를 멈추면 내 입력·예측이 서버와 어긋난다(판정 무관이어야 한다).</para>
    /// </summary>
    public sealed class VisualTimeWarp
    {
        /// <summary>따라잡는 빠르기: 지연 1초를 1초에 갚는다 = 그림이 2배속으로 흐른다.</summary>
        public const float CatchUpRate = 1f;

        /// <summary>최대 지연. 예측 보간기가 쥔 샘플이 20개(50 Hz면 0.4초)라 넉넉히 그 반 아래로.</summary>
        public const float MaxDelay = 0.2f;

        private float hold;
        private float slow;
        private float slowRate = 1f;

        public float Delay { get; private set; }

        /// <summary>이만큼 그림을 세운다. 이미 서 있으면 더 긴 쪽.</summary>
        public void Hitstop(float seconds)
        {
            hold = Mathf.Max(hold, seconds);
        }

        /// <summary>이만큼 그림을 rate배로 흘린다.</summary>
        public void Slow(float seconds, float rate)
        {
            slow = Mathf.Max(slow, seconds);
            slowRate = Mathf.Clamp01(rate);
        }

        /// <summary>실제로 deltaTime이 흘렀다. 새 지연을 돌려준다.</summary>
        public float Advance(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            if (hold > 0f && dt > 0f)
            {
                float used = Mathf.Min(dt, hold);
                hold -= used;
                Delay += used;                       //  그림 시각이 서 있다
                dt -= used;
            }
            if (slow > 0f && dt > 0f)
            {
                float used = Mathf.Min(dt, slow);
                slow -= used;
                Delay += used * (1f - slowRate);     //  그림이 rate배로 흐른다
                dt -= used;
            }
            if (dt > 0f && hold <= 0f && slow <= 0f)
            {
                Delay = Mathf.Max(0f, Delay - dt * CatchUpRate);
            }
            Delay = Mathf.Min(Delay, MaxDelay);
            return Delay;
        }
    }
}
