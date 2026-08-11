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

        protected override void OnElapsedTimeUpdate()
        {
            double target = TargetTime;
            double before = elapsedTime;
            elapsedTime = clockDilator.Advance(before, target, Time.deltaTime);
            TraceClock(before, target);
        }

        // [진단용 임시] 시작 구간에 시계가 왜 앞서는지 가르려고 둔다. 덤프 한 장으로는
        // "시드가 틀렸나 / 타깃이 움직였나 / 수렴이 느린가"를 못 나눈다 — 시드 직후부터
        // 초 단위로 따라가야 갈린다. 원인이 확정되면 이 블록은 통째로 지운다.
        private const double TraceDuration = 25;

        private double traceStartRealtime = -1;
        private int lastTracedSecond = -1;

        /// <summary>매치 시드 직후 호출. 이 시점부터 25초를 1초 간격으로 남긴다.</summary>
        public void BeginClockTrace()
        {
            traceStartRealtime = Time.realtimeSinceStartupAsDouble;
            lastTracedSecond = -1;
        }

        private void TraceClock(double before, double target)
        {
            if (traceStartRealtime < 0)
            {
                return;
            }

            double sinceSeed = Time.realtimeSinceStartupAsDouble - traceStartRealtime;
            if (sinceSeed > TraceDuration)
            {
                traceStartRealtime = -1;
                return;
            }

            int second = (int)sinceSeed;
            if (second == lastTracedSecond)
            {
                return;
            }
            lastTracedSecond = second;

            // error가 양수 = 시계가 타깃보다 앞서 있음(내려와야 함). ClockDilator는 이걸
            // 최대 ±5%로만 줄이므로, 1초 어긋나면 수렴에 20초가 걸린다.
            double error = before - target;
            long serverTickEstimate = (long)(networkTime.ServerNow / interval);
            Debug.Log(
                $"[ClockTrace] t={sinceSeed:F1} tick={tick} elapsed={before:F3} target={target:F3}" +
                $" error={error * 1000:F0}ms lead={tick - serverTickEstimate}" +
                $" rtt={networkTime.Rtt * 1000:F0} margin={(leadState != null ? leadState.AheadMargin : LeadState.DefaultMargin) * 1000:F0}" +
                $" predicted={networkTime.PredictedTime:F3} serverNow={networkTime.ServerNow:F3}");
        }
    }
}
