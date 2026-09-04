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
            // 이 게임엔 외삽 대상이 없다(OwnerPredictedSyncPolicy는 예측 아니면 보간만 준다)
            // — 그래도 EntityBinder의 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration>(
                c => new FlappyExtrapolationAcceleration(c.Resolve<FlappyConfig>()), Lifetime.Singleton);

            builder.Register<FlappyMoveSystem>(Lifetime.Singleton);
            builder.Register<FlappyStunSystem>(Lifetime.Singleton);
            builder.Register<FlappyDashSystem>(Lifetime.Singleton);
            //  새는 +x로 달린다. 폴백을 주지 않는다 — 마커가 없으면 룰이 Initialize에서 터뜨린다.
            builder.Register(c => new FinishLineBounds(FinishAxis.X), Lifetime.Singleton);
            builder.Register(c => new FinishSystem(
                c.Resolve<FinishLineBounds>(), FinishAxis.X, increasing: true), Lifetime.Singleton);
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 아예 부딪히지 않는다(서로 통과한다).
            // 새의 물리 몸은 PhysicsBodyFactory가 만들면서 무조건 Character 레이어에 둔다. 그래서 이
            // 마스크에 Character가 없는 한 새끼리는 sweep에 걸리지 않는다.
            // (겉모습 프리팹 Bird.prefab에는 콜라이더가 없어 물리에는 아예 존재하지 않는다.)
            // FlappyWorld를 구체로도 해석할 수 있어야 보정 핸들러가 자기 게임 월드를 직접 본다.
            builder.Register<FlappyWorld>(c => new FlappyWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<FlappyMoveSystem>(),
                c.Resolve<FlappyStunSystem>(),
                c.Resolve<FlappyDashSystem>(),
                c.Resolve<FinishSystem>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                LayerMask.GetMask("Default")), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            //  스턴은 서버 권위다. 내 새는 클라가 굴리므로 "내가 맵에 부딪혔나"를 예측하게 되는데,
            //  그 판정이 서버와 갈리면 0.8초 얼음이 통째로 어긋난다.
            builder.Register<IServerCorrectionHandler, FlappyServerCorrectionHandler>(Lifetime.Singleton);

            //  내 새만 예측하고, 남의 새는 서버 시간대의 스냅샷 보간으로 그린다.
            //
            //  남을 예측하려면 그 입력이 필요한데, 클라는 서버보다 앞서 달리므로 지금 그릴 구간의
            //  입력은 상대가 아직 보내지도 않았다. 서버가 남의 입력을 되뿌리게 해 봤지만
            //  (EntityInputsToC) 편도 75ms에서 100개 중 0개가 제때 왔다 — 지나간 구간만 정확해지고
            //  화면에 그리는 구간은 그대로였다. 남는 오차 = (속도폭 53) × (앞선 시간 0.105초) ≈ 5.6m이고
            //  상대가 날갯짓할 때마다 그만큼 튄다. 앞서 달리는 한 없앨 수 없는 값이다.
            //
            //  보간으로 그리면 그 튐이 0이 된다. 대가는 남의 새가 약 0.23초(내가 앞선 시간 + 편도 +
            //  보간 쿠션)만큼 뒤에 그려지는 것 — 11m/s로 2.5m다. 전진 속도가 모두 같아 새마다
            //  똑같이 걸리는 상수라 새들끼리의 순서는 정확하고, 레이스 중엔 보이지 않는다.
            //  결승선에서만 드러나므로 등수는 화면이 아니라 통과 틱으로 판정한다.
            //  (이 선택의 옛 대가였던 "몸싸움이 어긋난다"는 새끼리 충돌을 없애면서 사라졌다.)
            //
            //  전부 보간(AllInterpolatedSyncPolicy)도 실측했다 — 화면은 일관된 한 장이 되지만
            //  입력 지연이 RTT+쿠션이라 편도 50ms에서도 조작이 불가능했다. 탭 타이밍이 전부인
            //  게임이라 내 새의 예측은 포기할 수 없다.
            builder.Register<IEntitySyncPolicy>(
                c => new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId),
                Lifetime.Singleton);

#if UNITY_EDITOR
            //  손 떼고 남의 새를 관찰하기 위한 자동 비행. 토글은 LOP ▸ Debug ▸ Auto Flap.
            //  두 클라를 동시에 조종하면서는 관찰도, 설정을 바꿔 가며 비교하는 것도 불가능하다.
            builder.RegisterEntryPoint<FlappyAutoFlapSystem>();
#endif

            //  AsSelf로도 등록한다 — FlapPad가 "추격자까지 몇 m"를 그리려면 벽 위치를 읽어야 하고,
            //  같은 값을 읽어야 숫자와 그림이 어긋나지 않는다.
            builder.RegisterEntryPoint<FlappyChaserView>().AsSelf();

            builder.RegisterEntryPoint<FlappyHudCoordinator>();
            builder.Register<FlapPadViewModel>(Lifetime.Transient);
            builder.Register<FlapPadView>(Lifetime.Transient);
            builder.Register<RaceStartViewModel>(Lifetime.Transient);
            builder.Register<RaceStartView>(Lifetime.Transient);
            builder.Register<RaceEliminatedView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<FlapPadView>(() => container.Resolve<FlapPadView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceStartView>(() => container.Resolve<RaceStartView>()));
            sink.Add(windowManager.RegisterViewFactory<RaceEliminatedView>(() => container.Resolve<RaceEliminatedView>()));
        }
    }
}
