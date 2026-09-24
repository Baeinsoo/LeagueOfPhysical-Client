using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>한 발 승부 HUD. 보여 주기만 하는 얇은 바인더 — 매 프레임 ViewModel 값을 읽어 옮긴다.</summary>
    public class ArcheryShootOffHudView : UIView
    {
        private readonly ArcheryShootOffHudViewModel _viewModel;
        private Label _round;
        private VisualElement _timeFill;
        private Label _windArrow;
        private Label _windText;
        private Label _caption;
        private VisualElement _result;
        private int _drawnResultVersion = -1;
        private IVisualElementScheduledItem _tick;

        public ArcheryShootOffHudView(ArcheryShootOffHudViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _round = Root.Q<Label>("round");
            _timeFill = Root.Q<VisualElement>("time-fill");
            _windArrow = Root.Q<Label>("wind-arrow");
            _windText = Root.Q<Label>("wind-text");
            _caption = Root.Q<Label>("caption");
            _result = Root.Q<VisualElement>("result");

            _tick = Root.schedule.Execute(_ => Refresh()).Every(0);
        }

        private void Refresh()
        {
            _viewModel.Tick(Time.time);

            _round.text = _viewModel.RoundLabel;
            _timeFill.style.width = new StyleLength(new Length(_viewModel.TimeLeft01 * 100f, LengthUnit.Percent));

            float wind = _viewModel.WindArrow;
            //  테마 글꼴(Jua)에 화살표 글자가 없어 꺾쇠로 그린다. 셀수록 개수도 크기도 는다.
            int marks = wind == 0f ? 0 : 1 + Mathf.RoundToInt(Mathf.Abs(wind) * 2f);
            _windArrow.text = new string(wind > 0f ? '>' : '<', marks);
            _windArrow.style.fontSize = Mathf.Lerp(20f, 44f, Mathf.Abs(wind));
            _windText.text = _viewModel.WindText;

            string caption = _viewModel.Caption;
            _caption.text = caption;
            _caption.style.display = string.IsNullOrEmpty(caption) ? DisplayStyle.None : DisplayStyle.Flex;

            _result.style.display = _viewModel.ResultVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_viewModel.ResultVersion != _drawnResultVersion)
            {
                _drawnResultVersion = _viewModel.ResultVersion;
                DrawResult();
            }
        }

        private void DrawResult()
        {
            _result.Clear();
            var rows = _viewModel.ResultRows;
            for (int i = 0; i < rows.Count; i++)
            {
                var (name, points, detail) = rows[i];
                var label = new Label($"{name}   +{points}   {detail}");
                label.AddToClassList("shootoff-result-row");
                label.EnableInClassList("is-first", i == 0 && points > 0);
                label.pickingMode = PickingMode.Ignore;
                _result.Add(label);
            }
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _tick?.Pause();
                if (disposing)
                {
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
