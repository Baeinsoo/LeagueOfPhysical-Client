using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Flappy Race 덩어리 — 새 월드와 새 생성기를 쓴다.</summary>
    public class FlappyRaceLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);
            builder.Register<FlappyConfigProvider>(Lifetime.Singleton);
            builder.Register<FlappyConfig>(c => c.Resolve<FlappyConfigProvider>().Get(), Lifetime.Singleton);
            // 남을 외삽으로 그릴 때 쓸 가속도(중력) — EntityBinder는 이 인터페이스만 안다.
            builder.Register<IExtrapolationAcceleration>(
                c => new FlappyExtrapolationAcceleration(c.Resolve<FlappyConfig>()), Lifetime.Singleton);

            builder.Register<FlappyMoveSystem>(Lifetime.Singleton);
            builder.Register<FlappyBodyCollisionSystem>(Lifetime.Singleton);
            builder.Register<FlappyStunSystem>(Lifetime.Singleton);
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 물리엔진이 아니라 우리 계산으로 민다.
            // 새의 물리 몸은 PhysicsBodyFactory가 만들면서 무조건 Character 레이어에 둔다. 그래서 이
            // 마스크에 Character가 없는 한 새끼리는 sweep에 걸리지 않는다.
            // (겉모습 프리팹 Bird.prefab에는 콜라이더가 없어 물리에는 아예 존재하지 않는다.)
            // FlappyWorld를 구체로도 해석할 수 있어야 보정 핸들러가 자기 게임 월드를 직접 본다.
            builder.Register<FlappyWorld>(c => new FlappyWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<FlappyMoveSystem>(),
                c.Resolve<FlappyBodyCollisionSystem>(),
                c.Resolve<FlappyStunSystem>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                LayerMask.GetMask("Default")), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            //  스턴은 서버 권위다. 남의 새까지 클라가 굴리면서 "남이 부딪혔나"도 예측하게 됐고,
            //  그 판정이 갈리면 0.8초 얼음이 통째로 어긋난다.
            builder.Register<IServerCorrectionHandler, FlappyServerCorrectionHandler>(Lifetime.Singleton);

            // 남의 플랩 입력이 클라로 안 오므로 남을 굴리면 "계속 추락"이 된다 — 내 새만 예측하고 남은 외삽한다.
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedRemotesExtrapolatedSyncPolicy(
                    () => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);

            builder.RegisterEntryPoint<FlappyHudCoordinator>();
            builder.Register<FlapPadViewModel>(Lifetime.Transient);
            builder.Register<FlapPadView>(Lifetime.Transient);
            builder.Register<RaceStartViewModel>(Lifetime.Transient);
            builder.Register<RaceStartView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<FlapPadView>(() => container.Resolve<FlapPadView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceStartView>(() => container.Resolve<RaceStartView>()));
        }
    }
}
