using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 내 몸이 생기면 디버그 HUD + 조작판(<see cref="CardRushPadView"/>) + 정보 HUD(<see cref="RaceHudView"/>)를 연다.
    /// 매 틱 관전 대상을 새로 고른다 — 카메라(<see cref="CardRushCamera"/>)가 그 답을 읽는다.
    /// </summary>
    public class CardRushHudCoordinator : MessageHandlerBase, ITickable
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;
        private readonly ISpectateTargets spectate;

        private bool _opened;

        public CardRushHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            ISubscriber<EntityCreated> entityCreatedSubscriber, ISpectateTargets spectate)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.spectate = spectate;
        }

        protected override void Subscribe() => Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));

        //  관전 대상 고르기는 한 곳에서만 돌린다 — 두 곳에서 부르면 결승 뒤 머무는 시간이 두 배 빨리 흐른다.
        public void Tick() => spectate.Refresh();

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
