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
            if (input is not MatchStateInput matchStateInput)
            {
                throw new ArgumentException($"Invalid input type. Expected MatchStateInput, got {typeof(I).Name}");
            }

            return matchStateInput switch
            {
                MatchStateInput.InWaitingRoom => gameObject.GetOrAddComponent<InWaitingRoom>(),
                MatchStateInput.InGameRoom => gameObject.GetOrAddComponent<InGameRoom>(),
                MatchStateInput.Idle => gameObject.GetOrAddComponent<Idle>(),
                _ => throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}")
            };
        }

        protected override IEnumerator OnExecute()
        {
            var getUserLocation = WebAPI.GetUserLocation(Data.User.user.id);
            yield return getUserLocation;

            if (getUserLocation.isSuccess == false || getUserLocation.response.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"Failed to retrieve user information. Error: {getUserLocation.error}");
            }

            Data.User.userLocation = MapperConfig.mapper.Map<UserLocation>(getUserLocation.response.userLocation);

            switch (getUserLocation.response.userLocation.location)
            {
                case Location.WaitingRoom:
                    FSM.ProcessInput(MatchStateInput.InWaitingRoom);
                    break;

                case Location.GameRoom:
                    FSM.ProcessInput(MatchStateInput.InGameRoom);
                    break;

                default:
                    FSM.ProcessInput(MatchStateInput.Idle);
                    break;
            }
        }
    }
}
