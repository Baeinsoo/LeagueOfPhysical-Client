using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 투석기 표적이 나오는 "뿅"의 시각(3차 spec §1). 화면만의 숫자다 — 판정은 발사 틱부터라 여기와 무관하다.
    /// </summary>
    public static class ArcheryCatapultSpawnPop
    {
        /// <summary>발사 몇 초 전에 자리가 드러나나. "준비!"(0.7초)보다 짧아서 어디서 나올지는 끝까지 모른다.</summary>
        public const float RevealSeconds = 0.25f;

        /// <summary>발사까지 남은 시간이 (0, <see cref="RevealSeconds"/>]이면 참. 발사 틱부터는 진짜 표적(<see cref="ArcheryTargetView"/>)이 그린다.</summary>
        public static bool IsRevealing(double tick, long launchTick, float tickInterval)
        {
            double left = (launchTick - tick) * tickInterval;
            return left > 0d && left <= RevealSeconds + 1e-6;
        }

        /// <summary>
        /// 드러나는 동안의 크기 배수(0 → 1). 처음에 빨리 크고 발사 직전에 천천히 멈춘다(시안 1 − (1 − k)³).
        /// 드러나기 전은 0, 발사부터는 1(진짜 표적과 같은 크기 — 넘어가는 순간 안 튄다).
        /// </summary>
        public static float GrowAt(double tick, long launchTick, float tickInterval)
        {
            double left = (launchTick - tick) * tickInterval;
            if (left <= 0d)
            {
                return 1f;
            }
            float k = Mathf.Clamp01(1f - (float)(left / RevealSeconds));
            float inv = 1f - k;
            return 1f - inv * inv * inv;
        }
    }
}
