using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class InWaitingRoom : MonoState
    {
        private const int CHECK_INTERVAL = 1;   //  sec

        [Inject]
        private IUserDataStore userDataStore;

        public override IState GetNext<I>(I input)
        {
            if (input is not MatchStateInput matchStateInput)
            {
                throw new ArgumentException($"Invalid input type. Expected MatchStateInput, got {typeof(I).Name}");
            }

            return matchStateInput switch
            {
                MatchStateInput.CancelMatchmaking => gameObject.GetOrAddComponentWithInject<CancelMatchmaking>(),
                MatchStateInput.InGameRoom => gameObject.GetOrAddComponentWithInject<InGameRoom>(),
                MatchStateInput.Idle => gameObject.GetOrAddComponentWithInject<Idle>(),
                _ => throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}")
            };
        }

        protected override IEnumerator OnExecute()
        {
            while (true)
            {
                var getUserLocation = WebAPI.GetUserLocation(userDataStore.user.id);
                yield return getUserLocation;

                if (!getUserLocation.isSuccess)
                {
                    throw new Exception($"Failed to retrieve user information. Error: {getUserLocation.error}");
                }

                switch (getUserLocation.response.userLocation.location)
                {
                    case Location.GameRoom:
                        FSM.ProcessInput(MatchStateInput.InGameRoom);
                        yield break;

                    case Location.WaitingRoom:
                        // User is still in the waiting room, continue waiting
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
