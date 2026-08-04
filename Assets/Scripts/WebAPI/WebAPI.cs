using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class WebAPI
    {
        #region Auth
        // /auth 응답은 code 필드가 없어 ResponseBase 규약을 안 따름 — 성공 여부는 HTTP 상태 코드(isSuccess)로 판단

        //  이 두 호출은 반드시 NoAuth를 쓴다 — 로그인/가입 자체에 Bearer를 실으면, 갱신이 밀린
        //  상태에서 만료 임박/구 토큰이 얹혀 나가 서버가 401을 줄 수 있다. 그 401을
        //  AuthenticationService가 "자격증명이 거부됐다"로 오판하면 멀쩡한 계정을 지우고
        //  새로 가입해버린다(계정 유실). 자세한 이유는 LOPWebRequestInterceptor.NoAuth 참고.
        public static WebRequest<AnonymousSignInResponse> SignInAnonymous()
        {
            return new WebRequestBuilder<AnonymousSignInResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/auth/anonymous")
                .SetMethod(HttpMethod.POST)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.NoAuth)
                .Build();
        }

        public static WebRequest<LoginResponse> Login(LoginRequest request)
        {
            return new WebRequestBuilder<LoginResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/auth/login")
                .SetMethod(HttpMethod.POST)
                .SetRequestBody(request)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.NoAuth)
                .Build();
        }
        #endregion

        #region Lobby
        public static WebRequest<JoinLobbyResponse> JoinLobby(string userId)
        {
            return new WebRequestBuilder<JoinLobbyResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/lobby/join/{userId}")
                .SetMethod(HttpMethod.PUT)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<LeaveLobbyResponse> LeaveLobby(string userId)
        {
            return new WebRequestBuilder<LeaveLobbyResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/lobby/leave/{userId}")
                .SetMethod(HttpMethod.PUT)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
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
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<CancelMatchmakingResponse> CancelMatchmaking(string ticketId)
        {
            return new WebRequestBuilder<CancelMatchmakingResponse>()
                .SetUri($"{EnvironmentSettings.active.matchmakingBaseURL}/matchmaking/{ticketId}")
                .SetMethod(HttpMethod.DELETE)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<GetMatchResponse> GetMatch(string matchId)
        {
            return new WebRequestBuilder<GetMatchResponse>()
                .SetUri($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}")
                .SetMethod(HttpMethod.GET)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }
        #endregion

        #region User
        public static WebRequest<GetUserResponse> GetUser(string userId)
        {
            return new WebRequestBuilder<GetUserResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}")
                .SetMethod(HttpMethod.GET)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<GetUserResponse> GetUserByUsername(string username)
        {
            return new WebRequestBuilder<GetUserResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user/username/{username}")
                .SetMethod(HttpMethod.GET)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<CreateUserResponse> CreateUser(CreateUserRequest request)
        {
            return new WebRequestBuilder<CreateUserResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user")
                .SetMethod(HttpMethod.POST)
                .SetRequestBody(request)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<GetUserLocationResponse> GetUserLocation(string userId)
        {
            return new WebRequestBuilder<GetUserLocationResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/location/")
                .SetMethod(HttpMethod.GET)
                .SetDeserialize(GetUserLocationResponse.Deserialize)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<GetUserStatsResponse> GetUserStats(string userId, int queueId)
        {
            return new WebRequestBuilder<GetUserStatsResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/stats?queueId={queueId}")
                .SetMethod(HttpMethod.GET)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }
        #endregion

        #region Room
        public static WebRequest<GetRoomResponse> GetRoom(string roomId)
        {
            return new WebRequestBuilder<GetRoomResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}")
                .SetMethod(HttpMethod.GET)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<RoomJoinableResponse> CheckRoomJoinable(string roomId)
        {
            return new WebRequestBuilder<RoomJoinableResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}/joinable")
                .SetMethod(HttpMethod.GET)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }
        #endregion
    }
}
