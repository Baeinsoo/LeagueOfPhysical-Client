using GameFramework;
using MessagePipe;
using GameFramework.Netcode;

namespace LOP
{
    public class GameInputTimingMessageHandler : MessageHandlerBase
    {
        private readonly InputTimingStats inputTimingStats;
        private readonly LeadState leadState;
        private readonly ISubscriber<InputTimingToC> inputTimingSubscriber;

        private readonly LeadController leadController = new LeadController();

        public GameInputTimingMessageHandler(InputTimingStats inputTimingStats, LeadState leadState, ISubscriber<InputTimingToC> inputTimingSubscriber)
        {
            this.inputTimingStats = inputTimingStats;
            this.leadState = leadState;
            this.inputTimingSubscriber = inputTimingSubscriber;
        }

        protected override void Subscribe() => Track(inputTimingSubscriber.Subscribe(OnInputTimingToC));

        private void OnInputTimingToC(InputTimingToC message)
        {
            inputTimingStats.Update(message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount);

            if (leadState.Enabled)
            {
                var summary = new InputTimingSummary(
                    message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount, message.SampleCount);
                leadState.AheadMargin = leadController.Adjust(leadState.AheadMargin, summary);
            }
        }
    }
}
