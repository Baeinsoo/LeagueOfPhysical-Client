using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private IMessageDispatcher messageDispatcher;

        public void Register()
        {
            messageDispatcher.RegisterHandler<InputSequenceToC>(OnInputSequenceToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<InputSequenceToC>(OnInputSequenceToC);
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
