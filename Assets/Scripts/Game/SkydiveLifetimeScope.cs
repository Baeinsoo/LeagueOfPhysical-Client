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
            // 맵 씬의 WindVolume 마커가 맵 로드 시 여기에 자기를 넣는다.
            builder.Register<WindField>(Lifetime.Singleton);
            builder.Register<WindDriftSystem>(Lifetime.Singleton);
            //  아래로 떨어지므로 y가 작아지는 방향이다. 마커가 없는 맵을 위해 지면 높이를 폴백으로 준다.
            builder.Register(c => new FinishLineBounds(
                FinishAxis.Y, c.Resolve<SkydiveConfig>().GroundY), Lifetime.Singleton);
            builder.Register(c => new FinishSystem(
                c.Resolve<FinishLineBounds>(), FinishAxis.Y, increasing: false), Lifetime.Singleton);

            // 맵 씬의 LaserVolume 마커가 맵 로드 시 여기에 자기를 넣는다. 클라는 레이저를 판정하지
            // 않지만, 마커의 [Inject]가 이걸 요구하므로 등록이 없으면 씬 주입이 그 자리에서 끊긴다.
            builder.Register<LaserField>(Lifetime.Singleton);
            builder.Register<SkydiveWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<WindDriftSystem>(),
                c.Resolve<FinishSystem>(),
                c.Resolve<WindField>(),
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                // sweep이 볼 것은 맵 지오메트리뿐이다. 몸의 물리 콜라이더는 Character 레이어에
                // 있으므로(PhysicsBodyFactory), 이 마스크에 Character가 없는 한 사람끼리는 안 걸린다.
                // 사람끼리 부딪히는 것은 별도 단계로 들어온다(슬라이스 6, 스펙 §4.1).
                LayerMask.GetMask("Default")), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            //  레이저를 그린다. 판정과 같은 식에 같은 틱을 넣으므로 그림과 판정이 어긋나지 않는다.
            builder.RegisterEntryPoint<SkydiveLaserView>().AsSelf();

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
            builder.RegisterEntryPoint<SkydiveAtmosphere>();
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
