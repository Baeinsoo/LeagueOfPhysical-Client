using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class InGameRoom : MonoState
    {
        private const int CHECK_INTERVAL = 2;   //  sec

        public override IState GetNext<I>(I input)
        {
            if (!input.TryParse(out MatchStateInput matchStateInput))
            {
                throw new ArgumentException($"Invalid input. input: {input}");
            }

            throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}");
        }

        protected override IEnumerator OnExecute()
        {
            var roomId = (Data.User.user.locationDetail as GameRoomLocationDetail).gameRoomId;

            while (true)
            {
                var getRoom = WebAPI.GetRoom(roomId);
                yield return getRoom;

                if (getRoom.isSuccess == false)
                {
                    Debug.LogError($"Room 정보를 받아오는데 실패하였습니다. error: {getRoom.error}");
                    FSM.ProcessInput(MatchStateInput.CheckMatchState);
                    yield break;
                }

                if (getRoom.response.room.status == RoomStatus.Ready || getRoom.response.room.status == RoomStatus.Playing)
                {
                    //RoomConnector.Instance.TryToEnterRoomById(roomId);
                    yield break;
                }

                yield return new WaitForSeconds(CHECK_INTERVAL);
            }
        }
    }
}
