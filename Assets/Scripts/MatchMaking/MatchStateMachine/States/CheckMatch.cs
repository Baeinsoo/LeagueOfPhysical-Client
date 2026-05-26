using GameFramework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CheckMatch : State<MatchEvent>
    {
        private readonly IObjectResolver resolver;
        private readonly IUserDataStore userDataStore;

        public CheckMatch(IObjectResolver resolver, IUserDataStore userDataStore)
        {
            this.resolver = resolver;
            this.userDataStore = userDataStore;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.LocationIsGameRoom => resolver.Resolve<InGameRoom>(),
                MatchEvent.LocationIsWaitingRoom => resolver.Resolve<InWaitingRoom>(),
                MatchEvent.LocationIsNone => resolver.Resolve<Idle>(),
                _ => this,
            };
        }

        protected override async Task OnExecuteAsync(CancellationToken ct)
        {
            try
            {
                var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id);

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (getUserLocation.response.code != ResponseCode.SUCCESS)
                {
                    Debug.LogError($"Failed to retrieve user information. code: {getUserLocation.response.code}");
                    return;
                }

                switch (getUserLocation.response.userLocation.location)
                {
                    case Location.WaitingRoom:
                        FSM.Fire(MatchEvent.LocationIsWaitingRoom);
                        break;

                    case Location.GameRoom:
                        FSM.Fire(MatchEvent.LocationIsGameRoom);
                        break;

                    default:
                        FSM.Fire(MatchEvent.LocationIsNone);
                        break;
                }
            }
            catch (WebRequestException e)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                Debug.LogError($"Failed to retrieve user information. Error: {e.Message}");
            }
        }
    }
}
