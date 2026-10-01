using GameFramework;
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
            builder.Register<IEntranceComponent, ServerCheckComponent>(Lifetime.Transient);   // 로그인보다 먼저
            builder.Register<IEntranceComponent, LoginComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadUserComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, JoinLobbyComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadMasterDataComponent>(Lifetime.Transient);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
            });
        }
    }
}
