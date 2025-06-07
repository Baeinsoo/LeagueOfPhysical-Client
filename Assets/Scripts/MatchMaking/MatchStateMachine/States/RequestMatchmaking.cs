using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class RequestMatchmaking : MonoState
    {
        [Inject]
        private IUserDataStore userDataStore;

        [Inject]
        private IMatchMakingDataStore matchMakingDataStore;

        private void Awake()
        {
            SceneLifetimeScope.Inject(this);
        }

        public override IState GetNext<I>(I input)
        {
            if (input is not MatchStateInput matchStateInput)
            {
                throw new ArgumentException($"Invalid input type. Expected MatchStateInput, got {typeof(I).Name}");
            }

            return matchStateInput switch
            {
                MatchStateInput.InWaitingRoom => gameObject.GetOrAddComponent<InWaitingRoom>(),
                MatchStateInput.CheckMatchState => gameObject.GetOrAddComponent<CheckMatchState>(),
                _ => throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}")
            };
        }

        protected override IEnumerator OnExecute()
        {
            var matchmakingRequest = new MatchmakingRequest
            {
                userId = userDataStore.user.id,
                matchType = matchMakingDataStore.matchType,
                subGameId = matchMakingDataStore.subGameId,
                mapId = matchMakingDataStore.mapId,
            };

            var requestMatchmaking = WebAPI.RequestMatchmaking(matchmakingRequest);
            yield return requestMatchmaking;

            if (!requestMatchmaking.isSuccess || requestMatchmaking.response.code != ResponseCode.SUCCESS)
            {
                var errorMessage = !requestMatchmaking.isSuccess
                    ? $"Failed to request matchmaking. Error: {requestMatchmaking.error}"
                    : $"Matchmaking request failed. Response code: {requestMatchmaking.response.code}";

                Debug.LogError(errorMessage);
                FSM.ProcessInput(MatchStateInput.CheckMatchState);
                yield break;
            }

            FSM.ProcessInput(MatchStateInput.InWaitingRoom);
        }
    }
}
