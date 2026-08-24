using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 출발 전 안내를 화면 가운데 큰 글자로 띄운다. 아래 입력을 막지 않는다 —
    /// 카운트다운 중 탭은 월드가 어차피 무시하므로 굳이 차단할 이유가 없다.
    /// </summary>
    public class RaceStartView : UIView
    {
        private readonly RaceStartViewModel _viewModel;

        private Label _text;
        private IVisualElementScheduledItem _tick;

        public RaceStartView(RaceStartViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        //  Notification이 아니라 Window인 이유: 카운트다운은 토스트가 아니라 게임 화면이라,
        //  로딩 같은 전체화면 오버레이(Loading 밴드)에 **가려져야** 한다. Notification은 그보다
        //  위 밴드라 로딩 위에 그려졌다. 같은 Window 밴드의 FlapPad·DebugHud보다는 나중에
        //  열리므로 그 위에 뜬다.
        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _text = Root.Q<Label>("race-start-text");

            //  UIView에는 Update가 없다 — 패널 스케줄러로 매 프레임 값을 가져온다
            //  (카운트다운은 변경 이벤트가 없는 샘플링 값이라 DebugHudView와 같은 방식).
            //  문구가 없어지면 display:None으로 "숨길" 뿐 창을 닫지 않는다 — 스케줄러는 레이스가
            //  끝날 때까지 계속 돌며(값을 매 프레임 확인), 창을 닫는 건 윈도우 매니저의 몫이다.
            _tick = Root.schedule.Execute(_ =>
            {
                string text = _viewModel.CurrentText();
                _text.text = text;
                Root.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }).Every(0);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed == false)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                    _tick = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
