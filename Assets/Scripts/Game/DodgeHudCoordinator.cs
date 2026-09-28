using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>내 몸이 생기면 Dodge 조작 패드와 디버그 HUD를 연다. SkydiveHudCoordinator와 같은 짝이다.</summary>
    public class DodgeHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private bool _opened;

        public DodgeHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
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

            // 조작면을 먼저 열어 Window 밴드 맨 아래에 깐다(전체화면이라 위 위젯 입력을 막지 않게).
            windowManager.Open<DodgePadView>();
            windowManager.Open<DebugHudView>();
            _opened = true;
        }
    }
}
