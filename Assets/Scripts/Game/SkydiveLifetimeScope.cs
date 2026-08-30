using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Skydive 덩어리(클라) — 떨어지는 월드, 내 몸만 예측하고 남은 보간.</summary>
    public class SkydiveLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<SkydiveConfigProvider>(Lifetime.Singleton);
            builder.Register<SkydiveConfig>(c => c.Resolve<SkydiveConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<StaminaSystem>(Lifetime.Singleton);
            builder.Register<SkydiveWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<SkydiveConfig>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);

            // 남의 자세를 실어 오는 권위 채널이 아직 없다 — 남을 예측하면 InputBuffer가 없어
            // ApplyPostureInput이 조기 반환해 영원히 대자로 굴러간다. 플레이어끼리 부딪히는
            // 충돌(스펙 §4.1)을 켜려면 자세를 스냅샷 권위로 올리는 게 먼저다. 그때까지 내 몸만
            // 예측하고 남은 보간한다.
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);

            // 이 게임엔 외삽 대상이 없다(정책이 Extrapolated를 절대 안 준다) — 그래도 EntityBinder의
            // 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            builder.RegisterEntryPoint<SkydiveHudCoordinator>();
            builder.Register<LOP.UI.SkydivePadViewModel>(Lifetime.Transient);
            builder.Register<LOP.UI.SkydivePadView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<LOP.UI.SkydivePadView>(
                () => container.Resolve<LOP.UI.SkydivePadView>()));
        }
    }
}
