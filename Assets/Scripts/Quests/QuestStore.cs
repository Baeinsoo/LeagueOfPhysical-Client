using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace LOP
{
    public enum QuestLoadState
    {
        Loading,
        Ready,
        //  서버 스위치(economy.quests)가 꺼져 있다.
        Disabled,
        //  한 번도 못 받았다(옛 백엔드 404·연결 실패).
        Failed,
    }

    /// <summary>
    /// 로비가 보는 "내 퀘스트"(spec 2026-10-30 §7). 로비에 들어올 때 받고(경기 뒤 서버가 진행을 바꾼다 — 재화와 같은 이유),
    /// 바꾸기 응답으로 일일 묶음을 덮는다. 실패하면 옛 값을 둔다.
    /// <para>남은 시간은 기기 시계가 아니라 받은 순간의 서버 시각과의 차이로 센다 — 폰 시계가 틀려도 05:00 KST가 맞게.</para>
    /// <para>로비 범위(Scoped) — 퀘스트 판과 로비 배지가 같은 인스턴스를 본다. 화면 하나만 쓰는 값이 아니라서다.</para>
    /// </summary>
    public class QuestStore : IDisposable
    {
        private readonly IUserDataStore _users;
        private readonly IQuestSeenStore _seen;
        private readonly Func<string, CancellationToken, UniTask<GetQuestsResponse>> _fetch;
        private readonly Func<string, RerollQuestRequest, CancellationToken, UniTask<RerollQuestResponse>> _reroll;
        private readonly Func<DateTime> _utcNow;

        private readonly ReactiveProperty<QuestDailyDto> _daily = new(null);
        private readonly ReactiveProperty<QuestWeeklyDto> _weekly = new(null);
        private readonly ReactiveProperty<QuestLoadState> _state = new(QuestLoadState.Loading);
        private readonly ReactiveProperty<int> _unseenCompleted = new(0);
        private readonly ReactiveProperty<int> _completedDaily = new(0);
        private TimeSpan _serverOffset = TimeSpan.Zero;

        //  최종 리뷰 m1: 판을 연 직후 GET과 바꾸기가 겹치면, 바꾸기보다 먼저 떠난 GET이 늦게 돌아와 바꾸기 결과를
        //  덮을 수 있다(바꾸기는 같은 행 id를 고쳐 써서 서버가 그 뒤 또 성공으로 받아들인다). 바꾸기 성공 때마다
        //  세대를 올리고, GET은 시작할 때의 세대를 기억해 둔다 — 돌아왔을 때 세대가 바뀌었으면 낡은 응답이라 버린다.
        private int _generation;

        public ReadOnlyReactiveProperty<QuestDailyDto> Daily => _daily;
        public ReadOnlyReactiveProperty<QuestWeeklyDto> Weekly => _weekly;
        public ReadOnlyReactiveProperty<QuestLoadState> State => _state;

        /// <summary>퀘스트 판을 연 뒤로 새로 끝난 칸 수 — 로비 🎯 배지.</summary>
        public ReadOnlyReactiveProperty<int> UnseenCompleted => _unseenCompleted;

        /// <summary>오늘 끝낸 일일 칸 수.</summary>
        public ReadOnlyReactiveProperty<int> CompletedDaily => _completedDaily;

        /// <summary>지금 서버 시각(UTC). 받기 전엔 기기 시각.</summary>
        public DateTime ServerNow => _utcNow() + _serverOffset;

        [VContainer.Inject]
        public QuestStore(IUserDataStore users, IQuestSeenStore seen)
            : this(users, seen, WebAPI.GetQuests, WebAPI.RerollQuest, () => DateTime.UtcNow)
        {
        }

        /// <param name="fetch">(userId, ct) → 내 퀘스트. 시험이 가짜를 꽂는다.</param>
        /// <param name="utcNow">기기 UTC 시각. 시험이 시계를 움직인다.</param>
        public QuestStore(IUserDataStore users, IQuestSeenStore seen,
            Func<string, CancellationToken, UniTask<GetQuestsResponse>> fetch,
            Func<string, RerollQuestRequest, CancellationToken, UniTask<RerollQuestResponse>> reroll,
            Func<DateTime> utcNow)
        {
            _users = users;
            _seen = seen;
            _fetch = fetch;
            _reroll = reroll;
            _utcNow = utcNow;
        }

        public async UniTask<bool> RefreshAsync(CancellationToken cancellationToken)
        {
            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return false;

            int generationAtStart = _generation;
            try
            {
                var response = await _fetch(userId, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return false;
                if (generationAtStart != _generation)
                {
                    //  m1: 받는 동안 바꾸기가 성공해 더 새 값이 이미 있다 — 세대가 바뀐 낡은 응답은 버린다.
                    UnityEngine.Debug.LogWarning("[QuestStore] Dropped a stale GET — a reroll advanced the generation meanwhile.");
                    return false;
                }
                if (response?.code == ResponseCode.ECONOMY_DISABLED)
                {
                    _state.Value = QuestLoadState.Disabled;
                    //  m2: 켜져 있다가 꺼지면 묵은 카드를 치운다 — 판이 빈 목록으로 다시 그려 버튼까지 함께 숨는다.
                    _daily.Value = null;
                    _weekly.Value = null;
                    Recount();
                    return false;
                }
                if (response == null || response.code != ResponseCode.SUCCESS || response.daily == null || response.weekly == null)
                {
                    UnityEngine.Debug.LogWarning($"[QuestStore] Failed to refresh quests. code: {response?.code}");
                    MarkFailedIfEmpty();
                    return false;
                }

                _serverOffset = response.serverNow - _utcNow();
                _daily.Value = response.daily;
                _weekly.Value = response.weekly;
                _state.Value = QuestLoadState.Ready;
                Recount();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                //  옛 백엔드(퀘스트 라우트 없음)면 404로 여기 온다 — 판은 "불러오지 못했어요"를 보인다.
                UnityEngine.Debug.LogWarning($"[QuestStore] Failed to refresh quests. error: {e.Message}");
                MarkFailedIfEmpty();
                return false;
            }
        }

        /// <summary>일일 칸 하나를 바꾼다. 응답 코드를 돌려준다(연결 실패는 0). 성공이면 일일 묶음을 덮는다.</summary>
        public async UniTask<int> RerollAsync(string questId, CancellationToken cancellationToken)
        {
            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return 0;

            try
            {
                var response = await _reroll(userId, new RerollQuestRequest { questId = questId }, cancellationToken);
                if (response?.code == ResponseCode.SUCCESS && response.daily != null)
                {
                    _daily.Value = response.daily;
                    //  m1: 세대를 올려 둔다 — 이 바꾸기보다 먼저 떠난 GET이 늦게 와도 RefreshAsync가 버린다.
                    _generation++;
                    Recount();
                }
                return response?.code ?? 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[QuestStore] Failed to reroll. error: {e.Message}");
                return 0;
            }
        }

        /// <summary>퀘스트 판을 열었다 — 지금 끝나 있는 칸은 다 본 것으로 적는다(안 끝난 칸은 적지 않는다).</summary>
        public void MarkAllSeen()
        {
            _seen.MarkSeen(CompletedIds());
            Recount();
        }

        private IEnumerable<string> CompletedIds()
        {
            foreach (var q in _daily.Value?.quests ?? Array.Empty<QuestItemDto>())
            {
                if (q != null && q.completed) yield return q.id;
            }
            foreach (var q in _weekly.Value?.quests ?? Array.Empty<QuestItemDto>())
            {
                if (q != null && q.completed) yield return q.id;
            }
        }

        private void Recount()
        {
            int unseen = 0;
            foreach (var id in CompletedIds())
            {
                if (!_seen.IsSeen(id)) unseen++;
            }
            _unseenCompleted.Value = unseen;

            int daily = 0;
            foreach (var q in _daily.Value?.quests ?? Array.Empty<QuestItemDto>())
            {
                if (q != null && q.completed) daily++;
            }
            _completedDaily.Value = daily;
        }

        private void MarkFailedIfEmpty()
        {
            if (_daily.Value == null && _state.Value != QuestLoadState.Disabled) _state.Value = QuestLoadState.Failed;
        }

        public void Dispose()
        {
            _daily.Dispose();
            _weekly.Dispose();
            _state.Dispose();
            _unseenCompleted.Dispose();
            _completedDaily.Dispose();
        }
    }
}
