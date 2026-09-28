using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// Dodge 덩어리(클라) — 플랩왕의 캐릭터 월드를 그대로 쓰고, 조작은 이동 하나다.
    /// 위험은 서버가 보낸 패턴을 같은 식으로 그리기만 한다(판정은 서버).
    /// </summary>
    public class DodgeLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            // FlapWangLifetimeScope와 같은 월드 배선 — 보정 핸들러가 구체 LOPWorld를 직접 본다.
            builder.Register<LOPWorld>(Lifetime.Singleton).As<GameFramework.World.IWorld>().AsSelf();
            builder.Register<IServerCorrectionHandler, LOPServerCorrectionHandler>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);

            // 내 몸만 예측한다 — 남을 밀어내는 것이 게임성이 아니라 보간으로 충분하다(스펙 §5.3).
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);

            // 외삽 대상이 없어 값은 안 쓰이지만 EntityBinder의 생성자 의존이라 등록은 필요하다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            builder.Register<DodgeConfigProvider>(Lifetime.Singleton);
            builder.Register<DodgeConfig>(c => c.Resolve<DodgeConfigProvider>().Get(), Lifetime.Singleton);
            builder.Register<DodgeClientState>(Lifetime.Singleton);
            builder.Register<DodgeStageProvider>(Lifetime.Singleton);
            builder.Register<DodgeStageTable>(c => c.Resolve<DodgeStageProvider>().Get(), Lifetime.Singleton);
            builder.RegisterEntryPoint<DodgeStateHandler>();
            // 판정과 같은 식에 내 몸이 그려지는 틱을 넣어 그린다(스펙 §5.3).
            builder.RegisterEntryPoint<DodgeHazardView>().AsSelf();

            builder.RegisterEntryPoint<DodgeHudCoordinator>();
            builder.Register<DodgePadViewModel>(Lifetime.Transient);
            builder.Register<DodgePadView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<DodgePadView>(() => container.Resolve<DodgePadView>()));
        }
    }
}
