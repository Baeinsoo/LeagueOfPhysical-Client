using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>매칭 대기 오버레이. 큐 이름·기다린 시간 + 취소 버튼. 여는 쪽이 SetQueueName·SetCancelCallback으로 배선한다.</summary>
    public class MatchingWaitingView : UIView
    {
        // LOP.Action(MonoBehaviour 컴포넌트)이 System.Action을 가리므로 풀 한정한다.
        private Button _cancelButton;
        private System.Action _onCancel;
        private Label _queueLabel;
        private Label _elapsedLabel;
        private string _queueName = string.Empty;
        private float _openedAt;
        private IVisualElementScheduledItem _ticker;

        public override UILayer Layer => UILayer.Loading;
        public override bool BlocksUnderlyingInput => true;

        public void SetCancelCallback(System.Action onCancel) => _onCancel = onCancel;

        /// <summary>"랭크 매칭 중"처럼 어느 큐인지. 랭크는 오래 걸릴 수 있어 무엇을 기다리는지 보여 준다.</summary>
        public void SetQueueName(string queueName)
        {
            _queueName = queueName ?? string.Empty;
            if (_queueLabel != null) _queueLabel.text = $"{_queueName} 매칭 중";
        }

        /// <summary>"m:ss".</summary>
        public static string FormatElapsed(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        public override void OnOpen()
        {
            base.OnOpen();

            _cancelButton = Root.Q<Button>("cancel-button");
            _cancelButton.clicked += OnCancelClicked;

            _queueLabel = Root.Q<Label>("matching-queue");
            _elapsedLabel = Root.Q<Label>("matching-elapsed");
            SetQueueName(_queueName);

            _openedAt = UnityEngine.Time.realtimeSinceStartup;
            UpdateElapsed();
            _ticker = Root.schedule.Execute(UpdateElapsed).Every(1000);
        }

        public override void OnClose()
        {
            if (_cancelButton != null) _cancelButton.clicked -= OnCancelClicked;
            _ticker?.Pause();
            base.OnClose();
        }

        private void OnCancelClicked() => _onCancel?.Invoke();

        private void UpdateElapsed()
        {
            if (_elapsedLabel == null) return;
            _elapsedLabel.text = FormatElapsed((int)(UnityEngine.Time.realtimeSinceStartup - _openedAt));
        }
    }
}
