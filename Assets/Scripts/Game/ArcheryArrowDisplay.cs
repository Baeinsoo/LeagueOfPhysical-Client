using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살을 <b>어떻게 그릴지</b>만 정하는 계산. 판정(서버·시뮬)은 이 값을 보지 않는다.
    /// </summary>
    public static class ArcheryArrowDisplay
    {
        /// <summary>
        /// 꼬리선의 끝이 궤적의 몇 초 지점에 있나. 화살은 늘 <b>실제 시각</b>에 그리고, 꼬리는 그보다
        /// <paramref name="trailSeconds"/>만큼 뒤에서 따라온다.
        ///
        /// <para>남의 화살은 발사 소식이 늦게 와서 비행 중간에 처음 보인다 — 그 순간 꼬리를 <b>활(0초)부터</b>
        /// 그려 순간이동이 아니라 날아온 것처럼 보이게 하고, 그 긴 꼬리는 <paramref name="trailSeconds"/>에
        /// 걸쳐 보통 길이로 줄인다. 꼬리 끝은 앞으로만 간다.</para>
        /// </summary>
        /// <param name="seconds">쏜 뒤 실제로 흐른 시간.</param>
        /// <param name="firstSeenSeconds">화살을 처음 본 순간 이미 흘러 있던 시간(제때 봤으면 0에 가깝다).</param>
        public static float TrailTailSeconds(float seconds, float firstSeenSeconds, float trailSeconds)
        {
            if (trailSeconds <= 0f)
            {
                return seconds;
            }

            //  늦게 봐서 생긴 긴 꼬리의 남은 몫 — 처음 본 순간 가득, trailSeconds 뒤 0.
            float extra = Mathf.Max(0f, firstSeenSeconds - trailSeconds)
                        * Mathf.Clamp01(1f - (seconds - firstSeenSeconds) / trailSeconds);
            return Mathf.Clamp(seconds - trailSeconds - extra, 0f, seconds);
        }

        /// <summary>
        /// 서버가 "맞았다"고 알려 온 화살을 숨길까. <b>맞으면 사라지는 과녁</b>(원형 맵)이면 화살도 같이
        /// 치운다. 사거리·한 발 승부처럼 <b>과녁이 남는</b> 모드에서는 꽂힌 채 남겨야 한다 — 거기선 여러
        /// 발(한 발 승부는 모두의 화살)이 한 과녁에 꽂혀 있는 것 자체가 보여 줄 내용이다.
        /// 클라가 꽂힌 자리를 계산하지 못한 화살은(드물다) 날아가는 그림이 거짓이 되므로 숨긴다.
        /// </summary>
        public static bool HideConfirmedHit(bool confirmedHit, bool hasImpact, bool targetConsumedOnHit)
        {
            return confirmedHit && (hasImpact == false || targetConsumedOnHit);
        }
    }
}
