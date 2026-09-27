using System.Collections.Generic;
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
        private Label _resultHeadline;
        private VisualElement _resultList;
        private ArcheryShootOffResultPanel _face;
        private int _pinCount;
        private int _drawnResultVersion = -1;
        private IVisualElementScheduledItem _tick;
        private VisualElement _flash;
        private VisualElement _bullPopups;

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
            _resultHeadline = Root.Q<Label>("result-headline");
            _resultList = Root.Q<VisualElement>("result-list");
            _face = new ArcheryShootOffResultPanel();
            _face.style.flexGrow = 1f;
            Root.Q<VisualElement>("result-face").Add(_face);
            _flash = Root.Q<VisualElement>("flash");
            _bullPopups = Root.Q<VisualElement>("bull-popups");

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

            bool showResult = _viewModel.ResultVisible;
            _result.style.display = showResult ? DisplayStyle.Flex : DisplayStyle.None;
            if (_viewModel.ResultVersion != _drawnResultVersion)
            {
                _drawnResultVersion = _viewModel.ResultVersion;
                DrawResult();
            }
            if (showResult)
            {
                _face.SetShown(ArcheryShootOffResultLayout.PinsShown(_viewModel.ResultSeconds, _pinCount));
            }

            _flash.style.opacity = _viewModel.FlashAlpha;
            while (_viewModel.TryTakeBull(out var world, out var color))
            {
                SpawnBullPopup(world, color);
            }
        }

        //  남의 10점 — 맞은 자리에서 "10!!"이 선수 색으로 떠오르며 사라진다.
        private void SpawnBullPopup(Vector3 world, Color color)
        {
            var camera = _viewModel.Camera;
            if (camera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                return;   // 카메라 뒤
            }
            var at = new Vector2(screen.x / Screen.width * Root.layout.width,
                                 (1f - screen.y / Screen.height) * Root.layout.height);

            var label = new Label("10!!");
            label.AddToClassList("shootoff-bull-popup");
            label.pickingMode = PickingMode.Ignore;
            label.style.color = color;
            label.style.left = at.x;
            label.style.top = at.y;
            _bullPopups.Add(label);

            int durationMs = Mathf.RoundToInt(ArcheryBullseyeFx.PopupSeconds * 1000f);
            label.style.transitionProperty = new StyleList<StylePropertyName>(
                new List<StylePropertyName> { "top", "opacity" });
            label.style.transitionDuration = new StyleList<TimeValue>(
                new List<TimeValue> { new TimeValue(durationMs, TimeUnit.Millisecond), new TimeValue(durationMs, TimeUnit.Millisecond) });
            label.schedule.Execute(() =>
            {
                label.style.top = at.y - 70f;
                label.style.opacity = 0f;
            }).ExecuteLater(16);
            label.schedule.Execute(() => label.RemoveFromHierarchy()).ExecuteLater(durationMs + 120);
        }

        private void DrawResult()
        {
            var byRank = _viewModel.ResultByRank;
            float radius = _viewModel.ResultFaceRadius;

            _resultHeadline.text = _viewModel.ResultHeadline;
            _face.SetFace(radius, _viewModel.FaceBands, ArcheryShootOffResultLayout.ViewRadius(byRank, radius));

            var pins = new List<(Vector2, Color)>();
            foreach (int i in ArcheryShootOffResultLayout.PinOrder(byRank, radius))
            {
                pins.Add((byRank[i].FaceOffset, _viewModel.ColorOf(byRank[i].ShooterId)));
            }
            _face.SetPins(pins);
            _pinCount = pins.Count;

            _resultList.Clear();
            foreach (var p in byRank)
            {
                var row = new VisualElement();
                row.AddToClassList("shootoff-result-row");
                row.EnableInClassList("is-me", _viewModel.ResultName(p.ShooterId) == "나");
                row.pickingMode = PickingMode.Ignore;

                var dot = new VisualElement();
                dot.AddToClassList("shootoff-result-dot");
                dot.style.backgroundColor = _viewModel.ColorOf(p.ShooterId);
                dot.pickingMode = PickingMode.Ignore;
                row.Add(dot);

                row.Add(Text(ArcheryShootOffResultLayout.RankLabel(p.Rank)));
                row.Add(Text(_viewModel.ResultName(p.ShooterId)));
                row.Add(Text(ArcheryShootOffResultLayout.PointsText(p.Points, _viewModel.ResultMultiplier)));
                row.Add(Text(ArcheryShootOffResultLayout.DistanceText(p.Hit, p.Distance)));
                _resultList.Add(row);
            }
        }

        private static Label Text(string text)
        {
            var label = new Label(text);
            label.AddToClassList("shootoff-result-text");
            label.pickingMode = PickingMode.Ignore;
            return label;
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
