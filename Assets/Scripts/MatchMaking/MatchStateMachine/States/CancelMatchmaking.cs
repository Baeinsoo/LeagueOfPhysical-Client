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

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            if (userDataStore.userLocation.locationDetail is not WaitingRoomLocationDetail waitingRoomLocationDetail)
            {
                Debug.LogError("User is not in a waiting room.");
                return MatchEvent.RecheckRequested;
            }

            var cancelMatchmaking = await WebAPI.CancelMatchmaking(waitingRoomLocationDetail.matchmakingTicketId);

            switch (cancelMatchmaking.response.code)
            {
                case ResponseCode.ALREADY_IN_GAME:
                    return MatchEvent.LocationIsGameRoom;

                case ResponseCode.MATCH_MAKING_TICKET_NOT_EXIST:
                    Debug.LogError("Matchmaking ticket does not exist.");
                    return MatchEvent.RecheckRequested;

                case ResponseCode.NOT_MATCH_MAKING_STATE:
                    Debug.LogError("Not in matchmaking state.");
                    return MatchEvent.RecheckRequested;

                default:
                    return MatchEvent.RecheckRequested;
            }
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Failed to cancel matchmaking. Error: {e.Message}");
            return MatchEvent.RecheckRequested;
        }
    }
}
