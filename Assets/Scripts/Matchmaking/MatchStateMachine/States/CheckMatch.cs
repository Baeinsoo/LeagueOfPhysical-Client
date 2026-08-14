using GameFramework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LOP
{
    public class CheckMatch : State<MatchEvent>
    {
        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<InMatchmaking> inMatchmaking;
        private readonly Func<Idle> idle;
        private readonly IUserLocationService userLocationService;

        public CheckMatch(Func<InGameRoom> inGameRoom, Func<InMatchmaking> inMatchmaking, Func<Idle> idle, IUserLocationService userLocationService)
        {
            this.inGameRoom = inGameRoom;
            this.inMatchmaking = inMatchmaking;
            this.idle = idle;
            this.userLocationService = userLocationService;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.LocationIsGameRoom => inGameRoom(),
                MatchEvent.LocationIsMatchmaking => inMatchmaking(),
                MatchEvent.LocationIsNone => idle(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            //  재시도는 서비스가 한다. 여기서는 결과를 전이로만 옮긴다.
            if (await userLocationService.RefreshAsync(ct))
            {
                return ToEvent(userLocationService.UserLocation.CurrentValue.location);
            }

            //  반복 실패 → 초기 화면(Idle)으로 안전 복귀.
            return MatchEvent.LocationIsNone;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to retrieve user information. Error: {e.Message}");
            return MatchEvent.LocationIsNone;
        }

        private static MatchEvent ToEvent(Location location)
        {
            return location switch
            {
                Location.Matchmaking => MatchEvent.LocationIsMatchmaking,
                Location.GameRoom => MatchEvent.LocationIsGameRoom,
                _ => MatchEvent.LocationIsNone,
            };
        }
    }
}
