using LOP.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 덩어리가 게임 종류와 무관하게 공통으로 쓰는 등록.
    /// 게임마다 갈리는 것(월드·플레이어 몸 생성기·게임 UI)은 각 게임 스코프가 따로 넣는다.
    /// </summary>
    public class GameplayInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
            builder.Register<MovementSystem>(Lifetime.Singleton);
            builder.Register<MotionContributionSystem>(Lifetime.Singleton);
            builder.Register<InputBufferSystem>(Lifetime.Singleton);
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
            builder.Register<CharacterLoadoutProvider>(Lifetime.Singleton);
            builder.Register<AbilityActivator>(Lifetime.Singleton);

            // effect 실행 — executor가 타입별 핸들러로 디스패치. AbilitySystem이 Active 창에서 구동.
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id),
                c.Resolve<GameFramework.World.EntityRegistry>()), Lifetime.Singleton);
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.IPhysicsSimulator, GameFramework.Physics.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.ICollisionQuery, GameFramework.Physics.UnityCollisionQuery>(Lifetime.Singleton);
            // sweep이 캐릭터도 막는다(Character 포함) — 캐릭터는 서로 통과 못 하는 단단한 벽. 서버와 동일 설정.
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.Physics.ICollisionQuery>(), LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);
            // 클라: 내 캐릭만 움직인다(남은 벽). 겹치면 내가 전부 빠져나옴(1.0) — sweep 벽이 주로 막고
            // 밀어내기는 슬쩍 들어간 겹침만 복구. 남은 서버 스냅대로 보간해 따라옴.
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
            builder.Register<GameFramework.Runner.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);

            // 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 스스로 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputTimingMessageHandler>();
            builder.RegisterEntryPoint<GameWorldEventMessageHandler>();
            builder.RegisterEntryPoint<MatchEndedMessageHandler>();
            // EntityBinder가 EntityCreated 때 로컬 유저 actor를 만들어 playerContext.actor에 세팅한다.
            // 게임별 PlayerHudCoordinator(각 게임 스코프가 등록)와 등록 순서가 무관하다 — 이유는 그쪽 주석 참고.
            builder.RegisterEntryPoint<EntityBinder>();
            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();
            builder.Register<ItemCreator>(Lifetime.Singleton);
            builder.Register<EntitySpawner>(Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);

            builder.Register<DebugHudViewModel>(Lifetime.Transient);
            builder.Register<DebugHudView>(Lifetime.Transient);

            builder.Register<MatchSeed>(Lifetime.Singleton);
            builder.Register<ReconciliationStats>(Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.1f, 0.025f, 3f), Lifetime.Singleton);
            builder.Register<InputTimingStats>(Lifetime.Singleton);
            builder.Register<GameFramework.Netcode.SnapshotArrivalStats>(Lifetime.Singleton);
            builder.Register<LeadState>(Lifetime.Singleton);
            builder.Register<GameFramework.Netcode.INetworkTime, MirrorNetworkTime>(Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<LOPSavedState>(128), Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<InputCommand>(128), Lifetime.Singleton);
            builder.Register<Reconciler>(Lifetime.Singleton);
            builder.Register<RemoteInterpolationClock>(Lifetime.Singleton);
            builder.Register<EntityRenderClock>(Lifetime.Singleton);

            // Slice 5-B: LOPRunner.UpdateRunner 인라인 파이프라인 스텝 → ITickSystem 추출(god-object 해체).
            builder.Register<ReconcileSystem>(Lifetime.Singleton);
            builder.Register<PhysicsSimulationSystem>(Lifetime.Singleton);
            builder.Register<WorldEventDrainSystem>(Lifetime.Singleton);
            builder.Register<LocalSnapshotSystem>(Lifetime.Singleton);
            builder.Register<DespawnFlushSystem>(Lifetime.Singleton);
        }
    }
}
