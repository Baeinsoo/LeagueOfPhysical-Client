using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGame game;

        [Inject]
        private IRoomNetwork roomNetwork;

        public void Register()
        {
            //roomNetwork.RegisterHandler<InputSequnceToC>(OnInputSequnceToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            //roomNetwork.UnregisterHandler<InputSequnceToC>(OnInputSequnceToC);
        }

        //private void OnInputSequnceToC(InputSequnceToC response)
        //{
        //    EntityManager.instance.GetEntity(PlayerInfo.entityId).GetComponent<MyTransformAdjuster>().FeedInputSequnceData(response.inputSequnceData);
        //}
    }
}
