using System;
using R3;

namespace LOP
{
    /// <summary>부팅 단계 하나. Index는 0부터, Done이면 모든 단계가 끝났다.</summary>
    public readonly struct EntranceStep
    {
        public readonly int Index;
        public readonly int Count;
        public readonly string Label;
        public readonly bool Done;

        public EntranceStep(int index, int count, string label, bool done)
        {
            Index = index;
            Count = count;
            Label = label;
            Done = done;
        }

        public static EntranceStep None => new EntranceStep(-1, 0, string.Empty, false);
    }

    /// <summary>
    /// 부팅이 지금 어느 단계인지. EntranceScene이 단계마다 Begin, 다 끝나면 Complete를 부른다.
    /// 로그인처럼 사용자를 기다리는 동안은 AwaitingInput — 타이틀이 진행 막대 대신 버튼 자리를 비워 둔다.
    /// </summary>
    public sealed class EntranceProgress : IDisposable
    {
        private readonly ReactiveProperty<EntranceStep> _step = new(EntranceStep.None);
        private readonly ReactiveProperty<bool> _awaitingInput = new(false);

        public ReadOnlyReactiveProperty<EntranceStep> Step => _step;
        public ReadOnlyReactiveProperty<bool> AwaitingInput => _awaitingInput;

        public void Begin(int index, int count, string label) => _step.Value = new EntranceStep(index, count, label ?? string.Empty, false);

        public void Complete()
        {
            var s = _step.Value;
            _step.Value = new EntranceStep(s.Count - 1, s.Count, s.Label, true);
        }

        public void SetAwaitingInput(bool waiting) => _awaitingInput.Value = waiting;

        public void Dispose()
        {
            _step.Dispose();
            _awaitingInput.Dispose();
        }
    }
}
