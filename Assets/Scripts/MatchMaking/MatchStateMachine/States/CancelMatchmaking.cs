using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CancelMatchmaking : MonoState
    {
        [Inject]
        private IUserDataStore userDataStore;

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
                MatchStateInput.Idle => gameObject.GetOrAddComponent<Idle>(),
                MatchStateInput.InGameRoom => gameObject.GetOrAddComponent<InGameRoom>(),
                MatchStateInput.CheckMatchState => gameObject.GetOrAddComponent<CheckMatchState>(),
                _ => throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}")
            };
        }

        protected override IEnumerator OnExecute()
        {
            if (userDataStore.userLocation.locationDetail is not WaitingRoomLocationDetail waitingRoomLocationDetail)
            {
                Debug.LogError("User is not in a waiting room.");
                FSM.ProcessInput(MatchStateInput.CheckMatchState);
                yield break;
            }

            var cancelMatchmaking = WebAPI.CancelMatchmaking(waitingRoomLocationDetail.matchmakingTicketId);
            yield return cancelMatchmaking;

            if (!cancelMatchmaking.isSuccess)
            {
                Debug.LogError($"Failed to cancel matchmaking. Error: {cancelMatchmaking.error}");
                FSM.ProcessInput(MatchStateInput.CheckMatchState);
                yield break;
            }

            switch (cancelMatchmaking.response.code)
            {
                case ResponseCode.ALREADY_IN_GAME:
                    FSM.ProcessInput(MatchStateInput.InGameRoom);
                    break;

                case ResponseCode.MATCH_MAKING_TICKET_NOT_EXIST:
                    Debug.LogError("Matchmaking ticket does not exist.");
                    break;

                case ResponseCode.NOT_MATCH_MAKING_STATE:
                    Debug.LogError("Not in matchmaking state.");
                    break;

                default:
                    FSM.ProcessInput(MatchStateInput.CheckMatchState);
                    break;
            }
        }
    }
}
