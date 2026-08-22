using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 디버그/유틸 HUD View. 화면 고정(Window 밴드, 표시 전용 / picking 통과).
    /// ViewModel에서 매 프레임 값을 pull해 라벨을 갱신한다(이벤트 없는 샘플링 값이라 R3 미사용 —
    /// 원본 TimeUI.Update() 폴링을 패널 스케줄러로 대체).
    /// </summary>
    public class DebugHudView : UIView
    {
        private readonly DebugHudViewModel _viewModel;

        private Label _tickText;
        private Label _serverTickText;
        private Label _leadText;
        private Label _elapsedText;
        private Label _rttText;
        private Label _reconLastText;
        private Label _reconAvgText;
        private Label _reconMaxText;
        private Label _timingAvgDText;
        private Label _timingMaxDText;
        private Label _timingPruneText;
        private Label _timingSeqGapText;
        private Label _snapshotFirstText;
        private Label _snapshotTickText;
        private Label _fpsText;
        private Label _entityCountText;
        private Label _snapLagText;
        private Label _snapIntervalText;
        private Label _cushionText;
        private Button _resetButton;
        private Button _dumpButton;

        private IVisualElementScheduledItem _tick;

        public DebugHudView(DebugHudViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _tickText = Root.Q<Label>("tick-text");
            _serverTickText = Root.Q<Label>("server-tick-text");
            _leadText = Root.Q<Label>("lead-text");
            _elapsedText = Root.Q<Label>("elapsed-text");
            _rttText = Root.Q<Label>("rtt-text");
            _reconLastText = Root.Q<Label>("recon-last-text");
            _reconAvgText = Root.Q<Label>("recon-avg-text");
            _reconMaxText = Root.Q<Label>("recon-max-text");
            _timingAvgDText = Root.Q<Label>("timing-avgd-text");
            _timingMaxDText = Root.Q<Label>("timing-maxd-text");
            _timingPruneText = Root.Q<Label>("timing-prune-text");
            _timingSeqGapText = Root.Q<Label>("timing-seqgap-text");
            _snapshotFirstText = Root.Q<Label>("snapshot-first-text");
            _snapshotTickText = Root.Q<Label>("snapshot-tick-text");
            _fpsText = Root.Q<Label>("fps-text");
            _entityCountText = Root.Q<Label>("entity-count-text");
            _snapLagText = Root.Q<Label>("snap-lag-text");
            _snapIntervalText = Root.Q<Label>("snap-interval-text");
            _cushionText = Root.Q<Label>("cushion-text");

            _resetButton = Root.Q<Button>("reset-button");
            _resetButton.clicked += _viewModel.ResetStats;

            _dumpButton = Root.Q<Button>("dump-button");
            _dumpButton.clicked += _viewModel.DumpStats;

            _tick = Root.schedule.Execute(Refresh).Every(0);
        }

        private void Refresh(TimerState _)
        {
            if (!_viewModel.IsRunning)
            {
                return;
            }

            _tickText.text = $"Client tick: {_viewModel.Tick}";
            _serverTickText.text = $"Server tick: {_viewModel.ServerTickEstimate}";
            _leadText.text = $"Lead: {_viewModel.Lead} tick";
            _elapsedText.text = $"Elapsed: {_viewModel.ElapsedTime:F2} s";
            _rttText.text = $"RTT: {_viewModel.RttMs:F0} ms";
            _reconLastText.text = $"Recon last: {_viewModel.ReconLast:F2} m";
            _reconAvgText.text = $"Recon avg: {_viewModel.ReconAverage:F2} m";
            _reconMaxText.text = $"Recon max: {_viewModel.ReconMax:F2} m (corr {_viewModel.CorrectionCount})";
            _timingAvgDText.text = $"d avg: {_viewModel.TimingAvgD:F1}";
            _timingMaxDText.text = $"d max: {_viewModel.TimingMaxD}";
            _timingPruneText.text = $"Prune: {_viewModel.TimingPrune}";
            _timingSeqGapText.text = $"SeqGap: {_viewModel.TimingSeqGap}";
            _snapshotFirstText.text = $"Snap first: {_viewModel.SnapshotFirstTick}";
            _snapshotTickText.text = $"Snap tick: {_viewModel.SnapshotLatestTick}";
            _fpsText.text = $"FPS: {_viewModel.Fps:F0} ({_viewModel.FrameMs:F1} ms)";
            _entityCountText.text = $"Entities: {_viewModel.EntityCount}";
            _snapLagText.text = $"Snap lag: {_viewModel.ServerTickLag} tick";
            _snapIntervalText.text = $"Snap gap: {_viewModel.SnapIntervalAvgMs:F1} / {_viewModel.SnapIntervalMaxMs:F1} ms";
            _cushionText.text = $"Cushion: {_viewModel.CushionMs:F1} ms";
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    if (_resetButton != null)
                    {
                        _resetButton.clicked -= _viewModel.ResetStats;
                    }
                    if (_dumpButton != null)
                    {
                        _dumpButton.clicked -= _viewModel.DumpStats;
                    }
                    _tick?.Pause();
                }
            }

            base.Dispose(disposing);
        }
    }
}
