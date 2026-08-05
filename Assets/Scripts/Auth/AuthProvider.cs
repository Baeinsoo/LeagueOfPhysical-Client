using System;

namespace LOP
{
    public enum AuthProvider
    {
        Anonymous = 0,
        GooglePlayGames = 1,
        GameCenter = 2,
    }

    public static class AuthProviderExtensions
    {
        //  서버 Prisma enum 값과 정확히 일치해야 한다. 어긋나면 400/501로 떨어진다.
        public static string ToWireString(this AuthProvider provider)
        {
            switch (provider)
            {
                case AuthProvider.Anonymous: return "ANONYMOUS";
                case AuthProvider.GooglePlayGames: return "GOOGLE_PLAY_GAMES";
                case AuthProvider.GameCenter: return "GAME_CENTER";
                default: throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
            }
        }
    }
}
