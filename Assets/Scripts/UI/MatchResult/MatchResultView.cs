using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 매치 결과 화면. ViewModel이 만들어 둔 줄 목록과 점수를 그리고, [확인]으로 닫는다.
    /// 여는 쪽(FrontEndCoordinator)이 SetConfirmCallback으로 닫기 동작을 배선한다.
    /// </summary>
    public class MatchResultView : UIView
    {
        private readonly MatchResultViewModel _viewModel;

        // LOP.Action(MonoBehaviour 컴포넌트)이 System.Action을 가리므로 풀 한정한다.
        private Button _confirmButton;
        private System.Action _onConfirm;

        public MatchResultView(MatchResultViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public void SetConfirmCallback(System.Action onConfirm) => _onConfirm = onConfirm;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("confirm-button");
            _confirmButton.clicked += OnConfirmClicked;

            BuildRows();
            BuildRating();
        }

        public override void OnClose()
        {
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmClicked;
            base.OnClose();
        }

        private void BuildRows()
        {
            var container = Root.Q<VisualElement>("matchresult-rows");

            //  보고가 실패한 판은 등수가 없다. 그때는 예전처럼 "매치 종료"만 남긴다.
            if (_viewModel.Rows.Count == 0)
            {
                container.style.display = DisplayStyle.None;
                return;
            }

            Root.Q<Label>("matchresult-message").style.display = DisplayStyle.None;

            foreach (var row in _viewModel.Rows)
            {
                var line = new VisualElement();
                line.AddToClassList("matchresult-row");
                if (row.IsMe) line.AddToClassList("matchresult-row--me");

                var placement = new Label($"{row.Placement}등");
                placement.AddToClassList("card-text");

                var name = new Label(row.DisplayName);
                name.AddToClassList("card-text");

                line.Add(placement);
                line.Add(name);
                container.Add(line);
            }
        }

        private void BuildRating()
        {
            var rating = Root.Q<VisualElement>("matchresult-rating");

            if (!_viewModel.HasRatingChange)
            {
                rating.style.display = DisplayStyle.None;
                return;
            }

            Root.Q<Label>("matchresult-rating-value").text = _viewModel.RatingText;
        }

        private void OnConfirmClicked() => _onConfirm?.Invoke();
    }
}
