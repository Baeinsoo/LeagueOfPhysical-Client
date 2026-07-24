using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class InGameRoom : State<MatchEvent>
    {
        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserDataStore userDataStore;
        private readonly RoomConnector roomConnector;
        private readonly AppStateMachine appStateMachine;

        public InGameRoom(Func<CheckMatch> checkMatch, IUserDataStore userDataStore, RoomConnector roomConnector, AppStateMachine appStateMachine)
        {
            this.checkMatch = checkMatch;
            this.userDataStore = userDataStore;
            this.roomConnector = roomConnector;
            this.appStateMachine = appStateMachine;
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

            if (await roomConnector.TryToEnterRoomById(gameRoomLocationDetail.gameRoomId))
            {
                //  매치 진입 확정 → 앱 FSM이 Room 씬을 로드(InMatch). 씬 언로드로 이 매칭 FSM은
                //  LobbyLifetimeScope.OnDestroy에서 정리되므로 자기 전이는 하지 않는다(null 반환).
                appStateMachine.Fire(AppEvent.MatchFound);
                return null;
            }

            //  입장 실패 → 위치 재확인.
            return MatchEvent.RecheckRequested;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Error while entering game room. Error: {e.Message}");
            return MatchEvent.RecheckRequested;
        }
    }
}
