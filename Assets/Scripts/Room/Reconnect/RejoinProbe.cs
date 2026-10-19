using Cysharp.Threading.Tasks;
using System.Threading;

namespace LOP
{
    public enum RejoinAnswer { Joinable, MatchOver, NotReady }

    /// <summary>재접속 시도 1회의 판정. 시도마다 로비에 새로 묻는다 — 끊긴 사이 판이 끝났을 수 있다.</summary>
    public static class RejoinProbe
    {
        /// <param name="roomId">끊긴 방. 모르면 null — 그때는 로비가 말한 방을 본다.</param>
        /// <param name="roomToCheck">방 상태를 더 물어볼 방 id. 판이 끝났으면 null.</param>
        public static RejoinAnswer FromLocation(UserLocationDto location, string roomId, out string roomToCheck)
        {
            roomToCheck = null;
            if (location?.location != Location.GameRoom || location.locationDetail is not GameRoomLocationDetail detail)
            {
                return RejoinAnswer.MatchOver;
            }
            //  다른 방이면 로비에 맡긴다 — 로비가 그 방에 넣어 준다.
            if (roomId != null && detail.gameRoomId != roomId)
            {
                return RejoinAnswer.MatchOver;
            }
            roomToCheck = detail.gameRoomId;
            return RejoinAnswer.NotReady;
        }

        public static RejoinAnswer FromRoom(RoomJoinableResponse response)
        {
            if (response == null) return RejoinAnswer.NotReady;
            if (response.code == ResponseCode.SUCCESS) return RejoinAnswer.Joinable;
            //  지워졌거나 닫혔거나 터진 방은 기다려도 안 바뀐다.
            if (response.code == ResponseCode.ROOM_NOT_EXIST) return RejoinAnswer.MatchOver;
            if (response.room != null && (response.room.status == RoomStatus.Closed || response.room.status == RoomStatus.Error))
            {
                return RejoinAnswer.MatchOver;
            }
            return RejoinAnswer.NotReady;
        }

        /// <summary>실제 로비 호출. 들어갈 수 있으면 방 정보가 RoomDataStore에 채워진다(응답 구독). 네트워크 오류는 예외로 올린다.</summary>
        public static async UniTask<RejoinAnswer> AskLobbyAsync(string userId, string roomId, CancellationToken ct)
        {
            var location = await WebAPI.GetUserLocation(userId, ct);
            if (location.code != ResponseCode.SUCCESS) return RejoinAnswer.NotReady;

            var answer = FromLocation(location.userLocation, roomId, out var roomToCheck);
            if (roomToCheck == null) return answer;

            return FromRoom(await WebAPI.CheckRoomJoinable(roomToCheck, ct));
        }
    }
}
