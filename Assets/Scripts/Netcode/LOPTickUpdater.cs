using GameFramework;
using GameFramework.Runner;
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

        //  이만큼 넘게 밀리면 갈아서 따라잡지 않고 번호만 옮긴다(32틱 = 0.64초).
        //  아래로는 프레임 히칭 정도라 갈아도 눈에 안 띄고(8틱/프레임이면 32틱은 4프레임에 소화),
        //  위로는 시계 보정이나 긴 멈춤이라 계산해 봐야 서버 스냅샷이 덮을 예측일 뿐이다.
        //  클라만 켠다 — 서버는 자기가 권위라 건너뛰면 그 구간 입력을 아무도 안 고쳐준다.
        protected override long MaxCatchUpTicks => 32;

        /// <summary>
        /// 이 시계가 수렴해 갈 목표 시각 — 서버 추정 시각에 앞서갈 여유를 더한 값.
        /// 매치 시작 시드도 여기서 가져간다(같은 식을 두 곳에 두면 어긋난다).
        /// </summary>
        public double TargetTime
        {
            get
            {
                // 동적 lead(LeadState)는 입력 타이밍 피드백으로 갱신됨. 주입 전(초기 프레임)엔 기본값.
                double aheadMargin = leadState != null ? leadState.AheadMargin : LeadState.DefaultMargin;
                return networkTime.PredictedTime + aheadMargin;
            }
        }

        protected override void OnElapsedTimeUpdate()
        {
            elapsedTime = clockDilator.Advance(elapsedTime, TargetTime, Time.deltaTime);
        }
    }
}
