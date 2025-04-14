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
            builder.Register<IDataContext, UserDataContext>(Lifetime.Transient);
            builder.Register<IDataContext, MatchMakingDataContext>(Lifetime.Transient);
            builder.Register<IDataContext, RoomDataContext>(Lifetime.Transient);
            builder.Register<IDataContext, GameDataContext>(Lifetime.Transient);
        }
    }
}
