using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 내 새가 생기면 Flappy 인게임 화면(입력면 + 디버그 HUD)을 연다.
    /// 엔티티 생성과 화면 띄우기를 분리한다 — 화면 교체는 "큰 흐름"이라 코디네이터 책임
    /// (아키텍처 가이드라인 "흐름의 경계"). FlapWang의 <see cref="PlayerHudCoordinator"/>와 같은 짝이다.
    /// </summary>
    public class FlappyHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private bool _opened;

        public FlappyHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            ISubscriber<EntityCreated> entityCreatedSubscriber)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
        }

        protected override void Subscribe() => Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 입력면을 먼저 열어 Window 밴드 최하단에 깐다(전체화면이라 위 위젯 입력을 막지 않도록).
            windowManager.Open<FlapPadView>();
            windowManager.Open<DebugHudView>();
            _opened = true;
        }
    }
}
