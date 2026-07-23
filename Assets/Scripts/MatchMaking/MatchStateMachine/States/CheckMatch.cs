using Cysharp.Threading.Tasks;
using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class CheckMatch : State<MatchEvent>
    {
        private const int MAX_ATTEMPTS = 3;
        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<InWaitingRoom> inWaitingRoom;
        private readonly Func<Idle> idle;
        private readonly IUserDataStore userDataStore;

        public CheckMatch(Func<InGameRoom> inGameRoom, Func<InWaitingRoom> inWaitingRoom, Func<Idle> idle, IUserDataStore userDataStore)
        {
            this.inGameRoom = inGameRoom;
            this.inWaitingRoom = inWaitingRoom;
            this.idle = idle;
            this.userDataStore = userDataStore;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.LocationIsGameRoom => inGameRoom(),
                MatchEvent.LocationIsWaitingRoom => inWaitingRoom(),
                MatchEvent.LocationIsNone => idle(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
            {
                var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id);

                if (getUserLocation.response.code == ResponseCode.SUCCESS)
                {
                    return ToEvent(getUserLocation.response.userLocation.location);
                }

                Debug.LogError($"Failed to retrieve user information. code: {getUserLocation.response.code} (attempt {attempt}/{MAX_ATTEMPTS})");
                await UniTask.Delay(RetryInterval, cancellationToken: ct);
            }

            //  반복 실패 → 초기 화면(Idle)으로 안전 복귀.
            return MatchEvent.LocationIsNone;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Failed to retrieve user information. Error: {e.Message}");
            return MatchEvent.LocationIsNone;
        }

        private static MatchEvent ToEvent(Location location)
        {
            return location switch
            {
                Location.WaitingRoom => MatchEvent.LocationIsWaitingRoom,
                Location.GameRoom => MatchEvent.LocationIsGameRoom,
                _ => MatchEvent.LocationIsNone,
            };
        }
    }
}
