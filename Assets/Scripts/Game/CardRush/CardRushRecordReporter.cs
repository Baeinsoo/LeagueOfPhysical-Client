using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 카드 러시의 개인 기록 → 기록지(spec §7). 판정과 무관하다(읽기만 한다).
    /// <list type="bullet">
    /// <item>완주 시간 — <see cref="FinishRecordGate"/>를 그대로 재사용한다(Flappy와 같은 넷코드 모양이라 같은 함정이 있다 —
    /// 리뷰 판정 11-02). 서버 등수(<see cref="FinishPlacement"/> &gt; 0)와 월드가 적은 결승 틱(<see cref="FinishState.FinishedTick"/>,
    /// <c>CardRushWorld</c>가 완주 순간 <see cref="CardRushRunnerState.MoveStartTick"/>을 그대로 옮겨 적는다)이 둘 다 있는 첫
    /// 장에 한 번만 적는다. 처음 본 장에 이미 둘 다 있으면(재접속) 이번 스코프가 본 판이 아니라 안 적고, 등수가 끝까지
    /// 없으면(DNF) 영원히 안 적는다.</item>
    /// <item>연속 정타 최고 — <see cref="CardRushRunnerState.BestStreak"/>은 이미 보정으로도 줄지 않는 서버 확정 최댓값이지만
    /// (Task 8 리뷰 — 예측 중인 <c>Misses</c>는 서버보다 커질 수 있어도 <c>BestStreak</c>는 max로 맞춘다), 여기서도 본 값의 최댓값만
    /// 적어 재접속·되감기로 그 값이 한 틱이라도 흔들려도 기록지는 거꾸로 가지 않는다.</item>
    /// </list>
    /// <para>"나"는 <see cref="IGameDataStore.userEntityId"/>로 본다(정보 HUD·<c>FlappyFinishRecorder</c>와 같은 이유 —
    /// 몸이 사라져도 정체는 남는다).</para>
    /// </summary>
    public sealed class CardRushRecordReporter : ITickable
    {
        //  브리프가 고정한 틱 길이(spec §7) — 이 종목은 러너의 tickUpdater 없이도 돈다.
        private const double SecondsPerTick = 0.02d;

        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly MatchStartState _matchStart;
        private readonly MatchRecordSheet _sheet;
        private readonly FinishRecordGate _finishGate = new FinishRecordGate();
        private int _bestStreak;

        public CardRushRecordReporter(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
                                      MatchStartState matchStart, MatchRecordSheet sheet)
        {
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
            _matchStart = matchStart;
            _sheet = sheet;
        }

        public void Tick()
        {
            //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
            string id = _gameDataStore.userEntityId;
            var mine = string.IsNullOrEmpty(id) ? null : _entityRegistry.Get(id);
            var runner = mine?.Get<CardRushRunner>();
            if (runner == null)
            {
                return;
            }

            int streak = runner.State.BestStreak;
            if (streak > _bestStreak)
            {
                _bestStreak = streak;
                _sheet.Set(CardRushRecordMetrics.StreakBest, _bestStreak);
            }

            int placement = mine.Get<FinishPlacement>()?.Value ?? 0;
            long finishedTick = mine.Get<FinishState>()?.FinishedTick ?? FinishState.NotFinished;
            long startTick = _matchStart.StartTick.CurrentValue;
            double? seconds = _finishGate.Observe(placement, finishedTick, startTick, SecondsPerTick);
            if (seconds.HasValue)
            {
                _sheet.Set(CardRushRecordMetrics.FinishTime, seconds.Value);
            }
        }
    }
}
