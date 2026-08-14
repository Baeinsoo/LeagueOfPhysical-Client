using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;
using UnityEngine;

namespace LOP
{
    public sealed class UserLocationService : IUserLocationService, IDisposable
    {
        private const int RefreshAttempts = 3;                //  1회 조회가 실패하면 이만큼까지 다시 시도
        private const int RetryIntervalSeconds = 1;
        private const int PollIntervalSeconds = 1;
        private const int MaxConsecutivePollFailures = 5;     //  폴링이 이만큼 내리 실패하면 포기

        private readonly IUserDataStore userDataStore;
        private readonly Subject<Unit> faulted = new();
        private readonly IDisposable locationSubscription;

        private CancellationTokenSource pollCts;
        private string requestedTicketId;

        public UserLocationService(IUserDataStore userDataStore)
        {
            this.userDataStore = userDataStore;
            //  폴링을 켜고 끄는 판단은 서비스가 한다 — 호출자에게 넘기면 정책이 다시 흩어진다.
            locationSubscription = userDataStore.userLocation.Subscribe(OnUserLocationChanged);
        }

        public ReadOnlyReactiveProperty<UserLocation> UserLocation => userDataStore.userLocation;

        public Observable<Unit> Faulted => faulted;

        public string TicketId
        {
            get
            {
                if (UserLocation.CurrentValue.locationDetail is MatchmakingLocationDetail detail)
                {
                    return detail.matchmakingTicketId;
                }

                //  방금 요청해서 서버 위치가 아직 안 따라온 구간에는 응답으로 받은 id를 쓴다.
                return requestedTicketId;
            }
        }

        public async UniTask<bool> RefreshAsync(CancellationToken ct = default)
        {
            for (int attempt = 1; attempt <= RefreshAttempts; attempt++)
            {
                if (await TryFetchAsync(ct))
                {
                    return true;
                }

                Debug.LogError($"Failed to retrieve user location. (attempt {attempt}/{RefreshAttempts})");

                if (attempt < RefreshAttempts)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(RetryIntervalSeconds), cancellationToken: ct);
                }
            }

            return false;
        }

        public void OnMatchmakingRequested(string ticketId)
        {
            requestedTicketId = ticketId;
            StartPolling();
        }

        public void Dispose()
        {
            StopPolling();
            locationSubscription.Dispose();
            faulted.Dispose();
        }

        //  조회 1회. 값 반영은 스토어가 응답 구독으로 하므로 여기서는 성공 여부만 돌려준다.
        private async UniTask<bool> TryFetchAsync(CancellationToken ct)
        {
            try
            {
                var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id, ct);
                return getUserLocation.code == ResponseCode.SUCCESS;
            }
            catch (GameFramework.Http.HttpRequestException e)
            {
                Debug.LogWarning($"User location request failed. Error: {e.Message}");
                return false;
            }
        }

        private void OnUserLocationChanged(UserLocation userLocation)
        {
            if (userLocation.location == Location.Matchmaking)
            {
                StartPolling();
                return;
            }

            //  매칭을 벗어났으면 들고 있던 티켓 id는 더 이상 유효하지 않다.
            requestedTicketId = null;
            StopPolling();
        }

        private void StartPolling()
        {
            if (pollCts != null)
            {
                return;
            }

            pollCts = new CancellationTokenSource();
            PollLoopAsync(pollCts.Token).Forget();
        }

        private void StopPolling()
        {
            if (pollCts == null)
            {
                return;
            }

            pollCts.Cancel();
            pollCts.Dispose();
            pollCts = null;
        }

        private async UniTaskVoid PollLoopAsync(CancellationToken ct)
        {
            int consecutiveFailures = 0;

            try
            {
                while (ct.IsCancellationRequested == false)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), cancellationToken: ct);

                    if (await TryFetchAsync(ct))
                    {
                        consecutiveFailures = 0;
                        continue;
                    }

                    if (++consecutiveFailures >= MaxConsecutivePollFailures)
                    {
                        Debug.LogError($"Giving up user location polling after {consecutiveFailures} failures.");
                        StopPolling();
                        faulted.OnNext(Unit.Default);
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //  폴링이 멈춰서 취소됨 — 정상.
            }
        }
    }
}
