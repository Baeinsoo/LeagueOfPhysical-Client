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
        private readonly IMatchmakingDataStore matchmakingDataStore;

        public RequestMatchmaking(Func<InWaitingRoom> inWaitingRoom, Func<CheckMatch> checkMatch, IUserDataStore userDataStore, IMatchmakingDataStore matchmakingDataStore)
        {
            this.inWaitingRoom = inWaitingRoom;
            this.checkMatch = checkMatch;
            this.userDataStore = userDataStore;
            this.matchmakingDataStore = matchmakingDataStore;
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
                matchType = matchmakingDataStore.matchType,
                subGameId = matchmakingDataStore.subGameId,
                mapId = matchmakingDataStore.mapId,
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
