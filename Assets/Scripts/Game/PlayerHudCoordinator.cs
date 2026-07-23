using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 로컬 유저 엔티티가 생성되면 인게임 HUD(GamePad/Stats/Hud/Debug)를 연다.
    /// 엔티티 생성(<see cref="CharacterCreator"/>)과 UI 네비게이션을 분리한다 — creator는 엔티티만 만들고,
    /// 화면 띄우기는 "큰 흐름(네비게이션)"이라 코디네이터 책임(아키텍처 가이드라인 "흐름의 경계").
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>)를 구독해 로컬 유저일 때 1회 연다.
    /// 닫기는 게임 스코프 teardown(WindowManager 팩토리 해제)이 담당.
    /// </summary>
    public class PlayerHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private bool _opened;

        public PlayerHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager, ISubscriber<EntityCreated> entityCreatedSubscriber)
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

            // GamePad를 먼저 열어 Window 밴드 최하단에 깐다(전체화면 카메라 드래그 배경이 위 UI 위젯 입력을 막지 않도록).
            windowManager.Open<GamePadView>();
            windowManager.Open<StatsView>();
            windowManager.Open<CharacterHudView>();
            windowManager.Open<DebugHudView>();
            _opened = true;
        }
    }
}
