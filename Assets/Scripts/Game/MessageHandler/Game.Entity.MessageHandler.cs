using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameEntityMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGame game;

        [Inject]
        private IRoomNetwork roomNetwork;

        public void Register()
        {
            //roomNetwork.RegisterHandler<EntityStatesToC>(OnEntityStatesToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            //roomNetwork.UnregisterHandler<EntityStatesToC>(OnEntityStatesToC);
        }

        //private void OnEntityStatesToC(EntityStatesToC response)
        //{
        //    foreach (var entityState in response.entityStates ?? Enumerable.Empty<EntityState>())
        //    {
        //        var entity = EntityManager.instance.GetEntity(entityState.entityId);

        //        if (entity.GetComponent<TransformAdjuster>() == null)
        //        {
        //            entity.GetComponent<MyTransformAdjuster>().FeedEntityState(entityState);
        //        }
        //        else
        //        {
        //            entity.GetComponent<TransformAdjuster>().FeedEntityState(entityState);
        //        }
        //    }
        //}
    }
}
