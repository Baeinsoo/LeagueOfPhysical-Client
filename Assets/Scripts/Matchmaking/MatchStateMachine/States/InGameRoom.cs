using Cysharp.Threading.Tasks;
using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class InGameRoom : State<MatchEvent>
    {
        private const int CHECK_INTERVAL = 1;   //  sec
        private const int MAX_ATTEMPTS = 10;

        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserDataStore userDataStore;
        private readonly RoomConnector roomConnector;

        public InGameRoom(Func<CheckMatch> checkMatch, IUserDataStore userDataStore, RoomConnector roomConnector)
        {
            this.checkMatch = checkMatch;
            this.userDataStore = userDataStore;
            this.roomConnector = roomConnector;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.RecheckRequested => checkMatch(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            if (userDataStore.userLocation.locationDetail is not GameRoomLocationDetail gameRoomLocationDetail)
            {
                Debug.LogError("User is not in a game room.");
                return MatchEvent.RecheckRequested;
            }

            for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
            {
                await roomConnector.TryToEnterRoomById(gameRoomLocationDetail.gameRoomId);
                await UniTask.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL), cancellationToken: ct);
            }

            //  여러 번 시도해도 입장 실패 → 위치 재확인.
            Debug.LogError($"Failed to enter game room after {MAX_ATTEMPTS} attempts.");
            return MatchEvent.RecheckRequested;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Error while entering game room. Error: {e.Message}");
            return MatchEvent.RecheckRequested;
        }
    }
}
