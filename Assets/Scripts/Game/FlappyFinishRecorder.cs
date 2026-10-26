using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 완주 시간을 기록지에 적는 때(spec §7, 옛 리뷰 M-1·M-3). 순수 계산이라 EditMode로 고정한다.
    /// <list type="bullet">
    /// <item>서버 등수(<see cref="FinishPlacement"/> &gt; 0)와 내 결승 틱이 <b>둘 다</b> 있는 첫 장에 한 번 — 예측 결승 틱만 믿으면
    /// 되감기(기절 오보정 등)에 1초 넘게 빠른 가짜 최고가 기기에 영구히 남을 수 있다.</item>
    /// <item>처음 본 장에 이미 등수가 있으면(결승 직후 재접속) 안 적는다 — 이번 스코프가 본 판이 아니다.</item>
    /// <item>값 = (결승 틱 − 출발 틱) × 틱 길이 — 정보 HUD 시계가 결승에서 멈춘 값(<see cref="RaceClock"/>)과 같다.</item>
    /// </list>
    /// </summary>
    public sealed class FinishRecordGate
    {
        private bool _seen;
        private bool _done;

        /// <summary>이번 장에 적을 완주 시간(초). 적을 때가 아니면 null. 판마다 한 번.</summary>
        public double? Observe(int placement, long finishedTick, long startTick, double secondsPerTick)
        {
            bool first = _seen == false;
            _seen = true;
            if (_done || placement <= 0)
            {
                return null;
            }
            if (first)
            {
                _done = true;
                return null;
            }
            if (finishedTick == FinishState.NotFinished || startTick == long.MaxValue || secondsPerTick <= 0d)
            {
                return null;
            }
            _done = true;
            return RaceClock.ElapsedSeconds(0d, startTick, secondsPerTick, finishedTick);
        }
    }

    /// <summary>
    /// 플래피 완주 시간 → 기록지(spec §7). 매 장 내 새의 등수·결승 틱을 <see cref="FinishRecordGate"/>에 넣는다. 판정과 무관하다.
    /// <para>"나"는 <see cref="IGameDataStore.userEntityId"/>로 본다(정보 HUD와 같은 이유 — 몸이 사라져도 정체는 남는다).
    /// 클라 시뮬은 내 새만 굴리므로 <see cref="FinishState"/>는 내 것만 믿을 수 있다.</para>
    /// </summary>
    public sealed class FlappyFinishRecorder : ITickable
    {
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly MatchStartState _matchStart;
        private readonly GameFramework.Runner.IRunner _runner;
        private readonly MatchRecordSheet _sheet;
        private readonly FinishRecordGate _gate = new FinishRecordGate();

        public FlappyFinishRecorder(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
                                    MatchStartState matchStart, GameFramework.Runner.IRunner runner, MatchRecordSheet sheet)
        {
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
            _matchStart = matchStart;
            _runner = runner;
            _sheet = sheet;
        }

        public void Tick()
        {
            //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
            string id = _gameDataStore.userEntityId;
            var mine = string.IsNullOrEmpty(id) ? null : _entityRegistry.Get(id);
            if (mine == null)
            {
                return;
            }
            double? seconds = _gate.Observe(
                mine.Get<FinishPlacement>()?.Value ?? 0,
                mine.Get<FinishState>()?.FinishedTick ?? FinishState.NotFinished,
                _matchStart.StartTick.CurrentValue,
                _runner.tickUpdater?.interval ?? 0d);
            if (seconds.HasValue)
            {
                _sheet.Set(FlappyRecordMetrics.FinishTime, seconds.Value);
            }
        }
    }
}
