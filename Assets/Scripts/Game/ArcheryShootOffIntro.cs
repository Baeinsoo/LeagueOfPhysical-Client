using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 라운드 소개 카드. 결과 화면이 닫히는 틱(= 자리가 바뀌는 틱)을 화면 전체로 덮어,
    /// 자리 바뀜(순간이동)이 카드 뒤에서 일어나게 한다. 과녁이 서기(60틱 뒤) 전에 걷힌다.
    /// </summary>
    public static class ArcheryShootOffIntro
    {
        private const float FadeInTicks = 8f;
        private const float HoldTicks = 35f;
        private const float FadeOutTicks = 15f;

        /// <summary>자리가 바뀌는 틱 <paramref name="changeTick"/> 기준으로 지금 카드가 얼마나 덮나(0~1).</summary>
        public static float AlphaAt(double renderTick, long changeTick)
        {
            float t = (float)(renderTick - changeTick);
            if (t < -FadeInTicks || t >= HoldTicks + FadeOutTicks)
            {
                return 0f;
            }
            if (t < 0f)
            {
                return 1f + t / FadeInTicks;
            }
            if (t <= HoldTicks)
            {
                return 1f;
            }
            return 1f - (t - HoldTicks) / FadeOutTicks;
        }

        /// <summary>지금 카드가 보이는 라운드. 없으면 −1.</summary>
        public static int ActiveRound(double renderTick, IReadOnlyList<long> changeTicks)
        {
            for (int i = 0; i < changeTicks.Count; i++)
            {
                if (AlphaAt(renderTick, changeTicks[i]) > 0f)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary><paramref name="from"/>에서 <paramref name="to"/>를 보는 유니티 요(도).</summary>
        public static float YawToward(Vector3 from, Vector3 to)
        {
            return Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        }
    }
}
