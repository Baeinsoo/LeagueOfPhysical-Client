using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : SceneLifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private RoomNetwork roomNetwork;
        [SerializeField] private LOPNetworkManager networkManager;
        [SerializeField] private LOPGame game;
        [SerializeField] private LOPGameEngine gameEngine;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(room);
            builder.RegisterComponent(roomNetwork).As<IRoomNetwork>();
            builder.RegisterComponent(networkManager);
            builder.RegisterComponent(game).As<IGame>();
            builder.RegisterComponent(gameEngine).As<IGameEngine>();

            builder.Register<IRoomMessageHandler, GameMessageHandler>(Lifetime.Transient);
        }
    }
}
