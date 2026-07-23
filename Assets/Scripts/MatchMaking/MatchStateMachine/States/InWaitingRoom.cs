using Cysharp.Threading.Tasks;
using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class InWaitingRoom : State<MatchEvent>
    {
        private const int CHECK_INTERVAL = 1;     //  sec
        private const int MAX_CONSECUTIVE_FAILURES = 5;

        private readonly Func<CancelMatchmaking> cancelMatchmaking;
        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<Idle> idle;
        private readonly IUserDataStore userDataStore;

        public InWaitingRoom(Func<CancelMatchmaking> cancelMatchmaking, Func<InGameRoom> inGameRoom, Func<Idle> idle, IUserDataStore userDataStore)
        {
            this.cancelMatchmaking = cancelMatchmaking;
            this.inGameRoom = inGameRoom;
            this.idle = idle;
            this.userDataStore = userDataStore;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.CancelClicked => cancelMatchmaking(),
                MatchEvent.LocationIsGameRoom => inGameRoom(),
                MatchEvent.LocationIsNone => idle(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            int consecutiveFailures = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id);

                    consecutiveFailures = 0;

                    switch (getUserLocation.response.userLocation.location)
                    {
                        case Location.GameRoom:
                            return MatchEvent.LocationIsGameRoom;

                        case Location.WaitingRoom:
                            break;   //  아직 대기 중 — 계속 폴링.

                        default:
                            return MatchEvent.LocationIsNone;
                    }
                }
                catch (WebRequestException e)
                {
                    //  일시 오류는 몇 번까지 넘어가고, 계속되면 초기 화면으로.
                    if (++consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    {
                        Debug.LogError($"Giving up polling after {consecutiveFailures} failures. Error: {e.Message}");
                        return MatchEvent.LocationIsNone;
                    }

                    Debug.LogWarning($"Location poll failed ({consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}). Error: {e.Message}");
                }

                await UniTask.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL), cancellationToken: ct);
            }

            return null;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Unexpected error while waiting. Error: {e.Message}");
            return MatchEvent.LocationIsNone;
        }
    }
}
