using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class RequestMatchmaking : State<MatchEvent>
    {
        private readonly Func<InWaitingRoom> inWaitingRoom;
        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserDataStore userDataStore;
        private readonly IMatchMakingDataStore matchMakingDataStore;

        public RequestMatchmaking(Func<InWaitingRoom> inWaitingRoom, Func<CheckMatch> checkMatch, IUserDataStore userDataStore, IMatchMakingDataStore matchMakingDataStore)
        {
            this.inWaitingRoom = inWaitingRoom;
            this.checkMatch = checkMatch;
            this.userDataStore = userDataStore;
            this.matchMakingDataStore = matchMakingDataStore;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.MatchRequestSucceeded => inWaitingRoom(),
                MatchEvent.MatchRequestFailed => checkMatch(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            var matchmakingRequest = new MatchmakingRequest
            {
                userId = userDataStore.user.id,
                matchType = matchMakingDataStore.matchType,
                subGameId = matchMakingDataStore.subGameId,
                mapId = matchMakingDataStore.mapId,
            };

            var requestMatchmaking = await WebAPI.RequestMatchmaking(matchmakingRequest);

            if (requestMatchmaking.response.code != ResponseCode.SUCCESS)
            {
                Debug.LogError($"Matchmaking request failed. Response code: {requestMatchmaking.response.code}");
                return MatchEvent.MatchRequestFailed;
            }

            return MatchEvent.MatchRequestSucceeded;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Failed to request matchmaking. Error: {e.Message}");
            return MatchEvent.MatchRequestFailed;
        }
    }
}
