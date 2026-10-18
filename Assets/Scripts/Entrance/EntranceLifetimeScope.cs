using GameFramework;
using System;
using LOP.UI;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class EntranceLifetimeScope : LifetimeScope
    {
        private IDisposable _titleViewRegistration;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IEntranceComponent, ServerCheckComponent>(Lifetime.Transient);   // 로그인보다 먼저
            builder.Register<IEntranceComponent, LoginComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadUserComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, JoinLobbyComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadMasterDataComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadPlayerProfileComponent>(Lifetime.Transient);   // 마스터데이터 뒤(코인 통화표)

            builder.Register<EntranceProgress>(Lifetime.Singleton);
            builder.Register<TitleViewModel>(Lifetime.Transient);
            builder.Register<TitleView>(Lifetime.Transient);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);

                //  타이틀은 이 씬 스코프가 연다. 씬이 내려갈 때 등록을 풀면 열린 타이틀이 함께 닫힌다 —
                //  같은 프레임에 Lobby 스코프가 로비 홈을 열어 빈 화면이 끼지 않는다.
                var windowManager = container.Resolve<IWindowManager>();
                _titleViewRegistration = windowManager.RegisterViewFactory<TitleView>(() => container.Resolve<TitleView>());
                windowManager.Open<TitleView>();
            });
        }

        protected override void OnDestroy()
        {
            _titleViewRegistration?.Dispose();
            _titleViewRegistration = null;
            base.OnDestroy();
        }
    }
}
