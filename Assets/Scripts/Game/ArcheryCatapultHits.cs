using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 투석기 적중 사건 → 맞은 표적과 맞은 자리. 표적은 통신하지 않으므로 무리 번호로 목록을 다시 만들어
    /// 표적 번호로 찾고, 맞은 틱의 위치를 계산한다. HUD의 "OO +3"과 깨지는 연출이 같이 쓴다.
    /// </summary>
    public static class ArcheryCatapultHits
    {
        /// <summary>
        /// 투석기 코스가 아니거나 <c>hitTick</c>이 0 이하면 찾지 않는다 — 옛 서버는 무리·표적 번호를 0으로 보내서
        /// 그대로 찾으면 엉뚱한 표적(첫 무리 0번) 자리에 글이 뜬다.
        /// </summary>
        public static bool TryLocate(ArcheryCourse course, long startTick, float interval, ArcheryTargetHitEvent hit,
                                     List<ArcheryTarget> scratch, out ArcheryTarget target, out Vector3 position)
        {
            target = default;
            position = default;
            if (course == null || course.IsCatapult == false || hit == null || hit.hitTick <= 0
                || hit.waveIndex < 0 || hit.waveIndex >= course.StepCount
                || startTick == long.MaxValue || interval <= 0f)
            {
                return false;
            }
            course.Fill(scratch, hit.waveIndex, startTick);
            foreach (var t in scratch)
            {
                if (t.SlotIndex == hit.slotIndex)
                {
                    target = t;
                    position = ArcheryTargetMotion.PositionAt(t, hit.hitTick, interval);
                    return true;
                }
            }
            return false;
        }
    }
}
