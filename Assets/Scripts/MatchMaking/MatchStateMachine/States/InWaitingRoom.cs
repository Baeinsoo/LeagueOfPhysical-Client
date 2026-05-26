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
        private const int CHECK_INTERVAL = 1;   //  sec

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
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id);

                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    switch (getUserLocation.response.userLocation.location)
                    {
                        case Location.GameRoom:
                            FSM.Fire(MatchEvent.LocationIsGameRoom);
                            return;

                        case Location.WaitingRoom:
                            //  Still waiting, keep polling.
                            break;

                        default:
                            FSM.Fire(MatchEvent.LocationIsNone);
                            return;
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                //  State exited while waiting.
            }
            catch (WebRequestException e)
            {
                Debug.LogError($"Failed to retrieve user information. Error: {e.Message}");
            }
        }
    }
}
