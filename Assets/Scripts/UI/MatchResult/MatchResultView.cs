using R3;
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
        private System.IDisposable _rankSubscription;

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
            _rankSubscription?.Dispose();
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

            //  무승부면 "매치 종료" 자리에 그렇게 적는다 — 등수만 보면 공동 1등이 이긴 것처럼 읽힌다.
            var message = Root.Q<Label>("matchresult-message");
            if (_viewModel.IsDraw)
            {
                message.text = "무승부";
            }
            else
            {
                message.style.display = DisplayStyle.None;
            }

            foreach (var row in _viewModel.Rows)
            {
                var line = new VisualElement();
                line.AddToClassList("matchresult-row");
                if (row.IsMe) line.AddToClassList("matchresult-row--me");

                var placement = new Label(MatchResultViewModel.FormatPlacement(row.Placement, row.IsDraw));
                placement.AddToClassList("card-text");

                var name = new Label(row.DisplayName);
                name.AddToClassList("card-text");

                line.Add(placement);
                line.Add(name);

                //  점수 없는 모드는 이 자리가 아예 안 보여야 한다 — 빈칸도 "0점"도 아니라 없는 것이다.
                if (row.HasScore)
                {
                    var score = new Label(MatchResultViewModel.FormatScore(row.Score, row.Gained, row.Lost));
                    score.AddToClassList("card-text");
                    score.AddToClassList("matchresult-row-score");
                    line.Add(score);
                }

                container.Add(line);
            }
        }

        //  랭크 줄은 로비 조회가 도착해야 생긴다. 빈 동안(캐주얼 포함)은 숨긴다.
        private void BuildRating()
        {
            var rating = Root.Q<VisualElement>("matchresult-rating");
            var value = Root.Q<Label>("matchresult-rating-value");

            _rankSubscription = _viewModel.RankLine.Subscribe(line =>
            {
                value.text = line;
                rating.style.display = string.IsNullOrEmpty(line) ? DisplayStyle.None : DisplayStyle.Flex;
            });
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    //  VContainer는 Transient를 추적하지 않는다 — WindowManager.Close가 View를 dispose하므로 VM 정리는 여기서
                    //  (ProfileView와 같은 방식). 닫힌 뒤 도착한 응답이 죽은 화면에 쓰지 않게 조회도 끊는다.
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void OnConfirmClicked() => _onConfirm?.Invoke();
    }
}
