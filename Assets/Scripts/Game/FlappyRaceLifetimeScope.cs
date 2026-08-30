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
            // 이 게임도 이제 외삽 대상이 없다(CharactersPredictedSyncPolicy가 Extrapolated를
            // 절대 안 준다) — 그래도 EntityBinder의 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
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

            //  캐릭터는 전부 예측한다. 남을 외삽으로 그리면 게임 규칙 밖에서 움직여
            //  낙하 상한을 모르고 맵을 뚫는다 — 실측은 스펙 §2 참고.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);

#if UNITY_EDITOR
            //  손 떼고 남의 새를 관찰하기 위한 자동 비행. 토글은 LOP ▸ Debug ▸ Auto Flap.
            //  두 클라를 동시에 조종하면서는 관찰도, 설정을 바꿔 가며 비교하는 것도 불가능하다.
            builder.RegisterEntryPoint<FlappyAutoFlapSystem>();
#endif

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
