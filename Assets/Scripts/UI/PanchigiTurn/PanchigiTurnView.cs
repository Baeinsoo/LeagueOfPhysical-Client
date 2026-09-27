using System.Collections.Generic;
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

        //  판과 게이지 사이 틈, 화면 왼쪽 끝에서 최소한 띄울 거리, 그리고 판 높이 대비 막대 길이.
        //  막대는 판만큼 길 필요가 없다 — 짧아야 눈이 한 번에 담는다.
        private const float BoardGap = 32f;
        private const float EdgeMargin = 8f;
        private const float HeightRatio = 2f / 3f;

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
            var scoreboard = Root.Q<VisualElement>("scoreboard");
            var chargeTrack = Root.Q<VisualElement>("charge-track");
            var chargeFill = Root.Q<VisualElement>("charge-fill");
            _tick = Root.schedule.Execute(_ =>
            {
                turnLabel.text = _viewModel.Label();
                RenderScoreboard(scoreboard, _viewModel.Rows());

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

        //  줄 수·칸 수가 같으면 라벨 글자만 바꾼다 — 매 프레임 요소를 새로 만들면 UI Toolkit 레이아웃이 매번 다시 돈다.
        private static void RenderScoreboard(VisualElement board, IReadOnlyList<PanchigiTurnViewModel.ScoreRow> rows)
        {
            if (rows.Count > 0 && board.childCount > 0 && board[0].Q($"frame-{rows[0].Frames.Count - 1}") == null)
            {
                board.Clear();   // 프레임 수가 바뀌었다
            }
            while (board.childCount > rows.Count) { board.RemoveAt(board.childCount - 1); }
            while (board.childCount < rows.Count) { board.Add(NewRow(rows[0].Frames.Count)); }

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                VisualElement line = board[r];
                line.EnableInClassList("score-row--current", row.Current);
                line.Q("chip").style.backgroundColor = row.Color;
                line.Q<Label>("name").text = row.Name;
                for (int f = 0; f < row.Frames.Count; f++)
                {
                    VisualElement cell = line.Q($"frame-{f}");
                    cell.Q<Label>("marks").text = string.Join(" ", row.Frames[f].Marks);
                    cell.Q<Label>("cum").text = row.Frames[f].Cumulative?.ToString() ?? string.Empty;
                }
                line.Q<Label>("total").text = row.Total.ToString();
            }
        }

        private static VisualElement NewRow(int frameCount)
        {
            var line = new VisualElement();
            line.AddToClassList("score-row");
            var chip = new VisualElement { name = "chip" };
            chip.AddToClassList("score-chip");
            line.Add(chip);
            var name = new Label { name = "name" };
            name.AddToClassList("score-name");
            line.Add(name);
            for (int f = 0; f < frameCount; f++)
            {
                var cell = new VisualElement { name = $"frame-{f}" };
                cell.AddToClassList("score-frame");
                var marks = new Label { name = "marks" };
                marks.AddToClassList("score-marks");
                var cum = new Label { name = "cum" };
                cum.AddToClassList("score-cum");
                cell.Add(marks);
                cell.Add(cum);
                line.Add(cell);
            }
            var total = new Label { name = "total" };
            total.AddToClassList("score-total");
            line.Add(total);
            return line;
        }

        //  판 왼쪽 옆에 붙인다. 값이 하나라도 수상하면 아무것도 안 쓰고 USS에 적힌 자리를 그대로 둔다 —
        //  잘못 쓰면 막대가 화면 밖으로 나가 아예 안 보인다.
        private void PlaceBesideBoard(VisualElement track)
        {
            if (_viewModel.TryGetBoardScreenRect(out Rect board) == false)
            {
                return;
            }

            //  이 View의 루트는 화면을 꽉 채우게 깔린다(WindowManager가 absolute 0,0,0,0으로 세운다).
            //  그래서 화면 비율을 그대로 곱하면 패널 좌표가 된다 — 좌표 변환 API의 y 방향에 기대지 않는다.
            VisualElement panel = track.parent;
            if (panel == null) { return; }

            float panelWidth = panel.resolvedStyle.width;
            float panelHeight = panel.resolvedStyle.height;
            float trackWidth = track.resolvedStyle.width;
            if (panelWidth <= 0f || panelHeight <= 0f || trackWidth <= 0f
                || Screen.width <= 0 || Screen.height <= 0)
            {
                return;   // 아직 레이아웃 전 — 다음 프레임에 자리를 잡는다
            }

            float boardLeft = board.xMin / Screen.width * panelWidth;
            //  화면 좌표는 원점이 왼쪽 *아래*, 패널 좌표는 왼쪽 *위*다 — 위아래가 뒤집힌다.
            float boardTop = (1f - board.yMax / Screen.height) * panelHeight;
            float boardBottom = (1f - board.yMin / Screen.height) * panelHeight;

            float left = boardLeft - trackWidth - BoardGap;
            float boardSpan = boardBottom - boardTop;
            if (float.IsNaN(left) || float.IsNaN(boardTop) || boardSpan <= 0f)
            {
                return;
            }

            //  짧게 만들되 판 한가운데에 맞춰 세운다 — 위쪽만 남기면 판과 어긋나 보인다.
            float height = boardSpan * HeightRatio;
            float top = boardTop + (boardSpan - height) * 0.5f;

            //  판이 화면 왼쪽에 바짝 붙어 자리가 없으면 가장자리에서 멈춘다.
            track.style.left = Mathf.Max(EdgeMargin, left);
            track.style.top = Mathf.Clamp(top, 0f, panelHeight - 1f);
            track.style.height = Mathf.Min(height, panelHeight);
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
