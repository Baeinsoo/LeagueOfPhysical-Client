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
            builder.RegisterMessagePipe();

            builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);

            builder.Register<UserDataStore>(Lifetime.Singleton)
                .As<IUserDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<MatchMakingDataStore>(Lifetime.Singleton)
                .As<IMatchMakingDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<RoomDataStore>(Lifetime.Singleton)
                .As<IRoomDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<RoomConnector>(Lifetime.Transient);

            new UIInstaller().Install(builder);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
            });
            #endregion
        }
    }
}
