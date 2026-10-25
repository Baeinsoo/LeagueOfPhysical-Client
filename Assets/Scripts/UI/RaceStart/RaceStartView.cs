using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 출발 전 안내를 화면 가운데 큰 글자로 띄운다. 플래피는 spec §3대로 3·2·1이 숫자마다 1.5배에서 팝, GO!는 초록으로 0.8초에 걸쳐
    /// 사라진다(변형 클래스 --pop·--go). 기마 궁술은 예전 모양 그대로(<see cref="IRaceStartLook"/> — 사용자 결정 10-25).
    /// 아래 입력을 막지 않는다 — 카운트다운 중 탭은 월드가 어차피 무시하므로 굳이 차단할 이유가 없다.
    /// </summary>
    public class RaceStartView : UIView
    {
        private const string PopClass = "race-start-text--pop";
        private const string GoClass = "race-start-text--go";

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

            //  UIView에는 Update가 없다 — 패널 스케줄러로 매 프레임 값을 가져온다.
            //  문구가 없어지면 display:None으로 "숨길" 뿐 창을 닫지 않는다 — 창을 닫는 건 윈도우 매니저의 몫이다.
            _tick = Root.schedule.Execute(_ => Draw()).Every(0);
        }

        private void Draw()
        {
            _viewModel.Refresh();
            string text = _viewModel.Text;
            bool show = string.IsNullOrEmpty(text) == false;
            Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show == false)
            {
                return;
            }
            _text.text = text;
            //  예전 모양(기마 궁술)이면 IsPop·IsGo가 늘 거짓, 크기·투명도 늘 1 — 기본 규칙 그대로 보인다.
            _text.EnableInClassList(PopClass, _viewModel.IsPop);
            _text.EnableInClassList(GoClass, _viewModel.IsGo);
            float s = _viewModel.TextScale;
            _text.style.scale = new Scale(new Vector3(s, s, 1f));
            _text.style.opacity = _viewModel.Alpha;
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
