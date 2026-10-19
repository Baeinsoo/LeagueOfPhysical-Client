using GameFramework;
using GameFramework.Runner;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : LifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private LOPNetworkManager networkManager;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(room);
            builder.RegisterComponent(networkManager);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);
            builder.Register<IPlayerContext, PlayerContext>(Lifetime.Singleton);
            builder.Register<GameDataStore>(Lifetime.Singleton).As<IGameDataStore, IDataStore>();

            // 룸 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<RoomSessionMessageHandler>();

            builder.Register<IGameFactory, LOPGameFactory>(Lifetime.Singleton);

            //  경기 중 내 연결 끊김 감시 — 끊기면 덮개를 띄우고 같은 판에 다시 들어간다.
            builder.RegisterEntryPoint<MatchConnectionWatcher>().AsSelf();

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
            });
            #endregion
        }
    }
}
