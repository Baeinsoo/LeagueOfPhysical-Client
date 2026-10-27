using LOP.UI;
using System;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>결과 씬 스코프 — 결과 복사본을 이 씬의 결과 저장소로 두고, 결과판 창과 진행(ResultFlow)을 세운다.</summary>
    public class ResultLifetimeScope : LifetimeScope
    {
        private IDisposable _matchResultViewRegistration;

        protected override void Configure(IContainerBuilder builder)
        {
            //  Root 저장소에서 떼어 오고 Root 쪽은 비운다 — 이 씬이 어떻게 닫히든 다음 판에 지난 결과가 안 남는다.
            var root = Parent != null ? Parent.Container.Resolve<IMatchResultDataStore>() : null;
            var snapshot = new MatchResultSnapshot(root?.result);
            root?.Clear();
            builder.RegisterInstance(snapshot).As<IMatchResultDataStore>();

            //  View와 함께 Transient — 결과판을 열 때 그 시점의 결과 복사본을 읽는다.
            builder.Register<MatchResultViewModel>(Lifetime.Transient);
            builder.Register<MatchResultView>(Lifetime.Transient);

            //  NEW BEST 소리(spec §7) — 경기 소리는 경기 스코프라 여기선 없다. 씬이 닫히면 VContainer가 치운다.
            builder.Register<IResultSound, ResultSound>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container =>
            {
                var windowManager = container.Resolve<IWindowManager>();
                _matchResultViewRegistration = windowManager.RegisterViewFactory<MatchResultView>(() => container.Resolve<MatchResultView>());

                var flow = new UnityEngine.GameObject("ResultFlow").AddComponent<ResultFlow>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(flow.gameObject, gameObject.scene);
                container.InjectGameObject(flow.gameObject);
            });
        }

        protected override void OnDestroy()
        {
            _matchResultViewRegistration?.Dispose();
            base.OnDestroy();
        }
    }
}
