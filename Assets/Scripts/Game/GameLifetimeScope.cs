using GameFramework;
using LOP.UI;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

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
            builder.Register<InputBufferSystem>(Lifetime.Singleton);
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityActivator>(Lifetime.Singleton);

            // effect 실행 — executor가 타입별 핸들러로 디스패치. AbilitySystem이 Active 창에서 구동.
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<MotionEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id)), Lifetime.Singleton);
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);

            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            builder.RegisterComponent(runner).As<IRunner>();
            builder.RegisterComponent(cameraController);

            // 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 스스로 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputMessageHandler>();
            builder.RegisterEntryPoint<GameInputTimingMessageHandler>();
            builder.RegisterEntryPoint<GameDamageMessageHandler>();
            builder.RegisterEntryPoint<GameAbilityMessageHandler>();
            builder.RegisterEntryPoint<PlayerHudCoordinator>();
            builder.RegisterEntryPoint<EntityBinder>();
            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();
            builder.Register<IEntityCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreator, ItemCreator>(Lifetime.Singleton);
            builder.Register<IEntityFactory, EntityFactory>(Lifetime.Singleton);

            builder.Register<StatsViewModel>(Lifetime.Transient);
            builder.Register<StatsView>(Lifetime.Transient);

            builder.Register<CharacterHudViewModel>(Lifetime.Transient);
            builder.Register<CharacterHudView>(Lifetime.Transient);

            builder.Register<GamePadViewModel>(Lifetime.Transient);
            builder.Register<GamePadView>(Lifetime.Transient);

            builder.Register<DebugHudViewModel>(Lifetime.Transient);
            builder.Register<DebugHudView>(Lifetime.Transient);

            builder.Register<ReconciliationStats>(Lifetime.Singleton);
            builder.Register<InputTimingStats>(Lifetime.Singleton);
            builder.Register<LeadState>(Lifetime.Singleton);
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
            builder.Register(_ => new InputHistory(128), Lifetime.Singleton);
            builder.Register<Reconciler>(Lifetime.Singleton);

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
