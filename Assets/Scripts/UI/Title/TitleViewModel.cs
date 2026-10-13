using System;
using R3;

namespace LOP.UI
{
    /// <summary>타이틀 화면 상태 — 진행 막대 비율·단계 글자·막대 표시 여부·LIVE 띠 문구.</summary>
    public sealed class TitleViewModel : IDisposable
    {
        public const string TickerBooting = "선수 입장 준비 중 · 잠시 후 경기장으로 이동합니다";
        public const string TickerAwaiting = "새 선수를 기다립니다 · 입장을 눌러 주세요";
        private const string NotStarted = "준비 중";

        private readonly CompositeDisposable _disposables = new();

        public ReadOnlyReactiveProperty<float> Ratio { get; }
        public ReadOnlyReactiveProperty<string> StepText { get; }
        public ReadOnlyReactiveProperty<bool> ShowProgress { get; }
        public ReadOnlyReactiveProperty<string> TickerText { get; }

        public TitleViewModel(EntranceProgress progress)
        {
            Ratio = progress.Step.Select(RatioOf).ToReadOnlyReactiveProperty().AddTo(_disposables);
            StepText = progress.Step.Select(TextOf).ToReadOnlyReactiveProperty().AddTo(_disposables);
            ShowProgress = progress.AwaitingInput.Select(w => !w).ToReadOnlyReactiveProperty().AddTo(_disposables);
            TickerText = progress.AwaitingInput.Select(w => w ? TickerAwaiting : TickerBooting).ToReadOnlyReactiveProperty().AddTo(_disposables);
        }

        /// <summary>시작한 단계 수 / 전체. 끝났을 때만 1.</summary>
        public static float RatioOf(EntranceStep step)
        {
            if (step.Done) return 1f;
            if (step.Count <= 0 || step.Index < 0) return 0f;
            float r = (step.Index + 1) / (float)step.Count;
            //  마지막 단계가 진행 중일 때 100%로 보이면 끝난 것처럼 보인다 — 끝나기 전엔 조금 덜 채운다.
            return r >= 1f ? 0.95f : r;
        }

        public static string TextOf(EntranceStep step)
        {
            if (step.Count <= 0 || step.Index < 0) return NotStarted;
            return $"{step.Label} · {step.Index + 1} / {step.Count}";
        }

        public void Dispose() => _disposables.Dispose();
    }
}
