using GameFramework.Auth;

namespace LOP
{
    /// <summary>로그인으로 확보한 세션. 네트워크 접속 세션(GameFramework.ISession)과 다른 개념이다.</summary>
    public class AuthSession
    {
        public string UserId { get; }
        public AccessTokenInfo Token { get; }

        public AuthSession(string userId, AccessTokenInfo token)
        {
            UserId = userId;
            Token = token;
        }
    }
}
