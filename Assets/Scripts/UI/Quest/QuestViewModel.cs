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

        /// <summary>지금 눌러서 바꿀 수 있나 — 바꾸기가 끝나는 동안(Busy)이거나 남은 횟수가 0이면 false(spec §7의 "잠김").</summary>
        public readonly bool CanReroll;

        /// <summary>바꾸기 버튼을 그릴 자리인가 — 바꿀 수 있는 기간(일일)의 안 끝난 칸이면 늘 true, 잠겨 있어도 버튼은 보인다.</summary>
        public readonly bool ShowReroll;

        public QuestCard(string id, string title, string tierText, string progressText, float ratio, string rewardText, bool completed, bool canReroll, bool showReroll)
        {
            Id = id;
            Title = title;
            TierText = tierText;
            ProgressText = progressText;
            Ratio = ratio;
            RewardText = rewardText;
            Completed = completed;
            CanReroll = canReroll;
            ShowReroll = showReroll;
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

        //  리셋 다시 받기 물러서기(I2) — 실패·Disabled·같은 기간 응답이면 endsAt이 안 바뀌어 매 Tick(1초)마다
        //  또 보낼 뻔한다. 그 endsAt으로 보낸 시도를 기억해 바뀌기 전엔 물러서기 동안만 다시 보낸다.
        private static readonly TimeSpan ResetRetryBackoff = TimeSpan.FromSeconds(30);

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
        private readonly ReactiveProperty<bool> _hasData = new(false);
        private bool _refreshingForReset;
        private DateTime? _resetAttemptedForEndsAt;
        private DateTime _lastResetAttemptAt = DateTime.MinValue;
        private bool _disposed;

        public ReadOnlyReactiveProperty<IReadOnlyList<QuestCard>> Daily => _daily;
        public ReadOnlyReactiveProperty<IReadOnlyList<QuestCard>> Weekly => _weekly;
        public ReadOnlyReactiveProperty<string> DailyRemaining => _dailyRemaining;
        public ReadOnlyReactiveProperty<string> WeeklyRemaining => _weeklyRemaining;
        public ReadOnlyReactiveProperty<string> RerollText => _rerollText;
        public ReadOnlyReactiveProperty<string> Message => _message;

        /// <summary>바꾸기 응답을 기다리는 동안 true — 그동안 바꾸기 버튼을 잠근다(같은 칸을 두 번 보내지 않게).</summary>
        public ReadOnlyReactiveProperty<bool> Busy => _busy;

        /// <summary>일일·주간 어느 쪽이든 한 번이라도 받았나 — false면 화면이 "오늘"·"이번 주" 빈 머리를 숨긴다.</summary>
        public ReadOnlyReactiveProperty<bool> HasData => _hasData;

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
            TryRefreshAfterReset(daily, now);
        }

        /// <summary>
        /// 일일 기간이 넘어가면 다시 받는다 — 단 같은 endsAt으로는 물러서기(<see cref="ResetRetryBackoff"/>) 동안
        /// 한 번만(I2). 실패·Disabled·서버가 아직 같은 기간을 주면 endsAt이 그대로라 매 Tick마다 보낼 뻔한 것을 막는다.
        /// </summary>
        private void TryRefreshAfterReset(QuestDailyDto daily, DateTime now)
        {
            if (_refreshingForReset || daily == null || now < daily.endsAt) return;
            if (_resetAttemptedForEndsAt == daily.endsAt && now - _lastResetAttemptAt < ResetRetryBackoff) return;

            _resetAttemptedForEndsAt = daily.endsAt;
            _lastResetAttemptAt = now;
            _refreshingForReset = true;
            RefreshAfterReset().Forget();
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
                    //  m2: 다시 받은 칸 중 끝난 것을 "봤다"로 적는다 — 안 적으면 판에서 본 완료가 로비 배지로 다시 뜬다.
                    if (!_disposed) _store.MarkAllSeen();
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
            bool ok = await _store.RefreshAsync(_cts.Token);
            if (_disposed) return;
            _store.MarkAllSeen();
            if (ok) ClearStaleRerollMessage();
        }

        private async UniTaskVoid RefreshAfterReset()
        {
            bool ok = await _store.RefreshAsync(_cts.Token);
            if (_disposed) return;
            _refreshingForReset = false;
            //  m2: 리셋 뒤 다시 받은 칸 중 끝난 것을 "봤다"로 적는다(배지가 다시 뜨지 않게).
            _store.MarkAllSeen();
            if (ok) ClearStaleRerollMessage();
        }

        //  m2: 바꾸기 실패 문구는 다음 "조용한" 다시 받기(열 때·리셋)가 성공하면 지운다.
        //  Disabled·Failed 문구는 OnState가 따로 관리하므로 여기서는 손대지 않는다.
        private void ClearStaleRerollMessage()
        {
            if (_message.Value != DisabledText && _message.Value != FailedText) _message.Value = string.Empty;
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
            //  m1: 버튼은 "잠김"일 뿐 사라지지 않는다(spec §7) — rerollEnabled는 지금 눌러도 되는지,
            //  showReroll은 그 기간에 버튼을 그릴지를 따로 가른다(주간은 showReroll 자체가 늘 false).
            bool rerollEnabled = daily != null && daily.rerollsLeft > 0 && !_busy.Value;
            _daily.Value = Cards(daily?.quests, eligiblePeriod: true, rerollEnabled);
            _weekly.Value = Cards(weekly?.quests, eligiblePeriod: false, rerollEnabled: false);
            _rerollText.Value = daily == null ? string.Empty : QuestFormat.Rerolls(daily.rerollsLeft);
            _hasData.Value = daily != null || weekly != null;
        }

        private static IReadOnlyList<QuestCard> Cards(QuestItemDto[] quests, bool eligiblePeriod, bool rerollEnabled)
        {
            if (quests == null) return Array.Empty<QuestCard>();
            var cards = new List<QuestCard>(quests.Length);
            foreach (var q in quests)
            {
                if (q == null) continue;
                bool showReroll = eligiblePeriod && !q.completed;
                cards.Add(new QuestCard(q.id, q.title, QuestFormat.Tier(q.tier), QuestFormat.Progress(q.progress, q.target, q.unit),
                    QuestFormat.Ratio(q.progress, q.target), QuestFormat.Reward(q.rewardCoins, q.rewardXp), q.completed,
                    canReroll: showReroll && rerollEnabled, showReroll));
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
            _hasData.Dispose();
        }
    }
}
