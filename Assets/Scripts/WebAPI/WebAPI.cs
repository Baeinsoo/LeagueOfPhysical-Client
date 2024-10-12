using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class WebAPI
    {
        #region Lobby
        public static WebRequest<JoinLobbyResponse> JoinLobby(string userId)
        {
            return new WebRequestBuilder<JoinLobbyResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/lobby/join/{userId}")
                .SetMethod(HttpMethod.PUT)
                .Build();
        }

        public static WebRequest<LeaveLobbyResponse> LeaveLobby(string userId)
        {
            return new WebRequestBuilder<LeaveLobbyResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/lobby/leave/{userId}")
                .SetMethod(HttpMethod.PUT)
                .Build();
        }
        #endregion

        #region MatchmakingTicket
        public static WebRequest<MatchmakingResponse> RequestMatchmaking(MatchmakingRequest request)
        {
            return new WebRequestBuilder<MatchmakingResponse>()
                .SetUri($"{EnvironmentSettings.active.matchmakingBaseURL}/matchmaking")
                .SetMethod(HttpMethod.POST)
                .SetRequestBody(request)
                .Build();
        }

        public static WebRequest<CancelMatchmakingResponse> CancelMatchmaking(string ticketId)
        {
            return new WebRequestBuilder<CancelMatchmakingResponse>()
                .SetUri($"{EnvironmentSettings.active.matchmakingBaseURL}/matchmaking/{ticketId}")
                .SetMethod(HttpMethod.DELETE)
                .Build();
        }

        public static WebRequest<GetMatchResponse> GetMatch(string matchId)
        {
            return new WebRequestBuilder<GetMatchResponse>()
                .SetUri($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}")
                .SetMethod(HttpMethod.GET)
                .Build();
        }
        #endregion

        #region User
        public static WebRequest<GetUserResponse> GetUser(string userId)
        {
            return new WebRequestBuilder<GetUserResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}")
                .SetMethod(HttpMethod.GET)
                .Build();
        }

        public static WebRequest<CreateUserResponse> CreateUser(CreateUserRequest request)
        {
            return new WebRequestBuilder<CreateUserResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user")
                .SetMethod(HttpMethod.POST)
                .SetRequestBody(request)
                .Build();
        }

        public static WebRequest<GetUserLocationResponse> GetUserLocation(string userId)
        {
            return new WebRequestBuilder<GetUserLocationResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/location/")
                .SetMethod(HttpMethod.GET)
                .SetDeserialize(GetUserLocationResponse.Deserialize)
                .Build();
        }
        #endregion

        #region Room
        public static WebRequest<GetRoomResponse> GetRoom(string roomId)
        {
            return new WebRequestBuilder<GetRoomResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}")
                .SetMethod(HttpMethod.GET)
                .Build();
        }

        public static WebRequest<RoomJoinableResponse> CheckRoomJoinable(string roomId)
        {
            return new WebRequestBuilder<RoomJoinableResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}/joinable")
                .SetMethod(HttpMethod.GET)
                .Build();
        }
        #endregion
    }
}
