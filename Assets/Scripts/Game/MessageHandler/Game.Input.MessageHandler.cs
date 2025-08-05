using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        public void Register()
        {
            EventBus.Default.Subscribe<InputSequenceToC>(nameof(IMessage), OnInputSequenceToC);
        }

        public void Unregister()
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

            LOPEntity lopEntity = gameEngine.entityManager.GetEntity<LOPEntity>(inputSequenceToC.EntityId);

            lopEntity.GetComponent<SnapReconciler>().AddServerInputSequence(inputSequence);
        }
    }
}
