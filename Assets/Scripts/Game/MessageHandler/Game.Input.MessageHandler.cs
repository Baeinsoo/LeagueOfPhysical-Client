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
            //messageDispatcher.RegisterHandler<EntityStatesToC>(OnEntityStatesToC, LOPRoomMessageInterceptor.Default);
            //messageDispatcher.RegisterHandler<InputSequnceToC>(OnInputSequnceToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            //messageDispatcher.UnregisterHandler<EntityStatesToC>(OnEntityStatesToC);
            //messageDispatcher.UnregisterHandler<InputSequnceToC>(OnInputSequnceToC);
        }
    }
}
