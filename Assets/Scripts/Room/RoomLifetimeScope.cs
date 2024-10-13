using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : LifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private RoomNetwork roomNetwork;
        [SerializeField] private LOPNetworkManager networkManager;
        [SerializeField] private LOPGame lopGame;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(room);
            builder.RegisterComponent(roomNetwork);
            builder.RegisterComponent(networkManager);
            builder.RegisterComponent(lopGame).As<IGame>();
        }
    }
}
