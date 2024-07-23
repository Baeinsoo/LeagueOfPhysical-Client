using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class EntranceLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IEntranceComponent, LoginComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, CheckUserComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, JoinLobbyComponent>(Lifetime.Transient);
            //builder.Register<IEntranceComponent, CheckLocationComponent>(Lifetime.Transient);
        }
    }
}
