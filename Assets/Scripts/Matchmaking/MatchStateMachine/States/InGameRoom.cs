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
        private readonly Func<CheckMatch> checkMatch;
        private readonly IUserLocationService userLocationService;
        private readonly RoomConnector roomConnector;
        private readonly AppStateMachine appStateMachine;
        private readonly IRunOutDirector runOutDirector;

        public InGameRoom(Func<CheckMatch> checkMatch, IUserLocationService userLocationService, RoomConnector roomConnector, AppStateMachine appStateMachine, IRunOutDirector runOutDirector)
        {
            this.checkMatch = checkMatch;
            this.userLocationService = userLocationService;
            this.roomConnector = roomConnector;
            this.appStateMachine = appStateMachine;
            this.runOutDirector = runOutDirector;
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
            if (userLocationService.UserLocation.CurrentValue.locationDetail is not GameRoomLocationDetail gameRoomLocationDetail)
            {
                Debug.LogError("User is not in a game room.");
                return MatchEvent.RecheckRequested;
            }

            bool entered = await RunOutEntry.EnterAsync(
                roomConnector.TryToEnterRoomById(gameRoomLocationDetail.gameRoomId).AsUniTask(), runOutDirector, ct);
            if (entered)
            {
                //  매치 진입 확정 → 앱 FSM이 Room 씬을 로드(InMatch). 출전 연출은 이미 끝났다(RunOutEntry가 함께 기다림).
                //  씬 언로드로 이 매칭 FSM은 LobbyLifetimeScope.OnDestroy에서 정리되므로 자기 전이는 하지 않는다(null 반환).
                appStateMachine.Fire(AppEvent.MatchFound);
                return null;
            }

            //  입장 실패 → 위치 재확인.
            return MatchEvent.RecheckRequested;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            runOutDirector.Reset();
            Debug.LogError($"Error while entering game room. Error: {e.Message}");
            return MatchEvent.RecheckRequested;
        }
    }
}
