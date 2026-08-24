using GameFramework;
using LOP.UI;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 앱 전역 메시지 버스. 메시지 타입별 브로커는 RegisterOrderedMessageBroker<T>로 명시
            // 등록한다(IL2CPP open-generic 미지원 대비).
            //
            // MessagePipe 기본 브로커(RegisterMessageBroker)를 쓰지 않는 이유: 그쪽은 핸들러를 부르는
            // 순서가 구독 순서와 어긋날 수 있다 — 구독 해제된 자리를 재사용하기 때문에, 매치를 몇 판
            // 반복하면 나중에 구독한 쪽이 먼저 불린다. 우리는 한 메시지를 여러 구독자가 나눠 받으므로
            // (예: GameInfoToC를 넷이 받는다) 그 순서가 곧 동작이다. OrderedMessageBroker는 구독 순서를
            // 보장한다. 발행·구독 인터페이스(IPublisher/ISubscriber)는 MessagePipe 것 그대로다.
            //
            // RegisterMessagePipe 자체는 남긴다 — GlobalMessagePipe가 쓰는 IServiceProvider 등록이 여기 있다.
            builder.RegisterMessagePipe();

            // WebResponse — 정적 코드(WebAPI)가 GlobalMessagePipe로 발행하므로 SetProvider 필요.
            builder.RegisterOrderedMessageBroker<CreateUserResponse>();
            builder.RegisterOrderedMessageBroker<GetUserResponse>();
            builder.RegisterOrderedMessageBroker<GetUserLocationResponse>();
            builder.RegisterOrderedMessageBroker<GetUserRatingResponse>();
            builder.RegisterOrderedMessageBroker<UpdateUserProfileResponse>();
            builder.RegisterOrderedMessageBroker<ChangeDisplayNameResponse>();
            builder.RegisterOrderedMessageBroker<GetMatchResponse>();
            builder.RegisterOrderedMessageBroker<RoomJoinableResponse>();

            // 엔티티 라이프사이클
            builder.RegisterOrderedMessageBroker<Event.Entity.EntityCreated>();
            builder.RegisterOrderedMessageBroker<Event.Entity.EntityDestroyed>();

            // 네트워크 수신(NetworkMessageDispatcher가 발행 → MessageHandler가 구독)
            builder.RegisterOrderedMessageBroker<GameInfoToC>();
            builder.RegisterOrderedMessageBroker<WorldEventBatchToC>();
            builder.RegisterOrderedMessageBroker<EntitySnapsToC>();
            builder.RegisterOrderedMessageBroker<EntitySpawnToC>();
            builder.RegisterOrderedMessageBroker<EntityDespawnToC>();
            builder.RegisterOrderedMessageBroker<UserEntitySnapToC>();
            builder.RegisterOrderedMessageBroker<StatAllocationToC>();
            builder.RegisterOrderedMessageBroker<InputTimingToC>();
            builder.RegisterOrderedMessageBroker<MatchEndedToC>();
            builder.Register<NetworkMessageDispatcher>(Lifetime.Singleton);

            // 엔티티별 이벤트(keyed, 키=entityId)
            builder.RegisterOrderedMessageBroker<string, Event.Entity.EntityDamage>();
            builder.RegisterOrderedMessageBroker<string, Event.Entity.AbilityActivated>();
            builder.RegisterOrderedMessageBroker<string, Event.Entity.EntityHealthChanged>();
            builder.RegisterOrderedMessageBroker<string, Event.Entity.EntityManaChanged>();
            builder.RegisterOrderedMessageBroker<string, Event.Entity.EntityLevelChanged>();
            builder.RegisterOrderedMessageBroker<string, Event.Entity.EntityStatPointsChanged>();
            builder.RegisterOrderedMessageBroker<string, Event.Entity.EntityStatChanged>();

            builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);

            //  자격증명 보관소는 프로필(인스턴스)마다 키가 달라야 해서 인스턴스로 등록한다.
            builder.RegisterInstance<GameFramework.Auth.IAuthCredentialStore>(
                new GameFramework.Auth.PlayerPrefsAuthCredentialStore("LOP.Auth", GameFramework.Auth.AuthProfile.Current));
            builder.Register<AuthenticationService>(Lifetime.Singleton)
                .As<GameFramework.Http.IAccessTokenProvider>()
                .AsSelf();

            builder.Register<UserDataStore>(Lifetime.Singleton)
                .As<IUserDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<MatchmakingDataStore>(Lifetime.Singleton)
                .As<IMatchmakingDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<RoomDataStore>(Lifetime.Singleton)
                .As<IRoomDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<MatchResultDataStore>(Lifetime.Singleton)
                .As<IMatchResultDataStore>()
                .As<IDataStore>()
                .AsSelf();

            //  유저 위치를 물어보는 유일한 곳. 로딩 VM이 Root 싱글턴이고 씬 경계(로비→룸)를 넘으므로
            //  이것도 Root에 둔다. 로비 스코프의 FSM 상태들이 이 인스턴스를 주입받는다.
            builder.Register<UserLocationService>(Lifetime.Singleton)
                .As<IUserLocationService>()
                .AsSelf();

            builder.Register<RoomConnector>(Lifetime.Transient);

            // 앱-플로우 씬 페이즈 FSM(Root). IStartable로 앱 시작 시 Start()되어 Boot 진입.
            // AsSelf로 자식 스코프(Entrance/Lobby/Room)가 주입받아 신호를 Fire할 수 있게 한다.
            builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
            builder.RegisterEntryPoint<AppStateMachine>().AsSelf();

            new UIInstaller().Install(builder);

            // 매치 진입 로딩 화면(룸 연결~게임 준비 구간을 연속으로 덮음).
            // VM은 UserLocationService의 위치를 관찰해 IsLoading을 파생하고,
            // 코디네이터가 그 신호로 로딩 창을 여닫는다(씬 경계를 넘어 뷰를 소유).
            builder.Register<MatchLoadingViewModel>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MatchLoadingCoordinator>();

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
                // 정적/비-DI 코드(WebAPI)가 GlobalMessagePipe.GetPublisher<T>로 발행할 수 있도록 provider 설정.
                GlobalMessagePipe.SetProvider(container.AsServiceProvider());

                //  모든 REST 요청이 현재 세션 토큰을 싣도록 WebAPI에 공급자를 꽂는다.
                WebAPI.SetAccessTokenProvider(container.Resolve<AuthenticationService>());
            });
            #endregion
        }
    }
}
