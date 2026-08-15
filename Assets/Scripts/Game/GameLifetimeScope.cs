using LOP.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 씬의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 빌드된다.
    /// 게임마다 무엇이 달라지는지는 파생 스코프가 <see cref="ConfigureGame"/>에서 정한다.
    /// </summary>
    public abstract class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, FormerlySerializedAs("gameEngine")] protected LOPRunner runner;

        // 전역 WindowManager에 이 스코프가 기여한 View 팩토리 핸들(OnDestroy에서 해제).
        private readonly List<IDisposable> viewRegistrations = new List<IDisposable>();

        protected override void Configure(IContainerBuilder builder)
        {
            new GameplayInstaller().Install(builder);

            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            // AsSelf는 LOP 전용 진입점(EndMatch 등)을 쓰는 소비자를 위한 것 — IRunner에는 없는 API다.
            builder.RegisterComponent(runner).As<GameFramework.Runner.IRunner>().AsSelf();

            ConfigureGame(builder);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
                SceneManager.sceneLoaded += OnSceneLoaded;

                // 전역 WindowManager에 게임 스코프 View 팩토리 기여: Open<T>가 게임 스코프 resolver로 생성 → IPlayerContext 주입.
                var windowManager = container.Resolve<IWindowManager>();
                viewRegistrations.Add(windowManager.RegisterViewFactory<DebugHudView>(() => container.Resolve<DebugHudView>()));
                RegisterViewFactories(container, windowManager, viewRegistrations);
            });
        }

        /// <summary>이 게임에서만 쓰는 등록 — 월드, 플레이어 몸 생성기, 게임 UI 등.</summary>
        protected abstract void ConfigureGame(IContainerBuilder builder);

        /// <summary>이 게임에서만 여는 화면의 View 팩토리를 sink에 담는다(담긴 것은 스코프가 알아서 해제한다).</summary>
        protected virtual void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
        }

        protected override void OnDestroy()
        {
            // 팩토리 해제 + 열린 View Close (base가 컨테이너를 dispose하기 전에).
            foreach (var registration in viewRegistrations)
            {
                registration?.Dispose();
            }
            viewRegistrations.Clear();

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
