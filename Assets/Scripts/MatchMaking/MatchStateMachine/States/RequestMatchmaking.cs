using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class RequestMatchmaking : State<MatchEvent>
    {
        private readonly IObjectResolver resolver;
        private readonly IUserDataStore userDataStore;
        private readonly IMatchMakingDataStore matchMakingDataStore;

        public RequestMatchmaking(IObjectResolver resolver, IUserDataStore userDataStore, IMatchMakingDataStore matchMakingDataStore)
        {
            this.resolver = resolver;
            this.userDataStore = userDataStore;
            this.matchMakingDataStore = matchMakingDataStore;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.MatchRequestSucceeded => resolver.Resolve<InWaitingRoom>(),
                MatchEvent.MatchRequestFailed => resolver.Resolve<CheckMatch>(),
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
