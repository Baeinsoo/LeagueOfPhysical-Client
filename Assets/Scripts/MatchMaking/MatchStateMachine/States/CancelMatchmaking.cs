using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class CancelMatchmaking : MonoState
    {
        public override IState GetNext<I>(I input)
        {
            if (!input.TryParse(out MatchStateInput matchStateInput))
            {
                throw new ArgumentException($"Invalid input. input: {input}");
            }

            switch (matchStateInput)
            {
                case MatchStateInput.Idle:
                    return gameObject.GetOrAddComponent<Idle>();

                case MatchStateInput.InGameRoom:
                    return gameObject.GetOrAddComponent<InGameRoom>();

                case MatchStateInput.CheckMatchState:
                    return gameObject.GetOrAddComponent<CheckMatchState>();
            }

            throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}");
        }

        protected override IEnumerator OnExecute()
        {
            var waitingRoomLocationDetail = Data.User.user.locationDetail as WaitingRoomLocationDetail;
            var matchmakingTicketId = waitingRoomLocationDetail.matchmakingTicketId;

            var cancelMatchmaking = WebAPI.CancelMatchmaking(matchmakingTicketId);
            yield return cancelMatchmaking;

            if (cancelMatchmaking.isSuccess == false)
            {
                Debug.LogError($"Matchmaking 취소에 실패하였습니다. error: {cancelMatchmaking.error}");
                FSM.ProcessInput(MatchStateInput.CheckMatchState);
                yield break;
            }

            switch (cancelMatchmaking.response.code)
            {
                case ResponseCode.ALREADY_IN_GAME:
                    FSM.ProcessInput(MatchStateInput.InGameRoom);
                    yield break;

                case ResponseCode.MATCH_MAKING_TICKET_NOT_EXIST:
                    Debug.LogError("Matchmaking 티켓이 존재하지 않습니다.");
                    yield break;

                case ResponseCode.NOT_MATCH_MAKING_STATE:
                    Debug.LogError("Matchmaking 상태가 아니었습니다.");
                    yield break;
            }

            FSM.ProcessInput(MatchStateInput.CheckMatchState);
        }
    }
}
