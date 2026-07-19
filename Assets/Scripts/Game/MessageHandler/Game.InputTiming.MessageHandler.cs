using GameFramework;
using MessagePipe;
using VContainer;
using GameFramework.Netcode;

namespace LOP
{
    public class GameInputTimingMessageHandler : IGameMessageHandler
    {
        [Inject]
        private InputTimingStats inputTimingStats;

        [Inject]
        private LeadState leadState;

        [Inject]
        private ISubscriber<InputTimingToC> inputTimingSubscriber;

        private readonly LeadController leadController = new LeadController();

        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = inputTimingSubscriber.Subscribe(OnInputTimingToC);
        }

        public void Dispose()
        {
            subscription?.Dispose();
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
