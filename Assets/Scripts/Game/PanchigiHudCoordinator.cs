using GameFramework;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 판치기는 아바타가 없어 EntityCreated로 내 캐릭을 기다릴 수 없다 —
    /// 첫 상태 메시지가 오면 턴 화면을 연다.
    /// </summary>
    public class PanchigiHudCoordinator : MessageHandlerBase
    {
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<PanchigiStateToC> subscriber;

        private bool opened;

        public PanchigiHudCoordinator(IWindowManager windowManager, ISubscriber<PanchigiStateToC> subscriber)
        {
            this.windowManager = windowManager;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(_ => Open()));

        private void Open()
        {
            if (opened)
            {
                return;
            }

            windowManager.Open<LOP.UI.PanchigiTurnView>();
            opened = true;
        }
    }
}
