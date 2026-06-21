using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        // 오버워치식 ahead 마진(지터/+1프레임). predictedTime에 편도지연(RTT/2)+서버피드백 이미 포함 — 마진만 추가.
        private const double AheadMargin = 0.030;

        private readonly ClockDilator clockDilator = new ClockDilator();

        protected override void OnElapsedTimeUpdate()
        {
            double target = GameEngine.NetworkTime.predictedTime + AheadMargin;
            elapsedTime = clockDilator.Advance(elapsedTime, target, Time.deltaTime);
        }
    }
}
