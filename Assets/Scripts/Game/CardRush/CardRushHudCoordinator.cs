using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>내 몸이 생기면 디버그 HUD + 조작판(<see cref="CardRushPadView"/>) + 정보 HUD(<see cref="RaceHudView"/>)를 연다.</summary>
    public class CardRushHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private bool _opened;

        public CardRushHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
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
            windowManager.Open<DebugHudView>();
            windowManager.Open<CardRushPadView>();
            windowManager.Open<RaceHudView>();
            _opened = true;
        }
    }
}
