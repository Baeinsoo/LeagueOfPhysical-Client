using Cysharp.Threading.Tasks;
using GameFramework.Http;
using MessagePipe;
using System;
using System.Threading;

namespace LOP
{
    public class WebAPI
    {
        //  static이라 DI가 안 된다 — RootLifetimeScope가 기동 시 공급자를 꽂아 준다.
        private static IAccessTokenProvider accessTokenProvider;

        //  인증을 붙이는 클라이언트와 절대 안 붙이는 클라이언트를 따로 둔다. 어느 쪽을 쓸지는
        //  호출부가 스스로 고른다 — URL 문자열로 판단하면 경로 접두사가 바뀔 때 조용히 깨진다
        //  (실제로 /lobby 접두사 때문에 죽은 검사가 된 적이 있다).
        private static readonly HttpClient authorized =
            new HttpClient(new BearerTokenHandler(new UnityWebRequestHandler(),
                new DeferredAccessTokenProvider(() => accessTokenProvider)));

        private static readonly HttpClient anonymous = new HttpClient(new UnityWebRequestHandler());

        public static void SetAccessTokenProvider(IAccessTokenProvider provider)
        {
            accessTokenProvider = provider;
        }

        //  응답을 역직렬화한 뒤 전역 발행까지 한다 — UserDataStore/RoomDataStore가 이걸 구독해
        //  자기 상태를 채운다. 이 발행이 끊기면 유저 데이터가 아예 안 들어온다.
        private static async UniTask<T> SendAsync<T>(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            T response = await client.SendAsync<T>(request, cancellationToken);
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
            return response;
        }

        private static async UniTask<T> SendAsync<T>(HttpClient client, HttpRequestMessage request, Func<string, T> deserialize, CancellationToken cancellationToken)
        {
            T response = await client.SendAsync(request, deserialize, cancellationToken);
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
            return response;
        }

        #region Auth
        //  이 두 호출은 반드시 anonymous를 쓴다 — 로그인/가입 자체에 Bearer를 실으면, 갱신이 밀린
        //  상태에서 만료 임박/구 토큰이 얹혀 나가 서버가 401을 줄 수 있다. 그 401을
        //  AuthenticationService가 "자격증명이 거부됐다"로 오판하면 멀쩡한 계정을 지우고 새로
        //  가입해버린다(계정 유실).
        public static UniTask<AnonymousSignInResponse> SignInAnonymous(CancellationToken cancellationToken = default)
            => SendAsync<AnonymousSignInResponse>(anonymous,
                HttpRequestMessage.Post($"{EnvironmentSettings.active.lobbyBaseURL}/auth/anonymous"), cancellationToken);

        public static UniTask<LoginResponse> Login(LoginRequest request, CancellationToken cancellationToken = default)
            => SendAsync<LoginResponse>(anonymous,
                HttpRequestMessage.Post($"{EnvironmentSettings.active.lobbyBaseURL}/auth/login", request), cancellationToken);
        #endregion

        #region Lobby
        public static UniTask<JoinLobbyResponse> JoinLobby(string userId, CancellationToken cancellationToken = default)
            => SendAsync<JoinLobbyResponse>(authorized,
                HttpRequestMessage.Put($"{EnvironmentSettings.active.lobbyBaseURL}/lobby/join/{userId}"), cancellationToken);

        #endregion

        #region MatchmakingTicket
        public static UniTask<MatchmakingResponse> RequestMatchmaking(MatchmakingRequest request, CancellationToken cancellationToken = default)
            => SendAsync<MatchmakingResponse>(authorized,
                HttpRequestMessage.Post($"{EnvironmentSettings.active.matchmakingBaseURL}/matchmaking", request), cancellationToken);

        public static UniTask<CancelMatchmakingResponse> CancelMatchmaking(string ticketId, CancellationToken cancellationToken = default)
            => SendAsync<CancelMatchmakingResponse>(authorized,
                HttpRequestMessage.Delete($"{EnvironmentSettings.active.matchmakingBaseURL}/matchmaking/{ticketId}"), cancellationToken);

        public static UniTask<GetMatchResponse> GetMatch(string matchId, CancellationToken cancellationToken = default)
            => SendAsync<GetMatchResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}"), cancellationToken);
        #endregion

        #region User
        public static UniTask<GetUserResponse> GetUser(string userId, CancellationToken cancellationToken = default)
            => SendAsync<GetUserResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}"), cancellationToken);



        public static UniTask<GetUserLocationResponse> GetUserLocation(string userId, CancellationToken cancellationToken = default)
            => SendAsync(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/location/"),
                GetUserLocationResponse.Deserialize, cancellationToken);

        //  전역 발행을 쓴다 — UserDataStore가 이 응답을 구독해 user를 갱신한다(GetUser와 같은 경로).
        //  RootLifetimeScope에 브로커를 등록해 둬야 한다. 안 하면 호출은 성공하고 그 뒤 발행에서 터진다.
        public static UniTask<ChangeDisplayNameResponse> ChangeDisplayName(string userId, string displayName, CancellationToken cancellationToken = default)
            => SendAsync<ChangeDisplayNameResponse>(authorized,
                HttpRequestMessage.Put($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/display-name",
                    new ChangeDisplayNameRequest { displayName = displayName }), cancellationToken);

        public static UniTask<GetUserRatingResponse> GetUserRating(string userId, int queueId, CancellationToken cancellationToken = default)
            => SendAsync<GetUserRatingResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/rating?queueId={queueId}"), cancellationToken);

        //  전역 발행(SendAsync)을 쓰지 않는다 — 이 응답을 구독해 상태를 채우는 스토어가 없고,
        //  브로커가 등록 안 된 타입을 발행하면 조회 자체는 성공했는데 그 뒤에 예외가 난다.
        //  부르는 쪽(프로필 ViewModel)이 반환값을 그대로 쓴다.
        public static UniTask<GetMatchHistoryResponse> GetMatchHistory(string userId, int limit, CancellationToken cancellationToken = default)
            => authorized.SendAsync<GetMatchHistoryResponse>(
                HttpRequestMessage.Get($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/matches?limit={limit}"), cancellationToken);
        #endregion

        #region Room

        public static UniTask<RoomJoinableResponse> CheckRoomJoinable(string roomId, CancellationToken cancellationToken = default)
            => SendAsync<RoomJoinableResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}/joinable"), cancellationToken);
        #endregion
    }
}
