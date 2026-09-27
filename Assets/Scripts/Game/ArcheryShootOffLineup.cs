using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 <b>화면용</b> 좌우 배치. 판정에서는 모두 한 점에서 쏘고, 화면에서만 내가 가운데,
    /// 남은 좌우에 서 있는 것처럼 그린다. 판정 코드는 이 값을 절대 보지 않는다.
    /// </summary>
    public static class ArcheryShootOffLineup
    {
        public const float SpacingMeters = 1.6f;

        /// <summary>남의 자리(0부터)를 사수 기준 오른쪽 거리로. 오른쪽·왼쪽을 번갈아 바깥으로 늘어선다.</summary>
        public static float SlotOffset(int othersIndex)
        {
            int step = othersIndex / 2 + 1;
            return (othersIndex % 2 == 0 ? step : -step) * SpacingMeters;
        }

        /// <summary>남의 화살이 그 캐릭터의 활에서 떠나 실제 꽂힌 점으로 모이도록, 간격을 비행 동안 줄인다.</summary>
        public static float ArrowBlend(float secondsSinceFire, float flightSeconds)
        {
            if (flightSeconds <= 0f)
            {
                return 0f;
            }
            return 1f - Mathf.Clamp01(secondsSinceFire / flightSeconds);
        }
    }
}
