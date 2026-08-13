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

        // [진단용 임시] 측정 창 안에서 가장 오래 걸린 프레임(ms). 읽는 쪽이 가져가면서 비운다.
        // 이 루프는 프레임당 정확히 한 번 돈다.
        private float diagMaxFrameMs;

        public float TakeDiagMaxFrameMs()
        {
            float value = diagMaxFrameMs;
            diagMaxFrameMs = 0f;
            return value;
        }

        protected override void OnElapsedTimeUpdate()
        {
            diagMaxFrameMs = Mathf.Max(diagMaxFrameMs, Time.deltaTime * 1000f);

            elapsedTime = clockDilator.Advance(elapsedTime, TargetTime, Time.deltaTime);
        }
    }
}
