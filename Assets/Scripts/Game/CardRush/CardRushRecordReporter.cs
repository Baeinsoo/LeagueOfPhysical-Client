using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 카드 러시의 개인 기록 → 기록지(spec §7). 판정과 무관하다(읽기만 한다).
    /// <list type="bullet">
    /// <item>완주 시간 — 서버가 등수를 확정하면(<see cref="FinishPlacement"/> &gt; 0) <see cref="FinishSeconds"/>로
    /// (마지막 정타 틱 − 출발 틱)을 한 번만 적는다. <see cref="CardRushRunnerState.MoveStartTick"/>은 완주 틱도 그대로 쓴다
    /// (<c>CardRushServerCorrectionHandler.ApplyAuthoritative</c>가 넣는 서버 확정 값).</item>
    /// <item>연속 정타 최고 — <see cref="CardRushRunnerState.BestStreak"/>은 이미 보정으로도 줄지 않는 서버 확정 최댓값이지만
    /// (Task 8 리뷰 — 예측 중인 <c>Misses</c>는 서버보다 커질 수 있어도 <c>BestStreak</c>는 max로 맞춘다), 여기서도 본 값의 최댓값만
    /// 적어 재접속·되감기로 그 값이 한 틱이라도 흔들려도 기록지는 거꾸로 가지 않는다.</item>
    /// </list>
    /// <para>"나"는 <see cref="IGameDataStore.userEntityId"/>로 본다(정보 HUD·<c>FlappyFinishRecorder</c>와 같은 이유 —
    /// 몸이 사라져도 정체는 남는다).</para>
    /// </summary>
    public sealed class CardRushRecordReporter : ITickable
    {
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly MatchStartState _matchStart;
        private readonly MatchRecordSheet _sheet;
        private int _bestStreak;
        private bool _finishDone;

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

            if (_finishDone)
            {
                return;
            }
            int placement = mine.Get<FinishPlacement>()?.Value ?? 0;
            if (placement <= 0)
            {
                return;
            }
            _finishDone = true;
            long startTick = _matchStart.StartTick.CurrentValue;
            if (startTick != long.MaxValue)
            {
                _sheet.Set(CardRushRecordMetrics.FinishTime, FinishSeconds(runner.State.MoveStartTick, startTick));
            }
        }

        /// <summary>출발 틱부터 마지막 정타 틱까지(틱 0.02초). 순수 계산이라 따로 시험한다.</summary>
        internal static double FinishSeconds(long moveStartTick, long startTick) => (moveStartTick - startTick) * 0.02d;
    }
}
