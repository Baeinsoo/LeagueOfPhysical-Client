using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>타이틀 화면 바인더. 진행 막대·단계 글자·LIVE 띠를 VM 값으로 그린다. 로그인·연결 실패는 이 위에 따로 뜬다.</summary>
    public class TitleView : UIView
    {
        private readonly TitleViewModel _vm;

        public TitleView(TitleViewModel vm) => _vm = vm;

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();
            var fill = Root.Q("title-progress-fill");
            var step = Root.Q<Label>("title-step");
            var group = Root.Q("title-progress");
            var ticker = Root.Q<Label>("ticker-text");
            Root.Q<Label>("title-version").text = $"버전 {UnityEngine.Application.version}";

            _vm.Ratio.Subscribe(r => KitProgress.SetRatio(fill, r)).AddTo(Disposables);
            _vm.StepText.Subscribe(t => step.text = t).AddTo(Disposables);
            _vm.ShowProgress.Subscribe(show => group.style.display = show ? DisplayStyle.Flex : DisplayStyle.None).AddTo(Disposables);
            //  띠 글자는 고정 문구 둘이라 1300 폭 안에 들어간다 — 말줄임 없이 그대로.
            _vm.TickerText.Subscribe(t => ticker.text = t).AddTo(Disposables);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing) _vm.Dispose();
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
