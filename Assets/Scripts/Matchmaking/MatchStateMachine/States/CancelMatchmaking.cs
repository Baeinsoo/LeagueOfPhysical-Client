using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class CancelMatchmaking : State<MatchEvent>
    {
        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserLocationService userLocationService;

        public CancelMatchmaking(Func<InGameRoom> inGameRoom, Func<CheckMatch> checkMatch, IUserLocationService userLocationService)
        {
            this.inGameRoom = inGameRoom;
            this.checkMatch = checkMatch;
            this.userLocationService = userLocationService;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.LocationIsGameRoom => inGameRoom(),
                MatchEvent.RecheckRequested => checkMatch(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            string ticketId = userLocationService.TicketId;
            if (string.IsNullOrEmpty(ticketId))
            {
                Debug.LogError("User is not in matchmaking.");
                return MatchEvent.RecheckRequested;
            }

            var cancelMatchmaking = await WebAPI.CancelMatchmaking(ticketId);

            switch (cancelMatchmaking.code)
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
