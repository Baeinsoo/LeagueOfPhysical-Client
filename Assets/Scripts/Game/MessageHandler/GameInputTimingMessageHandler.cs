using GameFramework;
using MessagePipe;
using GameFramework.Netcode;
using GameFramework.Runner;

namespace LOP
{
    public class GameInputTimingMessageHandler : MessageHandlerBase
    {
        // lead 마진의 바닥(틱 수). 유실된 입력의 복구 사본은 다음 틱 패킷에 실려 오므로 원본보다
        // 한 틱 늦게 도착한다. 서버는 지각을 1틱도 안 봐주고 버리니(ServerInputSystem의 PruneBefore),
        // 이만큼 여유가 없으면 중복 전송이 아무것도 못 살린다.
        private const int MinMarginTicks = 1;

        private readonly IRunner runner;
        private readonly InputTimingStats inputTimingStats;
        private readonly LeadState leadState;
        private readonly ISubscriber<InputTimingToC> inputTimingSubscriber;

        public GameInputTimingMessageHandler(IRunner runner, InputTimingStats inputTimingStats, LeadState leadState, ISubscriber<InputTimingToC> inputTimingSubscriber)
        {
            this.runner = runner;
            this.inputTimingStats = inputTimingStats;
            this.leadState = leadState;
            this.inputTimingSubscriber = inputTimingSubscriber;
        }

        protected override void Subscribe() => Track(inputTimingSubscriber.Subscribe(OnInputTimingToC));

        // [진단용 임시] 실기기에서 폐기가 재현되는지 보기 위한 창별 원본. 확인 후 제거.
        private int diagFeedbackCount;

        private void OnInputTimingToC(InputTimingToC message)
        {
            inputTimingStats.Update(message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount);

            if (diagFeedbackCount < 400)
            {
                diagFeedbackCount++;
                float frameMax = runner.tickUpdater is LOPTickUpdater lopTickUpdater ? lopTickUpdater.TakeDiagMaxFrameMs() : 0f;
                UnityEngine.Debug.Log(
                    $"[InputTiming#{diagFeedbackCount}] tick={runner.tickUpdater?.tick}" +
                    $" avgD={message.AvgD:F2} maxD={message.MaxD} prune={message.PruneCount}" +
                    $" seqGap={message.SeqGapCount} n={message.SampleCount}" +
                    $" margin={leadState.AheadMargin * 1000:F0}ms frameMax={frameMax:F0}ms");
            }

            if (!leadState.Enabled)
            {
                return;
            }

            // 틱 간격은 서버가 정해 런타임에 들어오므로 바닥도 그때 환산한다.
            double interval = runner.tickUpdater?.interval ?? 0;
            if (interval <= 0)
            {
                return;
            }

            var summary = new InputTimingSummary(
                message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount, message.SampleCount);

            // 설정만 들고 있는 순수 정책 객체라 매번 만들어도 무방하다(0.5초에 1회 호출).
            var leadController = new LeadController(minMargin: interval * MinMarginTicks);
            leadState.AheadMargin = leadController.Adjust(leadState.AheadMargin, summary);
        }
    }
}
