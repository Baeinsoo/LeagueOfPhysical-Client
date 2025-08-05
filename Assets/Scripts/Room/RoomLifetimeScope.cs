using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : SceneLifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private LOPNetworkManager networkManager;
        [SerializeField] private LOPGame game;
        [SerializeField] private LOPGameEngine gameEngine;
        [SerializeField] private CameraController cameraController;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(room);
            builder.RegisterComponent(networkManager);
            builder.RegisterComponent(game).As<IGame>();
            builder.RegisterComponent(gameEngine).As<IGameEngine>();
            builder.RegisterComponent(cameraController);

            builder.Register<IRoomMessageHandler, GameMessageHandler>(Lifetime.Transient);
            
            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);

            builder.Register<IMessageDispatcher, LOPMessageDispatcher>(Lifetime.Singleton);

            builder.Register<IPlayerContext, PlayerContext>(Lifetime.Singleton);

            builder.Register<GameDataStore>(Lifetime.Singleton).As<IGameDataStore, IDataStore>();

            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();

            builder.Register<IActionManager, LOPActionManager>(Lifetime.Singleton);
            builder.Register<IMovementManager, LOPMovementManager>(Lifetime.Singleton);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<IMessageDispatcher>();
            });
            #endregion
        }
    }
}
