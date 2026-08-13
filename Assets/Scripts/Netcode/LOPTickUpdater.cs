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
        private float diagMaxFrameMs;

        public float TakeDiagMaxFrameMs()
        {
            float value = diagMaxFrameMs;
            diagMaxFrameMs = 0f;
            return value;
        }

        // [진단용 임시] 폐기는 첫 1초에 몰리는데 피드백은 0.3초에 한 번뿐이라 뭉개진다.
        // 시작 구간은 프레임 단위로, 그 뒤로는 튄 프레임만 남긴다.
        private int diagTraceFrames;
        private int diagGcBaseline = -1;

        /// <summary>[진단용 임시] 이번 프레임에 우리 시뮬이 쓴 시간(ms). LOPRunner가 틱마다 더한다.</summary>
        public float diagSimMs;

        protected override void OnElapsedTimeUpdate()
        {
            float frameMs = Time.deltaTime * 1000f;
            diagMaxFrameMs = Mathf.Max(diagMaxFrameMs, frameMs);

            // 메모리 정리가 프레임을 먹었는지는 세대 0 수거 횟수의 증가로 드러난다.
            int gcNow = System.GC.CollectionCount(0);
            int gcDelta = diagGcBaseline < 0 ? 0 : gcNow - diagGcBaseline;
            diagGcBaseline = gcNow;

            if (diagTraceFrames < 120 || frameMs > 40f)
            {
                diagTraceFrames++;
                Debug.Log(
                    $"[ClockTrace#{diagTraceFrames}] tick={tick}" +
                    $" drift={(networkTime.PredictedTime - Time.unscaledTimeAsDouble) * 1000:F0}ms" +
                    $" rtt={networkTime.Rtt * 1000:F0}ms frame={frameMs:F0}ms" +
                    $" sim={diagSimMs:F1}ms gc={gcDelta}");
            }

            diagSimMs = 0f;

            elapsedTime = clockDilator.Advance(elapsedTime, TargetTime, Time.deltaTime);
        }
    }
}
