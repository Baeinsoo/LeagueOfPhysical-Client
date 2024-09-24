using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class InGameRoom : MonoState
    {
        private const int CHECK_INTERVAL = 1;   //  sec

        public override IState GetNext<I>(I input)
        {
            if (input is not MatchStateInput matchStateInput)
            {
                throw new ArgumentException($"Invalid input type. Expected MatchStateInput, got {typeof(I).Name}");
            }

            throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}");
        }

        protected override IEnumerator OnExecute()
        {
            if (Data.User.userLocation.locationDetail is not GameRoomLocationDetail gameRoomLocationDetail)
            {
                Debug.LogError("User is not in a game room.");
                FSM.ProcessInput(MatchStateInput.CheckMatchState);
                yield break;
            }

            while (true)
            {
                yield return new RoomConnector().TryToEnterRoomById(gameRoomLocationDetail.gameRoomId).AsIEnumerator();

                yield return new WaitForSeconds(CHECK_INTERVAL);
            }
        }
    }
}
