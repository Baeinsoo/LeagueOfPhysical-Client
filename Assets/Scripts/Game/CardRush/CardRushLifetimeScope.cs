using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>카드 러시 덩어리(클라) — 물리 없는 상태 월드. 내 몸만 예측하고 남은 보간한다.</summary>
    public class CardRushLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        //  체력이 의미 없는 모드다 — 늘 가득 찬 막대가 머리 위에 뜨지 않게 끈다.
        protected override bool ShowHealthBars => false;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<CardRushConfigProvider>(Lifetime.Singleton);
            builder.Register<CardRushConfig>(c => c.Resolve<CardRushConfigProvider>().Get(), Lifetime.Singleton);

            //  맵 씬의 CardRushMapRules 마커가 맵 로드 때 여기에 값을 넣는다. 등록이 없으면 마커의 [Inject]가
            //  씬 주입을 끊는다 — 클·서 양쪽 다 등록해야 한다(서버 CardRushLifetimeScope와 같은 줄).
            builder.Register<CardRushMapRulesField>(Lifetime.Singleton);

            //  서버와 같은 팩토리. 클라 씨앗은 GameInfo로 늦게 오지만 월드가 출발 전에 코스를 다시 만든다.
            //  자리는 월드 Transform에 적고, 그림·PhysX 대리 몸은 그 값을 그대로 읽는다 — 월드에 IMotionBridge가 필요 없다.
            builder.Register<CardRushWorld>(c => new CardRushWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<CardRushMapRulesField>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<CardRushConfig>().Timing), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, CardRushRunnerCreator>(Lifetime.Singleton);

            //  내 몸만 예측한다(탭 타이밍이 전부라 내 입력은 바로 보여야 한다). 남은 스냅 보간으로 충분하다.
            builder.Register<IEntitySyncPolicy>(
                c => new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId),
                Lifetime.Singleton);

            //  위치 말고도 칸·멈춤 등 다섯 칸을 서버 판정에 맞춘다.
            builder.Register<IServerCorrectionHandler>(
                c => new CardRushServerCorrectionHandler(c.Resolve<CardRushWorld>()), Lifetime.Singleton);
            //  외삽 대상이 없다(예측 아니면 보간) — EntityBinder 생성자 의존이라 등록만 한다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            builder.RegisterEntryPoint<CardRushHudCoordinator>();
        }
    }
}
