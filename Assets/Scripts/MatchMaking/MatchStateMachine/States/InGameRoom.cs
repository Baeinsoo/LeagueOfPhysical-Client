using Cysharp.Threading.Tasks;
using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class InGameRoom : State<MatchEvent>
    {
        private const int CHECK_INTERVAL = 1;   //  sec

        private readonly IObjectResolver resolver;
        private readonly IUserDataStore userDataStore;
        private readonly RoomConnector roomConnector;

        public InGameRoom(IObjectResolver resolver, IUserDataStore userDataStore, RoomConnector roomConnector)
        {
            this.resolver = resolver;
            this.userDataStore = userDataStore;
            this.roomConnector = roomConnector;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.RecheckRequested => resolver.Resolve<CheckMatch>(),
                _ => this,
            };
        }

        protected override async Task OnExecuteAsync(CancellationToken ct)
        {
            if (userDataStore.userLocation.locationDetail is not GameRoomLocationDetail gameRoomLocationDetail)
            {
                Debug.LogError("User is not in a game room.");
                FSM.Fire(MatchEvent.RecheckRequested);
                return;
            }

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await roomConnector.TryToEnterRoomById(gameRoomLocationDetail.gameRoomId);

                    await UniTask.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                //  Left the room scene (or state exited); stop retrying.
            }
        }
    }
}
