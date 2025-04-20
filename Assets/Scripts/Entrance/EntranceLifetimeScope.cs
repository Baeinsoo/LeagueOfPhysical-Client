using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class EntranceLifetimeScope : SceneLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register<IEntranceComponent, LoginComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, CheckUserComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, JoinLobbyComponent>(Lifetime.Transient);
            //builder.Register<IEntranceComponent, CheckLocationComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadMasterDataComponent>(Lifetime.Transient);
        }
    }
}
