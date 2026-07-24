using GameFramework;
using UnityEngine;
using VContainer;
using GameFramework.Netcode;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        [Inject]
        private LeadState leadState;

        public GameFramework.Netcode.INetworkTime networkTime;

        private readonly ClockDilator clockDilator = new ClockDilator();

        protected override void OnElapsedTimeUpdate()
        {
            // 동적 lead(LeadState)는 입력 타이밍 피드백으로 갱신됨. 주입 전(초기 프레임)엔 기본값.
            double aheadMargin = leadState != null ? leadState.AheadMargin : LeadState.DefaultMargin;
            double target = networkTime.PredictedTime + aheadMargin;
            elapsedTime = clockDilator.Advance(elapsedTime, target, Time.deltaTime);
        }
    }
}
