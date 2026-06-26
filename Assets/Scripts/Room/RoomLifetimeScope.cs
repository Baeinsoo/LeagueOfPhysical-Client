using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : SceneLifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private LOPNetworkManager networkManager;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(room);
            builder.RegisterComponent(networkManager);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);
            builder.Register<IPlayerContext, PlayerContext>(Lifetime.Singleton);
            builder.Register<GameDataStore>(Lifetime.Singleton).As<IGameDataStore, IDataStore>();

            // 룸 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameMessageHandler>();

            builder.Register<IGameFactory, LOPGameFactory>(Lifetime.Singleton);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
            });
            #endregion
        }
    }
}
