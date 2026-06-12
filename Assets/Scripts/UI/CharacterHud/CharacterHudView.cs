using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 플레이어 HUD View. 화면 고정(Window 밴드, 스크린스페이스). ViewModel의 R3 상태를 구독해
    /// HP/MP/EXP 바와 레벨을 갱신하는 얇은 바인더(폴링 없음).
    /// </summary>
    public class CharacterHudView : UIView
    {
        private readonly CharacterHudViewModel _viewModel;

        private VisualElement _hpFill;
        private VisualElement _mpFill;
        private VisualElement _expFill;
        private Label _hpText;
        private Label _mpText;
        private Label _expText;
        private Label _levelText;

        public CharacterHudView(CharacterHudViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _hpFill = Root.Q<VisualElement>("hp-fill");
            _mpFill = Root.Q<VisualElement>("mp-fill");
            _expFill = Root.Q<VisualElement>("exp-fill");
            _hpText = Root.Q<Label>("hp-text");
            _mpText = Root.Q<Label>("mp-text");
            _expText = Root.Q<Label>("exp-text");
            _levelText = Root.Q<Label>("level-text");

            Disposables.Add(_viewModel.Hp.Subscribe(_ => RefreshHp()));
            Disposables.Add(_viewModel.MaxHp.Subscribe(_ => RefreshHp()));
            Disposables.Add(_viewModel.Mp.Subscribe(_ => RefreshMp()));
            Disposables.Add(_viewModel.MaxMp.Subscribe(_ => RefreshMp()));
            Disposables.Add(_viewModel.Exp.Subscribe(_ => RefreshExp()));
            Disposables.Add(_viewModel.ExpToNext.Subscribe(_ => RefreshExp()));
            Disposables.Add(_viewModel.Level.Subscribe(level =>
            {
                if (_levelText != null) _levelText.text = $"Lv {level}";
            }));
        }

        private void RefreshHp() => SetBar(_hpFill, _hpText, _viewModel.Hp.CurrentValue, _viewModel.MaxHp.CurrentValue);
        private void RefreshMp() => SetBar(_mpFill, _mpText, _viewModel.Mp.CurrentValue, _viewModel.MaxMp.CurrentValue);
        private void RefreshExp() => SetBar(_expFill, _expText, _viewModel.Exp.CurrentValue, _viewModel.ExpToNext.CurrentValue);

        private static void SetBar(VisualElement fill, Label text, long current, long max)
        {
            float percent = max > 0 ? Mathf.Clamp01((float)current / max) * 100f : 0f;
            if (fill != null) fill.style.width = Length.Percent(percent);
            if (text != null) text.text = $"{current} / {max}";
        }

        public override void Dispose()
        {
            _viewModel.Dispose();
            base.Dispose();
        }
    }
}
