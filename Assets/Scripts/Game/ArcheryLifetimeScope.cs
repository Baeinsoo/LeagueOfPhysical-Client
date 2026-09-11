using System;
using System.Collections.Generic;
using LOP.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Archery 덩어리(클라) — 제자리에 선 사수들, 남도 굴려서 남의 화살도 보인다.</summary>
    public class ArcheryLifetimeScope : GameLifetimeScope
    {
        private const float TickInterval = 0.02f;

        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<ArcheryAimSystem>(Lifetime.Singleton);
            builder.Register<ArcheryWorld>(c => new ArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, ArcheryPlayerCreator>(Lifetime.Singleton);

            // 남도 굴린다 — 서버가 되뿌린 남의 입력이 남의 활을 같은 규칙으로 당기게 한다.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);

            // 스냅에서 위치 말고 맞춰야 할 게 없다(판치기와 같은 사정) — 그래도 EntityBinder가 굴리는
            // Reconciler의 생성자 의존이라 등록은 필요하다.
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);

            // 이 게임엔 외삽 대상이 없다(정책이 Extrapolated를 절대 안 준다) — 그래도 EntityBinder의
            // 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            builder.RegisterEntryPoint<ArcheryAimView>().AsSelf();
            builder.RegisterEntryPoint<ArcheryArrowView>().AsSelf();
            builder.RegisterEntryPoint<ArcheryRemoteShotHandler>();

            builder.RegisterEntryPoint<ArcheryHudCoordinator>();
            builder.Register<ArcheryPadViewModel>(Lifetime.Transient);
            builder.Register<ArcheryPadView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<ArcheryPadView>(
                () => container.Resolve<ArcheryPadView>()));
        }
    }
}
