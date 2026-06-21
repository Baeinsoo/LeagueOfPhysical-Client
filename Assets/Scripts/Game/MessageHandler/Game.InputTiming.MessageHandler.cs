using GameFramework;
using VContainer;

namespace LOP
{
    public class GameInputTimingMessageHandler : IGameMessageHandler
    {
        [Inject]
        private InputTimingStats inputTimingStats;

        public void Register()
        {
            EventBus.Default.Subscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

        private void OnInputTimingToC(InputTimingToC message)
        {
            inputTimingStats.Update(message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount);
        }
    }
}
