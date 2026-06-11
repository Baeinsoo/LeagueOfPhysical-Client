using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 스탯 패널 View. 인게임 상시 표시(Window 밴드, 비모달). ViewModel의 R3 상태를 구독해 라벨/버튼을 갱신하고,
    /// 분배 버튼 클릭을 ViewModel 커맨드로 전달한다(폴링 없음).
    /// </summary>
    public class StatsView : UIView
    {
        private readonly StatsViewModel _viewModel;

        private Label _strength;
        private Label _dexterity;
        private Label _intelligence;
        private Label _vitality;
        private Label _statPoints;

        private Button _strengthButton;
        private Button _dexterityButton;
        private Button _intelligenceButton;
        private Button _vitalityButton;

        public StatsView(StatsViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _strength = Root.Q<Label>("strength-value");
            _dexterity = Root.Q<Label>("dexterity-value");
            _intelligence = Root.Q<Label>("intelligence-value");
            _vitality = Root.Q<Label>("vitality-value");
            _statPoints = Root.Q<Label>("statpoints-value");

            _strengthButton = Root.Q<Button>("strength-button");
            _dexterityButton = Root.Q<Button>("dexterity-button");
            _intelligenceButton = Root.Q<Button>("intelligence-button");
            _vitalityButton = Root.Q<Button>("vitality-button");

            Disposables.Add(_viewModel.Strength.Subscribe(v => _strength.text = v.ToString()));
            Disposables.Add(_viewModel.Dexterity.Subscribe(v => _dexterity.text = v.ToString()));
            Disposables.Add(_viewModel.Intelligence.Subscribe(v => _intelligence.text = v.ToString()));
            Disposables.Add(_viewModel.Vitality.Subscribe(v => _vitality.text = v.ToString()));
            Disposables.Add(_viewModel.StatPoints.Subscribe(v => _statPoints.text = v.ToString()));

            Disposables.Add(_viewModel.CanAllocate.Subscribe(SetButtonsVisible));

            _strengthButton.clicked += OnStrengthClicked;
            _dexterityButton.clicked += OnDexterityClicked;
            _intelligenceButton.clicked += OnIntelligenceClicked;
            _vitalityButton.clicked += OnVitalityClicked;
        }

        public override void OnClose()
        {
            if (_strengthButton != null) _strengthButton.clicked -= OnStrengthClicked;
            if (_dexterityButton != null) _dexterityButton.clicked -= OnDexterityClicked;
            if (_intelligenceButton != null) _intelligenceButton.clicked -= OnIntelligenceClicked;
            if (_vitalityButton != null) _vitalityButton.clicked -= OnVitalityClicked;

            base.OnClose();
        }

        private void OnStrengthClicked() => _viewModel.Allocate(nameof(StatsComponent.strength));
        private void OnDexterityClicked() => _viewModel.Allocate(nameof(StatsComponent.dexterity));
        private void OnIntelligenceClicked() => _viewModel.Allocate(nameof(StatsComponent.intelligence));
        private void OnVitalityClicked() => _viewModel.Allocate(nameof(StatsComponent.vitality));

        private void SetButtonsVisible(bool visible)
        {
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _strengthButton.style.display = display;
            _dexterityButton.style.display = display;
            _intelligenceButton.style.display = display;
            _vitalityButton.style.display = display;
        }

        public override void Dispose()
        {
            _viewModel.Dispose();
            base.Dispose();
        }
    }
}
