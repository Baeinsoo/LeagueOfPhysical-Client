using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace LOP.UI
{
    /// <summary>퀘스트 카드 한 장. 문구는 전부 <see cref="QuestFormat"/>이 만든다.</summary>
    public readonly struct QuestCard
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string TierText;
        public readonly string ProgressText;
        public readonly float Ratio;
        public readonly string RewardText;
        public readonly bool Completed;
        public readonly bool CanReroll;

        public QuestCard(string id, string title, string tierText, string progressText, float ratio, string rewardText, bool completed, bool canReroll)
        {
            Id = id;
            Title = title;
            TierText = tierText;
            ProgressText = progressText;
            Ratio = ratio;
            RewardText = rewardText;
            Completed = completed;
            CanReroll = canReroll;
        }
    }

    /// <summary>
    /// 퀘스트 판 ViewModel(spec 2026-10-30 §7). 상태는 전부 <see cref="QuestStore"/>에서 파생한다 — 자기 플래그를 따로 안 든다(상점과 같은 방식).
    /// 열 때 끝나 있는 칸을 "봤다"로 적어 로비 배지를 지운다.
    /// </summary>
    public sealed class QuestViewModel : IDisposable
    {
        private const string DisabledText = "퀘스트를 쉬고 있어요";
        private const string FailedText = "퀘스트를 불러오지 못했어요";

        private readonly QuestStore _store;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<IReadOnlyList<QuestCard>> _daily = new(Array.Empty<QuestCard>());
        private readonly ReactiveProperty<IReadOnlyList<QuestCard>> _weekly = new(Array.Empty<QuestCard>());
        private readonly ReactiveProperty<string> _dailyRemaining = new(string.Empty);
        private readonly ReactiveProperty<string> _weeklyRemaining = new(string.Empty);
        private readonly ReactiveProperty<string> _rerollText = new(string.Empty);
        private readonly ReactiveProperty<string> _message = new(string.Empty);
        private readonly ReactiveProperty<bool> _busy = new(false);
        private bool _refreshingForReset;
        private bool _disposed;

        public ReadOnlyReactiveProperty<IReadOnlyList<QuestCard>> Daily => _daily;
        public ReadOnlyReactiveProperty<IReadOnlyList<QuestCard>> Weekly => _weekly;
        public ReadOnlyReactiveProperty<string> DailyRemaining => _dailyRemaining;
        public ReadOnlyReactiveProperty<string> WeeklyRemaining => _weeklyRemaining;
        public ReadOnlyReactiveProperty<string> RerollText => _rerollText;
        public ReadOnlyReactiveProperty<string> Message => _message;

        /// <summary>바꾸기 응답을 기다리는 동안 true — 그동안 바꾸기 버튼을 잠근다(같은 칸을 두 번 보내지 않게).</summary>
        public ReadOnlyReactiveProperty<bool> Busy => _busy;

        public QuestViewModel(QuestStore store)
        {
            _store = store;
            _subscriptions.Add(_store.Daily.Subscribe(_ => Rebuild()));
            _subscriptions.Add(_store.Weekly.Subscribe(_ => Rebuild()));
            _subscriptions.Add(_store.State.Subscribe(OnState));
            _store.MarkAllSeen();
            //  열 때마다 다시 받는다 — 로비 진입 조회가 실패했어도 여기서 회복한다(상점과 같은 이유).
            RefreshThenMarkSeen().Forget();
        }

        /// <summary>View가 1초마다 부른다. 남은 시간을 다시 쓰고, 일일 기간이 넘어가면 한 번 다시 받는다(새 칸이 배정된다).</summary>
        public void Tick()
        {
            var now = _store.ServerNow;
            var daily = _store.Daily.CurrentValue;
            var weekly = _store.Weekly.CurrentValue;
            _dailyRemaining.Value = daily == null ? string.Empty : QuestFormat.Remaining(daily.endsAt, now);
            _weeklyRemaining.Value = weekly == null ? string.Empty : QuestFormat.Remaining(weekly.endsAt, now);
            if (!_refreshingForReset && daily != null && now >= daily.endsAt)
            {
                _refreshingForReset = true;
                RefreshAfterReset().Forget();
            }
        }

        public async UniTask RerollAsync(string questId)
        {
            if (_busy.Value) return;
            _busy.Value = true;
            Rebuild();
            try
            {
                //  판을 닫아도 응답은 끝까지 받는다 — 스토어는 로비까지 살아 있어 서버와 같아야 한다.
                int code = await _store.RerollAsync(questId, CancellationToken.None);
                if (_disposed) return;
                _message.Value = QuestFormat.RerollMessage(code);
                //  칸이 이미 바뀌었거나 끝났다 — 지금 보이는 판이 낡았다.
                if (code == ResponseCode.QUEST_NOT_FOUND || code == ResponseCode.QUEST_ALREADY_COMPLETED)
                {
                    await _store.RefreshAsync(_cts.Token);
                }
            }
            finally
            {
                if (!_disposed)
                {
                    _busy.Value = false;
                    Rebuild();
                }
            }
        }

        private async UniTaskVoid RefreshThenMarkSeen()
        {
            await _store.RefreshAsync(_cts.Token);
            if (!_disposed) _store.MarkAllSeen();
        }

        private async UniTaskVoid RefreshAfterReset()
        {
            await _store.RefreshAsync(_cts.Token);
            if (!_disposed) _refreshingForReset = false;
        }

        private void OnState(QuestLoadState state)
        {
            if (state == QuestLoadState.Disabled) _message.Value = DisabledText;
            else if (state == QuestLoadState.Failed) _message.Value = FailedText;
            else if (state == QuestLoadState.Ready && (_message.Value == DisabledText || _message.Value == FailedText)) _message.Value = string.Empty;
        }

        private void Rebuild()
        {
            var daily = _store.Daily.CurrentValue;
            var weekly = _store.Weekly.CurrentValue;
            bool canReroll = daily != null && daily.rerollsLeft > 0 && !_busy.Value;
            _daily.Value = Cards(daily?.quests, canReroll);
            _weekly.Value = Cards(weekly?.quests, false);
            _rerollText.Value = daily == null ? string.Empty : QuestFormat.Rerolls(daily.rerollsLeft);
        }

        private static IReadOnlyList<QuestCard> Cards(QuestItemDto[] quests, bool canReroll)
        {
            if (quests == null) return Array.Empty<QuestCard>();
            var cards = new List<QuestCard>(quests.Length);
            foreach (var q in quests)
            {
                if (q == null) continue;
                cards.Add(new QuestCard(q.id, q.title, QuestFormat.Tier(q.tier), QuestFormat.Progress(q.progress, q.target, q.unit),
                    QuestFormat.Ratio(q.progress, q.target), QuestFormat.Reward(q.rewardCoins, q.rewardXp), q.completed, canReroll && !q.completed));
            }
            return cards;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _subscriptions.Dispose();
            _daily.Dispose();
            _weekly.Dispose();
            _dailyRemaining.Dispose();
            _weeklyRemaining.Dispose();
            _rerollText.Dispose();
            _message.Dispose();
            _busy.Dispose();
        }
    }
}
