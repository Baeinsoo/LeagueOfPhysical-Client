using LOP.UI;
using MessagePipe;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 삼색 그랩 덩어리(클라) — 피하기와 같은 캐릭터 월드에 걷기 하나(이 모드만 7 m/s). 카드 정체·손·두더지는 서버 상태,
    /// 카드 자리는 서버가 보낸 자리를 원격 몸과 같은 시각으로 보간해 그린다(판정은 서버). 내 손만 쏜 순간 미리 그린다.
    /// </summary>
    public class TriGrabLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        // 이 모드는 점수다 — 체력 바는 헷갈린다.
        protected override bool ShowHealthBars => false;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            // DodgeLifetimeScope와 같은 월드 배선 — 보정 핸들러가 구체 LOPWorld를 직접 본다.
            builder.Register<LOPWorld>(Lifetime.Singleton).As<GameFramework.World.IWorld>().AsSelf();
            builder.Register<IServerCorrectionHandler, LOPServerCorrectionHandler>(Lifetime.Singleton);
            builder.Register<CharacterCreator>(Lifetime.Singleton);
            builder.Register<ICharacterCreator>(c => new TriGrabCharacterCreator(
                c.Resolve<CharacterCreator>(), c.Resolve<GameFramework.World.EntityRegistry>()), Lifetime.Singleton);

            // 내 몸만 예측한다 — 남은 보간으로 충분하다(스펙 §6).
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);
            // 외삽 대상이 없어 값은 안 쓰이지만 EntityBinder의 생성자 의존이라 등록은 필요하다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            builder.Register<TriGrabClientState>(Lifetime.Singleton);
            builder.Register<TriGrabCardTrack>(Lifetime.Singleton);
            builder.Register<TriGrabMyHook>(Lifetime.Singleton);
            builder.Register<TriGrabAim>(Lifetime.Singleton);
            builder.RegisterEntryPoint<TriGrabStateHandler>();
            builder.RegisterEntryPoint<TriGrabCardsHandler>();

            builder.RegisterEntryPoint<TriGrabCameraFit>().AsSelf();
            builder.RegisterEntryPoint<TriGrabCardsView>().AsSelf();
            builder.RegisterEntryPoint<TriGrabMoleView>().AsSelf();
            builder.RegisterEntryPoint<TriGrabHookView>().AsSelf();
            builder.RegisterEntryPoint<TriGrabRobotView>().AsSelf();
            builder.RegisterEntryPoint<TriGrabOverlayView>().AsSelf();

            builder.RegisterEntryPoint<TriGrabHudCoordinator>();
            builder.Register<TriGrabPadViewModel>(Lifetime.Transient);
            builder.Register<TriGrabPadView>(Lifetime.Transient);
            builder.Register<TriGrabHudViewModel>(Lifetime.Transient);
            builder.Register<TriGrabHudView>(Lifetime.Transient);

            builder.Register<IRaceStartLook, PlainRaceStartLook>(Lifetime.Singleton);
            builder.Register<RaceStartViewModel>(Lifetime.Transient);
            builder.Register<RaceStartView>(Lifetime.Transient);
            builder.Register<LeaveMatchConfirmViewModel>(Lifetime.Transient);
            builder.Register<LeaveMatchConfirmView>(Lifetime.Transient);
            builder.Register(c => new MatchLeaveRequester(c.Resolve<ISubscriber<LeaveMatchToC>>(),
                message => NetworkClient.Send(new CustomMirrorMessage { payload = message })), Lifetime.Singleton);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<TriGrabPadView>(() => container.Resolve<TriGrabPadView>()));
            sink.Add(windowManager.RegisterViewFactory<TriGrabHudView>(() => container.Resolve<TriGrabHudView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceStartView>(() => container.Resolve<RaceStartView>()));
            sink.Add(windowManager.RegisterViewFactory<LeaveMatchConfirmView>(() => container.Resolve<LeaveMatchConfirmView>()));
        }
    }
}
