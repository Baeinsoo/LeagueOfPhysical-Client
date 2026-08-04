using System;

namespace LOP
{
    [Serializable]
    public class CredentialDto
    {
        public string provider;
        public string providerUserId;
        public string secret;
    }

    [Serializable]
    public class AnonymousSignInResponse
    {
        public string userId;
        public CredentialDto credential;
        public string accessToken;
        public int expiresIn;
    }

    [Serializable]
    public class LoginRequest
    {
        public string provider;
        public string providerUserId;
        public string secret;
    }

    [Serializable]
    public class LoginResponse
    {
        public string userId;
        public string accessToken;
        public int expiresIn;
    }
}
