using GameFramework;
using LOP.UI;
using System;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class LobbyLifetimeScope : LifetimeScope
    {
        // 전역 WindowManager에 로비 스코프 View 팩토리를 기여한 핸들(OnDestroy에서 해제).
        private IDisposable _lobbyHomeViewRegistration;
        private IDisposable _shopViewRegistration;
        private IDisposable _settingsViewRegistration;
        private IDisposable _profileViewRegistration;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<MatchStateMachine>(Lifetime.Transient);

            // 각 상태를 Transient로 + 그 Func<T> 팩토리를 함께 등록한다. 상태 생성자가 IObjectResolver(서비스
            // 로케이터) 대신 Func<다음상태>로 전이 대상을 명시할 수 있게 함 — VContainer는 암묵적 Func<T>를
            // 제공하지 않아 팩토리를 명시 등록해야 한다.
            RegisterState<CheckMatch>(builder);
            RegisterState<Idle>(builder);
            RegisterState<RequestMatchmaking>(builder);
            RegisterState<InWaitingRoom>(builder);
            RegisterState<InGameRoom>(builder);
            RegisterState<CancelMatchmaking>(builder);

            // VM은 Scoped — LobbyHomeView(Play)와 Coordinator가 같은 인스턴스를 공유해야 신호가 이어진다.
            builder.Register<MatchmakingViewModel>(Lifetime.Scoped);
            builder.Register<LobbyHomeView>(Lifetime.Transient);
            builder.RegisterEntryPoint<MatchmakingCoordinator>();

            // 프론트엔드 네비(상점/설정/프로필). VM은 Scoped — LobbyHomeView와 FrontEndCoordinator가 공유한다.
            builder.Register<LobbyHomeViewModel>(Lifetime.Scoped);
            builder.Register<ShopView>(Lifetime.Transient);
            builder.Register<SettingsView>(Lifetime.Transient);
            builder.Register<ProfileView>(Lifetime.Transient);
            builder.RegisterEntryPoint<FrontEndCoordinator>();

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);

                // 전역 WindowManager에 LobbyHomeView 팩토리 기여: Open<LobbyHomeView>가 이 스코프 resolver로
                // 생성 → MatchmakingViewModel 주입. 로비 진입 시 허브 화면을 연다.
                var windowManager = container.Resolve<IWindowManager>();
                _lobbyHomeViewRegistration = windowManager.RegisterViewFactory<LobbyHomeView>(() => container.Resolve<LobbyHomeView>());

                // 셸도 같은 방식으로 기여: FrontEndCoordinator의 Open<T>가 이 스코프 resolver로 셸을 만든다.
                _shopViewRegistration = windowManager.RegisterViewFactory<ShopView>(() => container.Resolve<ShopView>());
                _settingsViewRegistration = windowManager.RegisterViewFactory<SettingsView>(() => container.Resolve<SettingsView>());
                _profileViewRegistration = windowManager.RegisterViewFactory<ProfileView>(() => container.Resolve<ProfileView>());

                windowManager.Open<LobbyHomeView>();
            });
        }

        protected override void OnDestroy()
        {
            // 팩토리 해제(열린 LobbyHomeView 닫힘) 후 컨테이너 dispose — VM(Scoped)·Coordinator(EntryPoint)
            // 정리는 그 컨테이너 dispose에서 함께 일어난다.
            _lobbyHomeViewRegistration?.Dispose();
            _shopViewRegistration?.Dispose();
            _settingsViewRegistration?.Dispose();
            _profileViewRegistration?.Dispose();
            base.OnDestroy();
        }

        // 상태를 Transient로 등록하고 그 Func<T> 팩토리도 함께 등록한다(전이를 Func<다음상태>로 명시하기 위해).
        private static void RegisterState<T>(IContainerBuilder builder) where T : class
        {
            builder.Register<T>(Lifetime.Transient);
            builder.RegisterFactory<T>(resolver => () => resolver.Resolve<T>(), Lifetime.Singleton);
        }
    }
}
