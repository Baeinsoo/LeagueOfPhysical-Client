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

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(room);
            builder.RegisterComponent(networkManager);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);
            builder.Register<IPlayerContext, PlayerContext>(Lifetime.Singleton);
            builder.Register<GameDataStore>(Lifetime.Singleton).As<IGameDataStore, IDataStore>();

            builder.Register<IRoomMessageHandler, GameMessageHandler>(Lifetime.Transient);

            builder.Register<IGameFactory, LOPGameFactory>(Lifetime.Singleton);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
            });
            #endregion
        }
    }
}
