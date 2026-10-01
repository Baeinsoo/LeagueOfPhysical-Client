using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>UI 인프라 DI 등록 모듈. 앱 루트 스코프에서 Install. 앱 스코프와 코드 결합을 분리한다.</summary>
    public class UIInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            var uiRoot = Resources.Load<WindowManager>("UI/UIRoot");
            builder.RegisterComponentInNewPrefab(uiRoot, Lifetime.Singleton)
                .DontDestroyOnLoad()
                .As<IWindowManager>();

            builder.Register<LoginViewModel>(Lifetime.Transient);
            builder.Register<LoginView>(Lifetime.Transient);

            //  로그인보다 먼저 — 서버에 닿는지 확인하고, 안 닿으면 [다시 시도] 팝업.
            builder.Register(_ => new LOP.ServerReachability(), Lifetime.Singleton);
            builder.Register<LOP.ServerGate>(Lifetime.Transient);
            builder.Register<ServerUnreachableViewModel>(Lifetime.Transient);
            builder.Register<ServerUnreachableView>(Lifetime.Transient);

            builder.Register<GameLoadingView>(Lifetime.Transient);
            builder.Register<MatchingWaitingView>(Lifetime.Transient);
            builder.Register<MatchmakingFailedView>(Lifetime.Transient);
        }
    }
}
