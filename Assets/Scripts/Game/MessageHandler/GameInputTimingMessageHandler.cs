using GameFramework;
using MessagePipe;
using VContainer;
using GameFramework.Netcode;

namespace LOP
{
    public class GameInputTimingMessageHandler : MessageHandlerBase
    {
        [Inject]
        private InputTimingStats inputTimingStats;

        [Inject]
        private LeadState leadState;

        [Inject]
        private ISubscriber<InputTimingToC> inputTimingSubscriber;

        private readonly LeadController leadController = new LeadController();

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
