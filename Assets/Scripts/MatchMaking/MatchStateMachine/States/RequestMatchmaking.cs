using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class RequestMatchmaking : MonoState
    {
        public override IState GetNext<I>(I input)
        {
            if (!input.TryParse(out MatchStateInput matchStateInput))
            {
                throw new ArgumentException($"Invalid input. input: {input}");
            }

            switch (matchStateInput)
            {
                case MatchStateInput.InWaitingRoom:
                    return gameObject.GetOrAddComponent<InWaitingRoom>();

                case MatchStateInput.CheckMatchState:
                    return gameObject.GetOrAddComponent<CheckMatchState>();
            }

            throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}");
        }

        protected override IEnumerator OnExecute()
        {
            //var matchSelectData = SceneDataContainer.Get<MatchSelectData>();

            //if (LOPSettings.Get().connectLocalServer)
            //{
            //    RoomConnector.Instance.TryToEnterRoomById("EditorTestRoom");
            //    yield break;
            //}

            var requestMatchmaking = WebAPI.RequestMatchmaking(new MatchmakingRequest
            {
                userId = Data.User.user.id,
                matchType = Data.MatchMaking.matchType,
                subGameId = Data.MatchMaking.subGameId,
                mapId = Data.MatchMaking.mapId,
            });

            yield return requestMatchmaking;

            if (requestMatchmaking.isSuccess == false)
            {
                Debug.LogError($"매치메이킹 요청에 실패하였습니다. error: {requestMatchmaking.error}");
                FSM.ProcessInput(MatchStateInput.CheckMatchState);
                yield break;
            }

            if (requestMatchmaking.response.code != ResponseCode.SUCCESS)
            {
                Debug.LogError($"매치메이킹 요청에 실패하였습니다. response.code: {requestMatchmaking.response.code}");
                FSM.ProcessInput(MatchStateInput.CheckMatchState);
                yield break;
            }

            FSM.ProcessInput(MatchStateInput.InWaitingRoom);
        }
    }
}
