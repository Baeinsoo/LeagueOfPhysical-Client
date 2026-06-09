using System;
using R3;

namespace LOP.UI
{
    /// <summary>로그인 팝업 ViewModel. 순수 C#. 선택된 LoginType을 OnLoginRequested로 1회 발행.</summary>
    public class LoginViewModel : IDisposable
    {
        private readonly Subject<LoginType> _loginRequested = new();

        public Observable<LoginType> OnLoginRequested => _loginRequested;

        public bool ShowGuest { get; }
        public bool ShowGpgs { get; }
        public bool ShowGameCenter { get; }

        public LoginViewModel()
        {
            ShowGuest = true;
            ShowGameCenter = false;
#if !UNITY_EDITOR && UNITY_ANDROID
            ShowGpgs = true;
#else
            ShowGpgs = false;
#endif
        }

        public void RequestLogin(LoginType loginType)
        {
            _loginRequested.OnNext(loginType);
        }

        public void Dispose()
        {
            _loginRequested.Dispose();
        }
    }
}
