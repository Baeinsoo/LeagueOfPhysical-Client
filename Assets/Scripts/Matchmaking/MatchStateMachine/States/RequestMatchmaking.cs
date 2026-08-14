using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class RequestMatchmaking : State<MatchEvent>
    {
        private readonly Func<InMatchmaking> inMatchmaking;
        private readonly Func<CheckMatch> checkMatch;
        private readonly IMatchmakingDataStore matchmakingDataStore;
        private readonly IUserLocationService userLocationService;

        public RequestMatchmaking(Func<InMatchmaking> inMatchmaking, Func<CheckMatch> checkMatch, IMatchmakingDataStore matchmakingDataStore, IUserLocationService userLocationService)
        {
            this.inMatchmaking = inMatchmaking;
            this.checkMatch = checkMatch;
            this.matchmakingDataStore = matchmakingDataStore;
            this.userLocationService = userLocationService;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.MatchRequestSucceeded => inMatchmaking(),
                MatchEvent.MatchRequestFailed => checkMatch(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            var matchmakingRequest = new MatchmakingRequest
            {
                queueId = matchmakingDataStore.queueId,
                gameModeId = matchmakingDataStore.gameModeId,
                mapId = matchmakingDataStore.mapId,
            };

            var requestMatchmaking = await WebAPI.RequestMatchmaking(matchmakingRequest);

            if (requestMatchmaking.code != ResponseCode.SUCCESS)
            {
                Debug.LogError($"Matchmaking request failed. Response code: {requestMatchmaking.code}");
                return MatchEvent.MatchRequestFailed;
            }

            //  받은 티켓 id를 서비스에 넘긴다 — 취소가 위치 폴링을 기다리지 않게.
            userLocationService.OnMatchmakingRequested(requestMatchmaking.ticketId);

            return MatchEvent.MatchRequestSucceeded;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Failed to request matchmaking. Error: {e.Message}");
            return MatchEvent.MatchRequestFailed;
        }
    }
}
