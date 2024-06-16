using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class InWaitingRoom : MonoState
    {
        private const int CHECK_INTERVAL = 2;   //  sec

        public override IState GetNext<I>(I input)
        {
            if (!input.TryParse(out MatchStateInput matchStateInput))
            {
                throw new ArgumentException($"Invalid input. input: {input}");
            }

            switch (matchStateInput)
            {
                case MatchStateInput.CancelMatchmaking:
                    return gameObject.GetOrAddComponent<CancelMatchmaking>();

                case MatchStateInput.InGameRoom:
                    return gameObject.GetOrAddComponent<InGameRoom>();

                case MatchStateInput.Idle:
                    return gameObject.GetOrAddComponent<Idle>();
            }

            throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}");
        }

        protected override IEnumerator OnExecute()
        {
            while (true)
            {
                var getUser = WebAPI.GetUser(Data.User.user.id);
                yield return getUser;

                if (getUser.isSuccess == false)
                {
                    throw new Exception($"유저 정보를 받아오는데 실패하였습니다. error: {getUser.error}");
                }

                Data.User.user = getUser.response.user;

                switch (getUser.response.user.location)
                {
                    case Location.InGameRoom:
                        FSM.ProcessInput(MatchStateInput.InGameRoom);
                        yield break;

                    case Location.InWaitingRoom:
                        var verifyUserLocation = WebAPI.VerifyUserLocation(Data.User.user.id);
                        yield return verifyUserLocation;
                        break;

                    default:
                        FSM.ProcessInput(MatchStateInput.Idle);
                        yield break;
                }

                yield return new WaitForSeconds(CHECK_INTERVAL);
            }
        }
    }
}
