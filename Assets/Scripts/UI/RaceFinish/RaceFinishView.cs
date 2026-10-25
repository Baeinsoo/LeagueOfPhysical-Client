using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 결승 도장(spec §3): 결승하는 순간 FINISH!(흰 글자·검은 외곽, −3.5°)가 1.8배에서 찍히고, 0.45초 뒤 순위 도장(순위색 바탕 +
    /// 검은 글자, +3°, 1위면 "우승!"). 도장이 사라지면 위쪽 작은 판에 "n위 · 완주". 그 뒤로는 지금처럼 관전 막대.
    ///
    /// <para>Notification이 아니라 Window인 이유는 <see cref="RaceStartView"/>와 같다:
    /// 토스트가 아니라 게임 화면이라 로딩·결과 같은 전체화면 오버레이에 <b>가려져야</b> 한다.</para>
    /// </summary>
    public class RaceFinishView : UIView
    {
        private readonly RaceFinishViewModel _viewModel;

        private Label _stamp;
        private VisualElement _rankStamp;
        private Label _rankSub;
        private Label _rankText;
        private VisualElement _settled;
        private Label _place;
        private IVisualElementScheduledItem _tick;

        public RaceFinishView(RaceFinishViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _stamp = Root.Q<Label>("race-finish-stamp");
            _rankStamp = Root.Q<VisualElement>("race-finish-rank-stamp");
            _rankSub = Root.Q<Label>("race-finish-rank-sub");
            _rankText = Root.Q<Label>("race-finish-rank-text");
            _settled = Root.Q<VisualElement>("race-finish-settled");
            _place = Root.Q<Label>("race-finish-place");

            //  여는 프레임에 바로 한 번 — 결승 소리와 1.8배 도장이 한 프레임 늦지 않게.
            Draw();
            //  값은 변경 알림이 없는 샘플링 값이라 매 프레임 읽는다(RaceStartView와 같은 방식).
            _tick = Root.schedule.Execute(Draw).Every(0);
        }

        private void Draw()
        {
            _viewModel.Refresh(Time.deltaTime);

            float a = _viewModel.FinishAlpha;
            _stamp.style.display = a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            _stamp.style.opacity = a;
            _stamp.style.scale = new Scale(new Vector3(_viewModel.FinishScale, _viewModel.FinishScale, 1f));

            _rankStamp.style.display = _viewModel.RankStampVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _rankStamp.style.opacity = _viewModel.RankAlpha;
            _rankStamp.style.scale = new Scale(new Vector3(_viewModel.RankScale, _viewModel.RankScale, 1f));
            _rankStamp.style.backgroundColor = _viewModel.RankColor;
            _rankSub.style.display = _viewModel.Winner ? DisplayStyle.Flex : DisplayStyle.None;
            _rankText.text = _viewModel.RankText;

            _settled.style.display = _viewModel.ShowSettled ? DisplayStyle.Flex : DisplayStyle.None;
            _place.text = _viewModel.PlacementText;
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
