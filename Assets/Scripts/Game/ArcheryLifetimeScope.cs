using System;
using System.Collections.Generic;
using LOP.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Archery 덩어리(클라) — 제자리에 선 사수들, 남도 굴려서 남의 화살도 보인다.</summary>
    public class ArcheryLifetimeScope : GameLifetimeScope
    {
        private const float TickInterval = 0.02f;

        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<ArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<ArcheryConfig>(c => c.Resolve<ArcheryConfigProvider>().Get(), Lifetime.Singleton);
            builder.Register<ArcheryConsumed>(Lifetime.Singleton);

            //  과녁이 언제 어디 서는지를 정하는 한 곳. 명단은 매치 시작 시점의 것을 그대로 쓴다 —
            //  중간에 나간 사람이 있어도 과녁 주인이 밀리지 않게(스펙 6.2절).
            builder.Register<ArcheryCourse>(c => new ArcheryCourse(
                c.Resolve<ArcheryConfig>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<IRoomDataStore>().match.playerList,
                TickInterval), Lifetime.Singleton);

            builder.RegisterComponent(cameraController);

            builder.Register<ArcheryAimSystem>(Lifetime.Singleton);
            builder.Register<ArcheryWorld>(c => new ArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, ArcheryPlayerCreator>(Lifetime.Singleton);

            // 남도 굴린다 — 서버가 되뿌린 남의 입력이 남의 활을 같은 규칙으로 당기게 한다.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);

            // 스냅에서 위치 말고 맞춰야 할 게 없다(판치기와 같은 사정) — 그래도 EntityBinder가 굴리는
            // Reconciler의 생성자 의존이라 등록은 필요하다.
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);

            // 이 게임엔 외삽 대상이 없다(정책이 Extrapolated를 절대 안 준다) — 그래도 EntityBinder의
            // 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);

            //  화살이 과녁에 꽂히는 순간은 틱마다 찾는다 — 서버의 ArcheryHitSystem과 같은 모양이다.
            //  프레임마다 찾으면 프레임 레이트에 따라 구간이 벌어져 기기마다 다르게 보인다.
            builder.Register(c => new ArcheryArrowStickSystem(
                c.Resolve<ArcheryWorld>(),
                c.Resolve<ArcheryCourse>(),
                c.Resolve<ArcheryConsumed>(),
                TickInterval), Lifetime.Singleton);

            builder.RegisterEntryPoint<ArcheryAimView>().AsSelf();
            builder.RegisterEntryPoint<ArcheryAimGuideView>().AsSelf();
            builder.RegisterEntryPoint<ArcheryArrowView>().AsSelf();
            builder.RegisterEntryPoint<ArcheryTargetView>().AsSelf();
            builder.RegisterEntryPoint<ArcheryRemoteShotHandler>();
            builder.RegisterEntryPoint<ArcheryHitHandler>();
            builder.RegisterEntryPoint<ArcheryStateHandler>();

            builder.RegisterEntryPoint<ArcheryHudCoordinator>();
            builder.Register<ArcheryPadViewModel>(Lifetime.Transient);
            builder.Register<ArcheryPadView>(Lifetime.Transient);

            //  화살이 생긴 *뒤*에 봐야 하므로 world.Tick 다음인 End에 문다(서버와 같은 자리).
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<ArcheryArrowStickSystem>());
            });
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<ArcheryPadView>(
                () => container.Resolve<ArcheryPadView>()));
        }
    }
}
