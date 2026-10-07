using GameFramework;
using LOP.UI;
using R3;

namespace LOP
{
    /// <summary>판 도중 끊김·재접속 알림을 어떻게 띄울지 — 스코프가 정한다(피하기는 자막이라 끈다).</summary>
    public sealed class PresenceDisplaySettings
    {
        public readonly bool Toast;
        public PresenceDisplaySettings(bool toast) => Toast = toast;
    }

    /// <summary>
    /// 끊김·재접속이 생기면 위쪽 토스트에 한 줄 띄운다. 토스트 화면은 첫 알림 때 한 번 연다 —
    /// 아무도 안 끊긴 판엔 화면이 아예 없다.
    /// </summary>
    public class PresenceToastCoordinator : MessageHandlerBase
    {
        private readonly PlayerPresenceStore store;
        private readonly IWindowManager windowManager;
        private readonly PresenceDisplaySettings settings;
        private PresenceToastView view;

        public PresenceToastCoordinator(PlayerPresenceStore store, IWindowManager windowManager, PresenceDisplaySettings settings)
        {
            this.store = store;
            this.windowManager = windowManager;
            this.settings = settings;
        }

        protected override void Subscribe()
        {
            if (settings.Toast == false) return;
            Track(store.Changes.Subscribe(change =>
            {
                view ??= windowManager.Open<PresenceToastView>();
                view.Show(PresenceText.For(change));
            }));
        }
    }
}
