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

            builder.Register<CheckMatch>(Lifetime.Transient);
            builder.Register<Idle>(Lifetime.Transient);
            builder.Register<RequestMatchmaking>(Lifetime.Transient);
            builder.Register<InWaitingRoom>(Lifetime.Transient);
            builder.Register<InGameRoom>(Lifetime.Transient);
            builder.Register<CancelMatchmaking>(Lifetime.Transient);

            builder.Register<MatchMakingViewModel>(Lifetime.Transient);
            builder.Register<MatchMakingView>(Lifetime.Transient);

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
    }
}
