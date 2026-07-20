using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CancelMatchmaking : State<MatchEvent>
    {
        private readonly IObjectResolver resolver;
        private readonly IUserDataStore userDataStore;

        public CancelMatchmaking(IObjectResolver resolver, IUserDataStore userDataStore)
        {
            this.resolver = resolver;
            this.userDataStore = userDataStore;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.LocationIsGameRoom => resolver.Resolve<InGameRoom>(),
                MatchEvent.RecheckRequested => resolver.Resolve<CheckMatch>(),
                _ => this,
            };
        }

        protected override async Task OnExecuteAsync(CancellationToken ct)
        {
            if (userDataStore.userLocation.locationDetail is not WaitingRoomLocationDetail waitingRoomLocationDetail)
            {
                Debug.LogError("User is not in a waiting room.");
                FSM.Fire(MatchEvent.RecheckRequested);
                return;
            }

            var cancelMatchmaking = await WebAPI.CancelMatchmaking(waitingRoomLocationDetail.matchmakingTicketId);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            switch (cancelMatchmaking.response.code)
            {
                case ResponseCode.ALREADY_IN_GAME:
                    FSM.Fire(MatchEvent.LocationIsGameRoom);
                    break;

                case ResponseCode.MATCH_MAKING_TICKET_NOT_EXIST:
                    Debug.LogError("Matchmaking ticket does not exist.");
                    FSM.Fire(MatchEvent.RecheckRequested);
                    break;

                case ResponseCode.NOT_MATCH_MAKING_STATE:
                    Debug.LogError("Not in matchmaking state.");
                    FSM.Fire(MatchEvent.RecheckRequested);
                    break;

                default:
                    FSM.Fire(MatchEvent.RecheckRequested);
                    break;
            }
        }

        protected override void OnError(Exception e)
        {
            Debug.LogError($"Failed to cancel matchmaking. Error: {e.Message}");
            FSM.Fire(MatchEvent.RecheckRequested);
        }
    }
}
