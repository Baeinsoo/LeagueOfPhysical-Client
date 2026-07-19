using GameFramework;
using LOP.UI;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;
using GameFramework.Netcode;

namespace LOP
{
    /// <summary>
    /// 게임 씬의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 빌드된다.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, FormerlySerializedAs("gameEngine")] private LOPRunner runner;
        [SerializeField] private CameraController cameraController;

        // 전역 WindowManager에 게임 스코프 View 팩토리를 기여한 핸들(OnDestroy에서 해제).
        private IDisposable _statsViewRegistration;
        private IDisposable _characterHudViewRegistration;
        private IDisposable _gamePadViewRegistration;
        private IDisposable _debugHudViewRegistration;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
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
            builder.Register<AbilityActivator>(Lifetime.Singleton);

            // effect 실행 — executor가 타입별 핸들러로 디스패치. AbilitySystem이 Active 창에서 구동.
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id)), Lifetime.Singleton);
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.ICollisionQuery, GameFramework.UnityCollisionQuery>(Lifetime.Singleton);
            // sweep이 캐릭터도 막는다(Character 포함) — 캐릭터는 서로 통과 못 하는 단단한 벽. 서버와 동일 설정.
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.ICollisionQuery>(), LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);
            // 클라: 내 캐릭만 움직인다(남은 벽). 겹치면 내가 전부 빠져나옴(1.0) — sweep 벽이 주로 막고
            // 밀어내기는 슬쩍 들어간 겹침만 복구. 남은 서버 스냅대로 보간해 따라옴.
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
            builder.Register<GameFramework.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);

            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            builder.RegisterComponent(runner).As<IRunner>();
            builder.RegisterComponent(cameraController);

            // 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 스스로 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputMessageHandler>();
            builder.RegisterEntryPoint<GameInputTimingMessageHandler>();
            builder.RegisterEntryPoint<GameWorldEventMessageHandler>();
            // 순서 중요: EntityBinder가 먼저 구독해야 EntityCreated 시 actor를 만들고 playerContext.actor를 세팅한다.
            // PlayerHudCoordinator(및 actor를 읽는 후속 구독자)는 그 뒤에 와야 한다 — 재정렬 금지.
            builder.RegisterEntryPoint<EntityBinder>();
            builder.RegisterEntryPoint<PlayerHudCoordinator>();
            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();
            builder.Register<CharacterCreator>(Lifetime.Singleton);
            builder.Register<ItemCreator>(Lifetime.Singleton);
            builder.Register<EntitySpawner>(Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);

            builder.Register<StatsViewModel>(Lifetime.Transient);
            builder.Register<StatsView>(Lifetime.Transient);

            builder.Register<CharacterHudViewModel>(Lifetime.Transient);
            builder.Register<CharacterHudView>(Lifetime.Transient);

            builder.Register<GamePadViewModel>(Lifetime.Transient);
            builder.Register<GamePadView>(Lifetime.Transient);

            builder.Register<DebugHudViewModel>(Lifetime.Transient);
            builder.Register<DebugHudView>(Lifetime.Transient);

            builder.Register<MatchSeed>(Lifetime.Singleton);
            builder.Register<ReconciliationStats>(Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.1f, 0.025f, 3f), Lifetime.Singleton);
            builder.Register<InputTimingStats>(Lifetime.Singleton);
            builder.Register<LeadState>(Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<PredictedAbilityState>(128), Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<InputCommand>(128), Lifetime.Singleton);
            builder.Register<Reconciler>(Lifetime.Singleton);
            builder.Register<RemoteInterpolationClock>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
                SceneManager.sceneLoaded += OnSceneLoaded;

                // 전역 WindowManager에 게임 스코프 View 팩토리 기여: Open<T>가 게임 스코프 resolver로 생성 → IPlayerContext 주입.
                var windowManager = container.Resolve<IWindowManager>();
                _statsViewRegistration = windowManager.RegisterViewFactory<StatsView>(() => container.Resolve<StatsView>());
                _characterHudViewRegistration = windowManager.RegisterViewFactory<CharacterHudView>(() => container.Resolve<CharacterHudView>());
                _gamePadViewRegistration = windowManager.RegisterViewFactory<GamePadView>(() => container.Resolve<GamePadView>());
                _debugHudViewRegistration = windowManager.RegisterViewFactory<DebugHudView>(() => container.Resolve<DebugHudView>());
            });
        }

        protected override void OnDestroy()
        {
            // 팩토리 해제 + 열린 View Close (base가 컨테이너를 dispose하기 전에).
            _statsViewRegistration?.Dispose();
            _characterHudViewRegistration?.Dispose();
            _gamePadViewRegistration?.Dispose();
            _debugHudViewRegistration?.Dispose();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        // Factory가 additive 로드하는 맵 씬도 이 컨테이너로 주입한다.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 자기 씬은 빌드 콜백에서 이미 주입했다. (자기 씬 Awake 중 구독해 자기 sceneLoaded도 수신됨)
            if (scene == gameObject.scene)
            {
                Debug.Log($"[GameLifetimeScope] Skip re-injecting own scene '{scene.name}'; already injected in build callback.");
                return;
            }

            Container.InjectSceneObjects(scene);
        }
    }
}
