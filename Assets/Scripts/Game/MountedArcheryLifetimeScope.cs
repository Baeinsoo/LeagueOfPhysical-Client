using System;
using System.Collections.Generic;
using LOP.UI;
using MessagePipe;
using Mirror;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>MountedArchery 덩어리(클라) — 말 위에서 달리며 쏘는 기수들. 내 말만 예측하고 남은 보간한다.</summary>
    public class MountedArcheryLifetimeScope : GameLifetimeScope
    {
        // 50Hz. 서버와 같아야 한다.
        private const float TickInterval = 0.02f;

        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<MountedArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<MountedArcheryConfig>(
                c => c.Resolve<MountedArcheryConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register(c => new MountSystem(c.Resolve<MountedArcheryConfig>().Mount), Lifetime.Singleton);
            builder.Register(c =>
            {
                var cfg = c.Resolve<MountedArcheryConfig>();
                return new ArcheryAimSystem(cfg.ToBowConfig(), cfg.RiderEyeHeight, turnBodyToAim: false);
            }, Lifetime.Singleton);

            builder.Register<MountedArcheryWorld>(c => new MountedArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                c.Resolve<MountSystem>(),
                c.Resolve<KinematicMoveSystem>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, MountedArcheryPlayerCreator>(Lifetime.Singleton);

            // 내 말만 예측한다 — 남은 보간으로 충분하다(스펙 §3.1).
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);

            // 스냅에서 위치 말고 맞춰야 할 게 없다 — 그래도 EntityBinder가 굴리는 Reconciler의 생성자 의존이라 등록은 필요하다.
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);

            // 외삽 대상이 없어 값은 안 쓰이지만 EntityBinder의 생성자 의존이라 등록은 필요하다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            builder.Register<MountedArcheryClientState>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MountedArcheryStateHandler>();
            builder.Register<MountedAimAssistState>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MountedAimView>().AsSelf();
            builder.RegisterEntryPoint<MountedHorseView>().AsSelf();
            builder.Register<IHaptics>(_ => Haptics.ForThisDevice(), Lifetime.Singleton);
            builder.RegisterEntryPoint<MountedPredictedHits>().AsSelf();
            builder.RegisterEntryPoint<MountedArrowView>().AsSelf();
            builder.RegisterEntryPoint<MountedZoneView>();
            builder.RegisterEntryPoint<MountedLandingMarker>();
            builder.RegisterEntryPoint<MountedHitShapeDebugView>();
            builder.RegisterEntryPoint<MountedArcheryRemoteShotHandler>();
            builder.Register<MountedSpectate>(Lifetime.Singleton).As<ISpectateTargets>().AsSelf();
            builder.RegisterEntryPoint<MountedArcheryHudCoordinator>();
            builder.Register<IArcheryPadViewModel, MountedArcheryPadViewModel>(Lifetime.Transient);
            builder.Register<ArcheryPadView>(Lifetime.Transient);
            //  카운트다운은 예전 모양 그대로, 무음(사용자 결정 10-25 — 개편은 플래피만).
            builder.Register<IRaceStartLook, PlainRaceStartLook>(Lifetime.Singleton);
            builder.Register<RaceStartViewModel>(Lifetime.Transient);
            builder.Register<RaceStartView>(Lifetime.Transient);
            builder.Register<MountedCombatHudViewModel>(Lifetime.Transient);
            builder.Register<MountedCombatHudView>(Lifetime.Transient);
            builder.Register<RaceEliminatedView>(Lifetime.Transient);
            builder.Register<RaceSpectateViewModel>(Lifetime.Transient);
            builder.Register<RaceSpectateView>(Lifetime.Transient);
            builder.Register<LeaveMatchConfirmViewModel>(Lifetime.Transient);
            builder.Register<LeaveMatchConfirmView>(Lifetime.Transient);
            builder.Register(c => new MatchLeaveRequester(c.Resolve<ISubscriber<LeaveMatchToC>>(),
                message => NetworkClient.Send(new CustomMirrorMessage { payload = message })), Lifetime.Singleton);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<ArcheryPadView>(
                () => container.Resolve<ArcheryPadView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceStartView>(
                () => container.Resolve<RaceStartView>()));
            sink.Add(windowManager.RegisterViewFactory<MountedCombatHudView>(
                () => container.Resolve<MountedCombatHudView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceEliminatedView>(
                () => container.Resolve<RaceEliminatedView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceSpectateView>(
                () => container.Resolve<RaceSpectateView>()));
            sink.Add(windowManager.RegisterViewFactory<LeaveMatchConfirmView>(
                () => container.Resolve<LeaveMatchConfirmView>()));
        }
    }
}
