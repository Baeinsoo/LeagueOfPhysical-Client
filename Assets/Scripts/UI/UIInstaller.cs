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

            builder.Register<GameLoadingView>(Lifetime.Transient);
            builder.Register<MatchingWaitingView>(Lifetime.Transient);
        }
    }
}
