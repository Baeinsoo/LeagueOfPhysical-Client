using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>프로필에 보여줄 큐 하나의 전적. 기록이 없으면 HasRecord가 false다.</summary>
    public readonly struct ProfileQueueStats
    {
        public readonly string QueueName;
        public readonly bool HasRecord;
        public readonly int Mmr;
        public readonly int GamesPlayed;
        public readonly int FirstPlaces;
        public readonly string AveragePlacement;

        public ProfileQueueStats(string queueName, bool hasRecord, int mmr, int gamesPlayed, int firstPlaces, string averagePlacement)
        {
            QueueName = queueName;
            HasRecord = hasRecord;
            Mmr = mmr;
            GamesPlayed = gamesPlayed;
            FirstPlaces = firstPlaces;
            AveragePlacement = averagePlacement;
        }
    }

    /// <summary>
    /// 프로필 ViewModel. 열릴 때 레이팅을 다시 받아온다 — 스토어는 로그인 때 한 번만 채워져,
    /// 그대로 읽으면 판을 하고 와도 로그인 시점의 낡은 값이 뜬다.
    /// 도착 전/후가 시간에 따라 바뀌는 라이브 상태라 R3로 노출한다(결과 화면과 다른 점).
    /// </summary>
    public class ProfileViewModel : IDisposable
    {
        //  큐를 TbQueue에서 읽는 것은 로비 선택 UI 슬라이스 몫이다 — 여기도 호출부 관례대로 id를 박는다.
        private static readonly (int id, string name)[] Queues =
        {
            (1, "캐주얼"),
            (2, "랭크"),
        };

        private readonly IUserDataStore _userDataStore;
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<IReadOnlyList<ProfileQueueStats>> _stats = new(null);
        private readonly ReactiveProperty<string> _status = new("불러오는 중…");

        /// <summary>도착 전에는 null.</summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<ProfileQueueStats>> Stats => _stats;

        /// <summary>비어 있으면 숨긴다. 로딩·실패 안내에 쓴다.</summary>
        public ReadOnlyReactiveProperty<string> Status => _status;

        public ProfileViewModel(IUserDataStore userDataStore)
        {
            _userDataStore = userDataStore;

            LoadAsync().Forget();
        }

        public void Dispose()
        {
            //  받아오는 도중에 화면이 사라질 수 있다. 먼저 끊어야 아래에서 dispose한 프로퍼티에
            //  값을 쓰려다 터지지 않는다.
            _cts.Cancel();
            _cts.Dispose();

            _stats.Dispose();
            _status.Dispose();
        }

        private async UniTaskVoid LoadAsync()
        {
            string userId = _userDataStore.user?.id;
            if (string.IsNullOrEmpty(userId))
            {
                _status.Value = "전적을 불러올 수 없습니다.";
                return;
            }

            try
            {
                foreach (var queue in Queues)
                {
                    await WebAPI.GetUserRating(userId, queue.id, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                //  화면이 먼저 닫힌 것뿐이다. 프로퍼티는 이미 dispose됐으니 건드리지 않는다.
                return;
            }
            catch (Exception e)
            {
                //  받아온 게 하나도 없으면 아래 Build가 전부 "기록 없음"으로 채운다 — 그건 거짓말이라
                //  실패를 그대로 알린다. 스토어에 남아 있던 로그인 시점 값도 쓰지 않는다.
                Debug.LogError($"Failed to load user rating. Error: {e.Message}");
                if (_cts.IsCancellationRequested) return;

                _status.Value = "전적을 불러오지 못했습니다.";
                return;
            }

            if (_cts.IsCancellationRequested) return;

            _status.Value = string.Empty;
            _stats.Value = Build(_userDataStore.userRatingByQueueId);
        }

        private static IReadOnlyList<ProfileQueueStats> Build(IReadOnlyDictionary<int, UserRating> ratingByQueueId)
        {
            var stats = new List<ProfileQueueStats>(Queues.Length);

            foreach (var queue in Queues)
            {
                if (ratingByQueueId.TryGetValue(queue.id, out var rating) && rating.gamesPlayed > 0)
                {
                    //  평균 등수는 판수로 나눈다 — 판수 0이면 0으로 나누므로 위 가드가 필수다.
                    //  로캘이 쉼표 소수점이면 "3,5등"이 된다 — 표기를 고정한다.
                    string average = ((double)rating.placementSum / rating.gamesPlayed)
                        .ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

                    stats.Add(new ProfileQueueStats(
                        queue.name, true, rating.mmr, rating.gamesPlayed, rating.firstPlaces, average));
                }
                else
                {
                    stats.Add(new ProfileQueueStats(queue.name, false, 0, 0, 0, null));
                }
            }

            return stats;
        }
    }
}
