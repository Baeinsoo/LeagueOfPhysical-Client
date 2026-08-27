using R3;

namespace LOP
{
    /// <summary>서버가 알려준 출발 예정과 준비 현황. 화면이 구독한다.</summary>
    public class MatchStartState
    {
        private readonly ReactiveProperty<long> _startTick = new(long.MaxValue);
        private readonly ReactiveProperty<int> _readyCount = new(0);
        private readonly ReactiveProperty<int> _totalCount = new(0);

        public ReadOnlyReactiveProperty<long> StartTick => _startTick;
        public ReadOnlyReactiveProperty<int> ReadyCount => _readyCount;
        public ReadOnlyReactiveProperty<int> TotalCount => _totalCount;

        public void Update(long tick, int ready, int total)
        {
            _startTick.Value = tick;
            _readyCount.Value = ready;
            _totalCount.Value = total;
        }
    }
}
