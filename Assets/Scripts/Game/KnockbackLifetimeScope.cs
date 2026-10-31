using System;
using System.Collections.Generic;
using LOP.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Knockback 덩어리(클라) — 칸 바닥 위를 걷는 몸들. 내 몸만 예측하고 남은 보간한다.</summary>
    public class KnockbackLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        // 이 모드는 체력 바가 의미가 없다 — 떨어지면 탈락이다.
        protected override bool ShowHealthBars => false;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<KnockbackConfigProvider>(Lifetime.Singleton);
            builder.Register<KnockbackConfig>(c => c.Resolve<KnockbackConfigProvider>().Get(), Lifetime.Singleton);

            // 바닥은 월드와 이동 시스템이 같은 한 개를 나눠 쓴다.
            builder.Register<KnockbackFloor>(c => c.Resolve<KnockbackConfig>().CreateFloor(), Lifetime.Singleton);
            builder.Register<KnockbackMoveSystem>(c => c.Resolve<KnockbackConfig>().CreateMoveSystem(
                c.Resolve<KnockbackFloor>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);

            builder.Register(c => new KnockbackFireSystem(c.Resolve<KnockbackConfig>()), Lifetime.Singleton);
            builder.Register(c => new BodyCollisionSystem(
                BodySizes.CharacterRadius, BodySizes.CharacterHeight,
                c.Resolve<KnockbackConfig>().BodyRestitution, new UnityEngine.Vector3(1, 0, 1)), Lifetime.Singleton);

            builder.Register<KnockbackWorld>(c => new KnockbackWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<KnockbackMoveSystem>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                c.Resolve<KnockbackFloor>(),
                c.Resolve<KnockbackFireSystem>(),
                c.Resolve<BodyCollisionSystem>(),
                c.Resolve<KnockbackConfig>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, KnockbackPlayerCreator>(Lifetime.Singleton);

            // 내 몸만 예측한다 — 남은 보간으로 충분하다.
            // 내 몸 보정은 0.1초 녹인다: 남이 쏜 넉백은 서버가 정해 늦게 알게 되므로 되감으면 2~3m씩 옮겨져
            // 순간이동으로 보였다(소스 엔진 cl_smooth와 같은 처리). 날아가는 중엔 조작이 원래 약해 잃는 게 적다.
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId, localCorrectionSmoothTime: 0.1f),
                Lifetime.Singleton);

            // 위치 말고도 날림 여부·피해 %를 서버 결과에 맞춘다.
            builder.Register<IServerCorrectionHandler>(
                c => new KnockbackServerCorrectionHandler(c.Resolve<KnockbackWorld>()), Lifetime.Singleton);
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            // 서버가 알려 주는 판 상태와 남의 발사·맞음. 상태는 칸·원·라벨·조작판이 함께 읽는다.
            builder.Register<KnockbackClientState>(Lifetime.Singleton);
            builder.RegisterEntryPoint<KnockbackStateHandler>();
            builder.RegisterEntryPoint<KnockbackRemoteShotHandler>();

            builder.RegisterEntryPoint<KnockbackFloorView>();
            builder.RegisterEntryPoint<KnockbackShotView>();
            builder.RegisterEntryPoint<KnockbackZoneView>();
            builder.RegisterEntryPoint<KnockbackDamageLabel>();
            builder.RegisterEntryPoint<KnockbackHudCoordinator>();
            builder.Register<KnockbackPadViewModel>(Lifetime.Transient);
            builder.Register<KnockbackPadView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<KnockbackPadView>(() => container.Resolve<KnockbackPadView>()));
        }
    }
}
