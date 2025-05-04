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
            builder.Register<IDataContextManager, LOPDataContextManager>(Lifetime.Singleton);

            builder.Register<UserDataContext>(Lifetime.Singleton)
                .As<IUserDataContext>()
                .As<IDataContext>()
                .AsSelf();

            builder.Register<MatchMakingDataContext>(Lifetime.Singleton)
                .As<IMatchMakingDataContext>()
                .As<IDataContext>()
                .AsSelf();

            builder.Register<RoomDataContext>(Lifetime.Singleton)
                .As<IRoomDataContext>()
                .As<IDataContext>()
                .AsSelf();
        }
    }
}
