using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>내 차례와 판 상황 두 줄. 남은 시간·뒤집힌 개수가 매 프레임 변하므로 스케줄러로 갱신한다.</summary>
    public class PanchigiTurnView : UIView
    {
        //  약할 땐 차분한 파랑, 셀수록 더운 노랑 — 높이만으로는 곁눈질에 잘 안 읽힌다.
        private static readonly Color Calm = new Color(0.36f, 0.58f, 0.78f);
        private static readonly Color Hot = new Color(0.85f, 0.64f, 0.25f);

        //  판과 게이지 사이 틈, 그리고 화면 왼쪽 끝에서 최소한 띄울 거리.
        private const float BoardGap = 12f;
        private const float EdgeMargin = 8f;

        private readonly PanchigiTurnViewModel _viewModel;

        private IVisualElementScheduledItem _tick;

        public PanchigiTurnView(PanchigiTurnViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var turnLabel = Root.Q<Label>("turn-label");
            var flipLabel = Root.Q<Label>("flip-label");
            var dropOutLabel = Root.Q<Label>("dropout-label");
            var chargeTrack = Root.Q<VisualElement>("charge-track");
            var chargeFill = Root.Q<VisualElement>("charge-fill");
            _tick = Root.schedule.Execute(_ =>
            {
                turnLabel.text = _viewModel.Label();
                flipLabel.text = _viewModel.FlipLabel();
                dropOutLabel.text = _viewModel.DropOutLabel();

                bool charging = _viewModel.IsCharging();
                chargeTrack.style.display = charging ? DisplayStyle.Flex : DisplayStyle.None;
                if (charging)
                {
                    PlaceBesideBoard(chargeTrack);

                    float t = _viewModel.Charge();
                    chargeFill.style.height = Length.Percent(t * 100f);
                    chargeFill.style.backgroundColor = Color.Lerp(Calm, Hot, t);
                }
            }).Every(0);
        }

        //  판 왼쪽 옆에 붙인다. USS에 적힌 자리는 판을 못 찾았을 때의 대비책으로만 남는다.
        private void PlaceBesideBoard(VisualElement track)
        {
            if (_viewModel.TryGetBoardScreenRect(out Rect board) == false || track.panel == null)
            {
                return;
            }

            //  화면 좌표는 원점이 왼쪽 *아래*, 패널 좌표는 왼쪽 *위*다 — 위아래가 뒤집힌다.
            Vector2 top = RuntimePanelUtils.ScreenToPanel(track.panel, new Vector2(board.xMin, board.yMax));
            Vector2 bottom = RuntimePanelUtils.ScreenToPanel(track.panel, new Vector2(board.xMin, board.yMin));

            float width = track.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f)
            {
                return;   // 아직 레이아웃 전 — 다음 프레임에 자리를 잡는다
            }

            //  판에 딱 붙으면 동전과 겹쳐 보인다. 화면이 좁아 왼쪽으로 넘치면 가장자리에서 멈춘다.
            float left = Mathf.Max(EdgeMargin, board.xMin > 0f ? top.x - width - BoardGap : EdgeMargin);
            track.style.left = left;
            track.style.top = top.y;
            track.style.height = Mathf.Max(1f, bottom.y - top.y);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                }
            }

            base.Dispose(disposing);
        }
    }
}
