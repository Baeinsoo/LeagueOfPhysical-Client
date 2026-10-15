using Cysharp.Threading.Tasks;
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
        private IDisposable _changeDisplayNameViewRegistration;
        private IDisposable _kitToastViewRegistration;
        private IDisposable _gameSelectViewRegistration;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<MatchStateMachine>(Lifetime.Transient);

            // 각 상태를 Transient로 + 그 Func<T> 팩토리를 함께 등록한다. 상태 생성자가 IObjectResolver(서비스
            // 로케이터) 대신 Func<다음상태>로 전이 대상을 명시할 수 있게 함 — VContainer는 암묵적 Func<T>를
            // 제공하지 않아 팩토리를 명시 등록해야 한다.
            RegisterState<CheckMatch>(builder);
            RegisterState<Idle>(builder);
            RegisterState<RequestMatchmaking>(builder);
            RegisterState<InMatchmaking>(builder);
            RegisterState<InGameRoom>(builder);
            RegisterState<CancelMatchmaking>(builder);

            // 고를 수 있는 게임 목록. 마스터데이터를 한 번 훑어 만들어 두므로 Scoped면 충분하다.
            builder.Register<PlayableGameProvider>(Lifetime.Scoped);

            // 로비 전체가 보는 내 재화·레벨 단일 진실원본. Scoped — 코인·레벨 칩(LobbyHomeView)과
            // 상점(구매·로드아웃 응답 반영)이 같은 인스턴스를 공유해야 값이 어긋나지 않는다.
            builder.Register<EconomyStore>(Lifetime.Scoped);

            //  출전 연출 지휘. 생성자에서 로딩판 문을 건다 — 빌드 콜백 첫 줄에서 바로 만들어 둔다.
            builder.Register<RunOutDirector>(Lifetime.Scoped).AsSelf().As<IRunOutDirector>();

            // 마지막으로 플레이한 게임·맵 기억(기기 로컬). VM이 생성될 때 복원해 쓴다.
            builder.Register<LastPlayedSelectionStore>(Lifetime.Scoped);

            // VM은 Scoped — LobbyHomeView(Play)와 Coordinator가 같은 인스턴스를 공유해야 신호가 이어진다.
            builder.Register<MatchmakingViewModel>(Lifetime.Scoped);
            builder.Register<LobbyHomeView>(Lifetime.Transient);
            builder.RegisterEntryPoint<MatchmakingCoordinator>();

            // 프론트엔드 네비(상점/설정/프로필). VM은 Scoped — LobbyHomeView와 FrontEndCoordinator가 공유한다.
            builder.Register<LobbyHomeViewModel>(Lifetime.Scoped);
            //  View와 함께 Transient — 상점을 열 때마다 그 시점의 스토어 값으로 품목 상태를 다시 센다.
            builder.Register<ShopViewModel>(Lifetime.Transient);
            builder.Register<ShopView>(Lifetime.Transient);
            builder.Register<SettingsView>(Lifetime.Transient);
            // View와 함께 Transient — 프로필을 열 때마다 전적을 다시 받아온다.
            builder.Register<ProfileViewModel>(Lifetime.Transient);
            builder.Register<ProfileView>(Lifetime.Transient);
            //  View와 함께 Transient — 열 때마다 그 시점의 이름을 입력란 시작값으로 읽어야 한다.
            builder.Register<ChangeDisplayNameViewModel>(Lifetime.Transient);
            builder.Register<ChangeDisplayNameView>(Lifetime.Transient);
            builder.Register<KitToastView>(Lifetime.Transient);

            //  종목 선택은 열 때마다 새 VM — 연 순간의 로비 선택에서 시작하고, 고르기를 눌러야만 로비에 넘긴다.
            builder.Register(resolver =>
            {
                var matchmaking = resolver.Resolve<MatchmakingViewModel>();
                return new GameSelectViewModel(
                    matchmaking.Games,
                    matchmaking.SelectedGameIndex.CurrentValue,
                    matchmaking.SelectedMapIndex.CurrentValue,
                    (game, map) => { matchmaking.SelectGame(game); matchmaking.SelectMap(map); });
            }, Lifetime.Transient);
            builder.Register<GameSelectView>(Lifetime.Transient);
            builder.RegisterEntryPoint<FrontEndCoordinator>();

            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<RunOutDirector>();
                container.InjectSceneObjects(gameObject.scene);

                // 전역 WindowManager에 LobbyHomeView 팩토리 기여: Open<LobbyHomeView>가 이 스코프 resolver로
                // 생성 → MatchmakingViewModel 주입. 로비 진입 시 허브 화면을 연다.
                var windowManager = container.Resolve<IWindowManager>();
                _lobbyHomeViewRegistration = windowManager.RegisterViewFactory<LobbyHomeView>(() => container.Resolve<LobbyHomeView>());

                // 셸도 같은 방식으로 기여: FrontEndCoordinator의 Open<T>가 이 스코프 resolver로 셸을 만든다.
                _shopViewRegistration = windowManager.RegisterViewFactory<ShopView>(() => container.Resolve<ShopView>());
                _settingsViewRegistration = windowManager.RegisterViewFactory<SettingsView>(() => container.Resolve<SettingsView>());
                _profileViewRegistration = windowManager.RegisterViewFactory<ProfileView>(() => container.Resolve<ProfileView>());
                _changeDisplayNameViewRegistration = windowManager.RegisterViewFactory<ChangeDisplayNameView>(() => container.Resolve<ChangeDisplayNameView>());
                _kitToastViewRegistration = windowManager.RegisterViewFactory<KitToastView>(() => container.Resolve<KitToastView>());
                _gameSelectViewRegistration = windowManager.RegisterViewFactory<GameSelectView>(() => container.Resolve<GameSelectView>());

                //  로비 가운데 3D 무대. 씬 파일을 고치지 않으려고 여기서 만든다 — 씬과 함께 사라진다.
                var stage = new UnityEngine.GameObject("LobbyStage").AddComponent<LobbyStage>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stage.gameObject, gameObject.scene);
                container.InjectGameObject(stage.gameObject);

                windowManager.Open<LobbyHomeView>();

                //  로비 들어올 때 한 번 받아 둔다 — 코인·레벨 칩이 바로 뜨려면 열기 전에 기다릴 필요
                //  없이 기본값(0코인·Lv1)으로 먼저 보이고 도착하면 갱신되는 쪽을 택한다.
                container.Resolve<EconomyStore>().RefreshAsync(destroyCancellationToken).Forget();
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
            _changeDisplayNameViewRegistration?.Dispose();
            _kitToastViewRegistration?.Dispose();
            _gameSelectViewRegistration?.Dispose();
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
