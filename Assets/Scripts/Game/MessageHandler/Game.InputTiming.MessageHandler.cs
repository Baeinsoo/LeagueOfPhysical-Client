using GameFramework;
using VContainer;

namespace LOP
{
    public class GameInputTimingMessageHandler : IGameMessageHandler
    {
        [Inject]
        private InputTimingStats inputTimingStats;

        [Inject]
        private LeadState leadState;

        private readonly LeadController leadController = new LeadController();

        public void Initialize()
        {
            EventBus.Default.Subscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

        public void Dispose()
        {
            EventBus.Default.Unsubscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

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
