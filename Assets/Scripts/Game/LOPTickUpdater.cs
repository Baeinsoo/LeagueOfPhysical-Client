using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        [Inject]
        private LeadState leadState;

        private readonly ClockDilator clockDilator = new ClockDilator();

        protected override void OnElapsedTimeUpdate()
        {
            // 동적 lead(LeadState)는 입력 타이밍 피드백으로 갱신됨. 주입 전(초기 프레임)엔 기본값.
            double aheadMargin = leadState != null ? leadState.AheadMargin : LeadState.DefaultMargin;
            double target = Runner.NetworkTime.predictedTime + aheadMargin;
            elapsedTime = clockDilator.Advance(elapsedTime, target, Time.deltaTime);
        }
    }
}
