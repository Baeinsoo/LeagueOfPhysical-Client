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
        private IDisposable _matchMakingViewRegistration;

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

            // VM은 Scoped — View와 Coordinator가 같은 인스턴스를 공유해야 신호가 이어진다.
            builder.Register<MatchMakingViewModel>(Lifetime.Scoped);
            builder.Register<MatchMakingView>(Lifetime.Transient);
            builder.RegisterEntryPoint<MatchmakingCoordinator>();

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);

                // 전역 WindowManager에 MatchMakingView 팩토리 기여: Open<MatchMakingView>가 이 스코프 resolver로
                // 생성 → MatchStateMachine/IMatchMakingDataStore 주입. 로비 진입 시 화면을 연다.
                var windowManager = container.Resolve<IWindowManager>();
                _matchMakingViewRegistration = windowManager.RegisterViewFactory<MatchMakingView>(() => container.Resolve<MatchMakingView>());
                windowManager.Open<MatchMakingView>();
            });
        }

        protected override void OnDestroy()
        {
            // 팩토리 해제 + 열린 MatchMakingView Close → VM.Dispose → FSM.Stop (base가 컨테이너 dispose하기 전).
            _matchMakingViewRegistration?.Dispose();
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
