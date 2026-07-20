using Cysharp.Threading.Tasks;
using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class InWaitingRoom : State<MatchEvent>
    {
        private const int CHECK_INTERVAL = 1;     //  sec
        private const int MAX_CONSECUTIVE_FAILURES = 5;

        private readonly IObjectResolver resolver;
        private readonly IUserDataStore userDataStore;

        public InWaitingRoom(IObjectResolver resolver, IUserDataStore userDataStore)
        {
            this.resolver = resolver;
            this.userDataStore = userDataStore;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.CancelClicked => resolver.Resolve<CancelMatchmaking>(),
                MatchEvent.LocationIsGameRoom => resolver.Resolve<InGameRoom>(),
                MatchEvent.LocationIsNone => resolver.Resolve<Idle>(),
                _ => this,
            };
        }

        protected override async Task OnExecuteAsync(CancellationToken ct)
        {
            int consecutiveFailures = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id);

                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    consecutiveFailures = 0;

                    switch (getUserLocation.response.userLocation.location)
                    {
                        case Location.GameRoom:
                            FSM.Fire(MatchEvent.LocationIsGameRoom);
                            return;

                        case Location.WaitingRoom:
                            break;   //  아직 대기 중 — 계속 폴링.

                        default:
                            FSM.Fire(MatchEvent.LocationIsNone);
                            return;
                    }
                }
                catch (WebRequestException e)
                {
                    //  일시 오류는 몇 번까지 넘어가고, 계속되면 초기 화면으로.
                    if (++consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    {
                        Debug.LogError($"Giving up polling after {consecutiveFailures} failures. Error: {e.Message}");
                        FSM.Fire(MatchEvent.LocationIsNone);
                        return;
                    }

                    Debug.LogWarning($"Location poll failed ({consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}). Error: {e.Message}");
                }

                await UniTask.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL), cancellationToken: ct);
            }
        }
    }
}
