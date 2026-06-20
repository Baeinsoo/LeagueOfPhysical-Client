using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        private readonly ClockDilator clockDilator = new ClockDilator();

        protected override void OnElapsedTimeUpdate()
        {
            // 2a: 타깃은 현행 NetworkTime.time 유지(lead 없음). 메커니즘만 rate dilation으로 전환.
            //     (2b에서 predictedTime + aheadMargin으로 flip)
            double target = Mirror.NetworkTime.time;
            elapsedTime = clockDilator.Advance(elapsedTime, target, Time.deltaTime);
        }
    }
}
