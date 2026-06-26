using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IRunner runner;

        public void Initialize()
        {
            EventBus.Default.Subscribe<InputSequenceToC>(nameof(IMessage), OnInputSequenceToC);
        }

        public void Dispose()
        {
            EventBus.Default.Unsubscribe<InputSequenceToC>(nameof(IMessage), OnInputSequenceToC);
        }

        private void OnInputSequenceToC(InputSequenceToC inputSequenceToC)
        {
            InputSequence inputSequence = new InputSequence
            {
                Tick = inputSequenceToC.InputSequence.Tick,
                Sequence = inputSequenceToC.InputSequence.Sequence,
            };

            LOPEntity lopEntity = runner.entityManager.GetEntity<LOPEntity>(inputSequenceToC.EntityId);

            lopEntity.GetComponent<SnapReconciler>().AddServerInputSequence(inputSequence);
        }
    }
}
