using GameFramework;
using LOP.UI;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 씬의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 빌드된다.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private LOPGame game;
        [SerializeField] private LOPGameEngine gameEngine;
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
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);

            // game/gameEngine은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            builder.RegisterComponent(game).As<IGame>();
            builder.RegisterComponent(gameEngine).As<IGameEngine>();
            builder.RegisterComponent(cameraController);

            builder.Register<IGameMessageHandler, GameInfoMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameDamageMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, PlayerHudCoordinator>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, EntityBinder>(Lifetime.Transient);
            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();
            builder.Register<IActionManager, LOPActionManager>(Lifetime.Singleton);
            builder.Register<IMovementManager, LOPMovementManager>(Lifetime.Singleton);
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

        // LOPGame이 additive 로드하는 맵 씬도 이 컨테이너로 주입한다.
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
