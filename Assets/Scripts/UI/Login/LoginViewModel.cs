using System;
using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>로그인 팝업 ViewModel. 사용자가 고른 방식으로 LoginService를 호출해 로그인을 수행하고,
    /// 결과(LoginResult)를 1회성으로 확정한다. 결과 확정이 곧 모달 닫기 신호.</summary>
    public class LoginViewModel : IDisposable
    {
        private readonly UniTaskCompletionSource<LoginResult> _result = new();

        /// <summary>로그인 결과. WindowManager(다이얼로그 서비스)가 await한다.</summary>
        public UniTask<LoginResult> ResultAsync => _result.Task;

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

        /// <summary>사용자가 로그인 방식을 선택. 서비스 레이어를 호출해 로그인 후 결과를 확정한다.</summary>
        public void RequestLogin(LoginType loginType)
        {
            LoginResult result = LoginService.instance.Login(loginType);
            _result.TrySetResult(result);
        }

        public void Dispose()
        {
            _result.TrySetCanceled();
        }
    }
}
