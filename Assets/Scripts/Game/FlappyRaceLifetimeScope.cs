using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Flappy Race 덩어리 — 새 월드와 새 생성기를 쓴다. 게임 UI는 다음 슬라이스.</summary>
    public class FlappyRaceLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);
            builder.Register<FlappyConfigProvider>(Lifetime.Singleton);
            builder.Register<FlappyConfig>(c => c.Resolve<FlappyConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register<GameFramework.World.IWorld, FlappyWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);
        }
    }
}
