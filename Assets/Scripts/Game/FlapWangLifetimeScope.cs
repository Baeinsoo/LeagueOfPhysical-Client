using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>FlapWang 덩어리 — 캐릭터 월드와 캐릭터 HUD·게임패드를 쓴다.</summary>
    public class FlapWangLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            // LOPWorld를 구체로도 해석할 수 있어야 보정 핸들러가 자기 게임 월드를 직접 본다.
            builder.Register<LOPWorld>(Lifetime.Singleton).As<GameFramework.World.IWorld>().AsSelf();
            builder.Register<IServerCorrectionHandler, LOPServerCorrectionHandler>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);

            // 내 캐릭터만 예측한다 — 남을 밀어내는 것이 게임성이 아니라서 보간으로 충분하다.
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);

            // 이 게임엔 외삽 대상이 없다(정책이 Extrapolated를 절대 안 준다) — 그래도 EntityBinder의
            // 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            // 공통 Installer의 EntityBinder와 등록 순서가 무관하다: HUD 뷰모델이 읽는 entityId는
            // EntityCreated 발행 전에 세팅되고, actor의 유일한 소비자는 폴링이라 순서를 타지 않는다.
            builder.RegisterEntryPoint<PlayerHudCoordinator>();

            builder.Register<StatsViewModel>(Lifetime.Transient);
            builder.Register<StatsView>(Lifetime.Transient);

            builder.Register<CharacterHudViewModel>(Lifetime.Transient);
            builder.Register<CharacterHudView>(Lifetime.Transient);

            builder.Register<GamePadViewModel>(Lifetime.Transient);
            builder.Register<GamePadView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<StatsView>(() => container.Resolve<StatsView>()));
            sink.Add(windowManager.RegisterViewFactory<CharacterHudView>(() => container.Resolve<CharacterHudView>()));
            sink.Add(windowManager.RegisterViewFactory<GamePadView>(() => container.Resolve<GamePadView>()));
        }
    }
}
