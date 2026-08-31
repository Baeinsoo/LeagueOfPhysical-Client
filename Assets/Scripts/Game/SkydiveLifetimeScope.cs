using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Skydive 덩어리(클라) — 떨어지는 월드, 남도 예측하되 스냅샷 권위로 자세를 맞춘다.</summary>
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
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                // sweep이 볼 것은 맵 지오메트리뿐이다. 몸의 물리 콜라이더는 Character 레이어에
                // 있으므로(PhysicsBodyFactory), 이 마스크에 Character가 없는 한 사람끼리는 안 걸린다.
                // 사람끼리 부딪히는 것은 별도 단계로 들어온다(슬라이스 6, 스펙 §4.1).
                LayerMask.GetMask("Default")), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);

            // 남의 자세가 이제 EntitySnap(PostureAxis/Gliding/Stamina)으로 스냅샷 권위 채널을
            // 타고 온다 — 남을 예측하며 굴려도(InputBuffer가 없어 마지막 값을 유지하는 것 자체가
            // 곧 외삽) 그 "마지막 값"을 매 스냅마다 서버로 눌러 주므로 서버와 어긋나지 않는다.
            // 그래서 플레이어끼리 부딪히는 충돌(스펙 §4.1)을 위해 남도 예측 대상으로 되돌린다.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);
            builder.Register<IServerCorrectionHandler, SkydiveServerCorrectionHandler>(Lifetime.Singleton);

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
