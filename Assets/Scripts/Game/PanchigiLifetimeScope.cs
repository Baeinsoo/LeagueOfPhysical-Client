using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>판치기 덩어리(클라) — 빈 월드, 예측 없음, 판을 비추는 카메라.</summary>
    public class PanchigiLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;
        [SerializeField] private PanchigiStrikeInput strikeInput;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);
            builder.RegisterComponent(strikeInput);

            builder.Register<GameFramework.World.IWorld>(c => new PanchigiWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
            builder.Register<ICharacterCreator, PanchigiPlayerCreator>(Lifetime.Singleton);
            builder.Register<IEntitySyncPolicy, AllInterpolatedSyncPolicy>(Lifetime.Singleton);
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);
        }
    }
}
