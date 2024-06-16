using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;

namespace LOP
{
    public class CheckMatchState : MonoState
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

                case MatchStateInput.InGameRoom:
                    return gameObject.GetOrAddComponent<InGameRoom>();

                case MatchStateInput.Idle:
                    return gameObject.GetOrAddComponent<Idle>();
            }

            throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}");
        }

        protected override IEnumerator OnExecute()
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
                case Location.InWaitingRoom:
                    FSM.ProcessInput(MatchStateInput.InWaitingRoom);
                    break;

                case Location.InGameRoom:
                    FSM.ProcessInput(MatchStateInput.InGameRoom);
                    break;

                default:
                    FSM.ProcessInput(MatchStateInput.Idle);
                    break;
            }
        }
    }
}
