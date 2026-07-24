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
            // 앱 전역 메시지 버스(MessagePipe). 메시지 타입별 브로커는 각 마이그레이션 슬라이스에서
            // RegisterMessageBroker<T>로 명시 등록한다(IL2CPP open-generic 미지원 대비).
            var options = builder.RegisterMessagePipe();

            // WebResponse — 정적 인터셉터(LOPWebRequestInterceptor)가 GlobalMessagePipe로 발행하므로 SetProvider 필요.
            builder.RegisterMessageBroker<CreateUserResponse>(options);
            builder.RegisterMessageBroker<GetUserResponse>(options);
            builder.RegisterMessageBroker<GetUserLocationResponse>(options);
            builder.RegisterMessageBroker<GetUserStatsResponse>(options);
            builder.RegisterMessageBroker<UpdateUserProfileResponse>(options);
            builder.RegisterMessageBroker<GetMatchResponse>(options);
            builder.RegisterMessageBroker<RoomJoinableResponse>(options);

            // 엔티티 라이프사이클
            builder.RegisterMessageBroker<Event.Entity.EntityCreated>(options);
            builder.RegisterMessageBroker<Event.Entity.EntityDestroyed>(options);

            // 네트워크 수신(NetworkMessageDispatcher가 발행 → MessageHandler가 구독)
            builder.RegisterMessageBroker<GameInfoToC>(options);
            builder.RegisterMessageBroker<WorldEventBatchToC>(options);
            builder.RegisterMessageBroker<EntitySnapsToC>(options);
            builder.RegisterMessageBroker<EntitySpawnToC>(options);
            builder.RegisterMessageBroker<EntityDespawnToC>(options);
            builder.RegisterMessageBroker<UserEntitySnapToC>(options);
            builder.RegisterMessageBroker<StatAllocationToC>(options);
            builder.RegisterMessageBroker<InputSequenceToC>(options);
            builder.RegisterMessageBroker<InputTimingToC>(options);
            builder.RegisterMessageBroker<MatchEndedToC>(options);
            builder.Register<NetworkMessageDispatcher>(Lifetime.Singleton);

            // 엔티티별 이벤트(keyed, 키=entityId)
            builder.RegisterMessageBroker<string, Event.Entity.EntityDamage>(options);
            builder.RegisterMessageBroker<string, Event.Entity.AbilityActivated>(options);
            builder.RegisterMessageBroker<string, Event.Entity.EntityHealthChanged>(options);
            builder.RegisterMessageBroker<string, Event.Entity.EntityManaChanged>(options);
            builder.RegisterMessageBroker<string, Event.Entity.EntityLevelChanged>(options);
            builder.RegisterMessageBroker<string, Event.Entity.EntityStatPointsChanged>(options);
            builder.RegisterMessageBroker<string, Event.Entity.EntityStatChanged>(options);

            builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);

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

            builder.Register<RoomConnector>(Lifetime.Transient);

            new UIInstaller().Install(builder);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
                // 정적/비-DI 코드(웹 인터셉터)가 GlobalMessagePipe.GetPublisher<T>로 발행할 수 있도록 provider 설정.
                GlobalMessagePipe.SetProvider(container.AsServiceProvider());
            });
            #endregion
        }
    }
}
