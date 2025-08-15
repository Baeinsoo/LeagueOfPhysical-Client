using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMasterDataManager, LOPMasterDataManager>(Lifetime.Singleton);

            builder.Register<UserDataStore>(Lifetime.Singleton)
                .As<IUserDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<MatchMakingDataStore>(Lifetime.Singleton)
                .As<IMatchMakingDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<RoomDataStore>(Lifetime.Singleton)
                .As<IRoomDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<RoomConnector>(Lifetime.Transient);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
            });
            #endregion
        }
    }
}
