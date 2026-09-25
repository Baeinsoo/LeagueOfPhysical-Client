using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살을 <b>어떻게 그릴지</b>만 정하는 계산. 판정(서버·시뮬)은 이 값을 보지 않는다.
    /// </summary>
    public static class ArcheryArrowDisplay
    {
        /// <summary>과녁까지 비행 시간을 모를 때(원형 맵) 따라잡는 데 쓰는 시간(초).</summary>
        public const float DefaultCatchUpSeconds = 0.25f;

        /// <summary>
        /// 비행이 거의 끝날 때 받았어도 이만큼은 날아가는 모습을 보여 준다(초). 너무 짧으면 과녁에
        /// 순간이동한 것처럼 보이고, 길면 꽂히는 그림이 실제보다 늦는다.
        /// </summary>
        public const float MinCatchUpSeconds = 0.08f;

        /// <summary>
        /// 남의 화살은 발사 소식이 늦게 온다(서버를 거치고, 내 시계는 서버보다 앞서 간다). 받은 시각의
        /// 실제 위치부터 그리면 <b>중간쯤에서 갑자기 나타난다.</b> 그래서 받은 순간 활(0초 지점)에서
        /// 출발시키고 조금 빨리 날려, 과녁에 닿는 순간 실제 궤적과 만나게 한다.
        /// </summary>
        /// <param name="trueSeconds">쏜 뒤 실제로 흐른 시간.</param>
        /// <param name="arrivalSeconds">발사 소식을 처음 받았을 때 이미 흘러 있던 시간(늦지 않았으면 0).</param>
        /// <param name="flightSeconds">과녁까지 비행 시간. 모르면 0.</param>
        public static float VisualSeconds(float trueSeconds, float arrivalSeconds, float flightSeconds)
        {
            if (arrivalSeconds <= 0f)
            {
                return trueSeconds;
            }

            float catchUp = flightSeconds > 0f
                ? Mathf.Max(flightSeconds - arrivalSeconds, MinCatchUpSeconds)
                : DefaultCatchUpSeconds;

            if (trueSeconds >= arrivalSeconds + catchUp)
            {
                return trueSeconds;
            }
            if (trueSeconds <= arrivalSeconds)
            {
                return 0f;
            }
            return (trueSeconds - arrivalSeconds) * (arrivalSeconds + catchUp) / catchUp;
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
