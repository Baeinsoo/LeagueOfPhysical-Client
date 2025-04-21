using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);
        }
    }
}
