using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>투석기 사냥 HUD. 보여 주기만 하는 얇은 바인더 — 매 프레임 ViewModel 값을 옮긴다.</summary>
    public class ArcheryCatapultHudView : UIView
    {
        private readonly ArcheryCatapultHudViewModel _viewModel;
        private Label _round, _introTitle, _introDetail;
        private VisualElement _timeFill, _cue, _stun, _stunFill, _popups, _result, _resultList, _intro;
        private int? _drawnRows;
        private IVisualElementScheduledItem _tick;

        public ArcheryCatapultHudView(ArcheryCatapultHudViewModel viewModel) { _viewModel = viewModel; }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();
            _round = Root.Q<Label>("round");
            _timeFill = Root.Q<VisualElement>("time-fill");
            _cue = Root.Q<VisualElement>("cue");
            Root.Q<Label>("cue").text = ArcheryCatapultHudText.CueText;
            _stun = Root.Q<VisualElement>("stun");
            _stunFill = Root.Q<VisualElement>("stun-fill");
            _popups = Root.Q<VisualElement>("popups");
            _result = Root.Q<VisualElement>("result");
            _resultList = Root.Q<VisualElement>("result-list");
            _intro = Root.Q<VisualElement>("intro");
            _introTitle = Root.Q<Label>("intro-title");
            _introDetail = Root.Q<Label>("intro-detail");
            _tick = Root.schedule.Execute(_ => Refresh()).Every(0);
        }

        private void Refresh()
        {
            _viewModel.Tick(Time.time);
            _round.text = _viewModel.RoundLabel;
            _timeFill.style.width = new StyleLength(new Length(_viewModel.TimeLeft01 * 100f, LengthUnit.Percent));
            Show(_cue, _viewModel.CueVisible);
            Show(_stun, _viewModel.StunVisible);
            _stunFill.style.width = new StyleLength(new Length(_viewModel.StunLeft01 * 100f, LengthUnit.Percent));
            Show(_intro, _viewModel.IntroVisible);
            _introTitle.text = _viewModel.IntroTitle;
            _introDetail.text = _viewModel.IntroDetail;
            Show(_result, _viewModel.ResultVisible);
            if (_viewModel.ResultVisible) { DrawRows(); } else { _drawnRows = null; }

            while (_viewModel.TryTakePopup(out var world, out var color, out var text, out var style))
            {
                SpawnPopup(world, color, text, style);
            }
        }

        //  줄마다 (이름, 누계, 이번 라운드)를 모두 섞은 값 — 늦게 온 스냅샷이 아래 줄 하나만 바꿔도 다시 그린다.
        private static int Signature(IReadOnlyList<ArcheryCatapultHudViewModel.Row> rows)
        {
            unchecked
            {
                int hash = 17;
                foreach (var row in rows)
                {
                    hash = hash * 31 + (row.Name?.GetHashCode() ?? 0);
                    hash = hash * 31 + row.Total;
                    hash = hash * 31 + row.Gain;
                }
                return hash * 31 + rows.Count;
            }
        }

        private void DrawRows()
        {
            var rows = _viewModel.ResultRows;
            int signature = Signature(rows);
            if (signature == _drawnRows) { return; }
            _drawnRows = signature;
            _resultList.Clear();
            foreach (var row in rows)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("catapult-result-row");
                var name = new Label(row.Name) { pickingMode = PickingMode.Ignore };
                name.style.color = row.Color;
                line.Add(name);
                line.Add(new Label($"{row.Total}점 ({ArcheryCatapultHudText.GainText(row.Gain)})") { pickingMode = PickingMode.Ignore });
                _resultList.Add(line);
            }
        }

        //  터진 자리에 쏜 사람 색 고리 + 글. 표적은 계속 날아가지만 글은 맞은 자리에 남아야 "어디서 뺏겼나"가 읽힌다.
        //  대박은 글·고리를 크게 오래 띄우고, 종의 "땡~"은 점수 팝업 위에 글만 띄운다(고리가 둘이면 어지럽다).
        private void SpawnPopup(Vector3 world, Color color, string text, ArcheryCatapultPopupStyle style)
        {
            int ms = ArcheryCatapultHudText.DurationMs(style);
            bool jackpot = style == ArcheryCatapultPopupStyle.Jackpot;
            bool withRing = style != ArcheryCatapultPopupStyle.Ding;
            var camera = _viewModel.Camera;
            if (camera == null || Screen.width <= 0 || Screen.height <= 0) { return; }
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) { return; }   // 카메라 뒤
            var at = new Vector2(screen.x / Screen.width * Root.layout.width,
                                 (1f - screen.y / Screen.height) * Root.layout.height);

            VisualElement ring = null;
            if (withRing)
            {
                ring = new VisualElement { pickingMode = PickingMode.Ignore };
                ring.AddToClassList("catapult-ring");
                ring.style.left = at.x; ring.style.top = at.y;
                ring.style.borderTopColor = ring.style.borderBottomColor = ring.style.borderLeftColor = ring.style.borderRightColor = color;
                _popups.Add(ring);
            }
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("catapult-popup");
            string cls = ArcheryCatapultHudText.ClassOf(style);
            if (cls.Length > 0) { label.AddToClassList(cls); }
            label.style.color = color;
            label.style.left = at.x; label.style.top = at.y - 18f;
            _popups.Add(label);

            var list = new List<StylePropertyName> { "top", "opacity", "width", "height" };
            var time = new List<TimeValue> { new TimeValue(ms, TimeUnit.Millisecond), new TimeValue(ms, TimeUnit.Millisecond),
                                             new TimeValue(ms, TimeUnit.Millisecond), new TimeValue(ms, TimeUnit.Millisecond) };
            label.style.transitionProperty = new StyleList<StylePropertyName>(list);
            label.style.transitionDuration = new StyleList<TimeValue>(time);
            if (ring != null)
            {
                ring.style.transitionProperty = new StyleList<StylePropertyName>(list);
                ring.style.transitionDuration = new StyleList<TimeValue>(time);
            }
            float rise = jackpot ? 48f : 33f;      // 시제품: 1.1초에 30 px 남짓 떠오른다, 대박은 더 높이
            float ringEnd = jackpot ? 180f : 100f;
            label.schedule.Execute(() =>
            {
                label.style.top = at.y - 18f - rise;
                label.style.opacity = 0f;
                if (ring != null)
                {
                    ring.style.width = ringEnd; ring.style.height = ringEnd;
                    ring.style.opacity = 0f;
                }
            }).StartingIn(16);
            label.schedule.Execute(() => { ring?.RemoveFromHierarchy(); label.RemoveFromHierarchy(); }).StartingIn(ms + 50);
        }

        private static void Show(VisualElement element, bool visible)
            => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

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
