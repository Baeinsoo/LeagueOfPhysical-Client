using System;
using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>로그인 팝업 ViewModel. 고른 방식으로 AuthenticationService를 호출해 세션을 확보하고,
    /// 결과(AuthSession)를 1회성으로 확정한다. 결과 확정이 곧 모달 닫기 신호.</summary>
    public class LoginViewModel : IDisposable
    {
        private readonly AuthenticationService authenticationService;
        private readonly UniTaskCompletionSource<AuthSession> _result = new();

        public UniTask<AuthSession> ResultAsync => _result.Task;

        public bool ShowGuest { get; }
        public bool ShowGpgs { get; }
        public bool ShowGameCenter { get; }

        /// <summary>로그인 실패 문구. 비어 있으면 표시하지 않는다.</summary>
        public string ErrorMessage { get; private set; } = string.Empty;

        public event Action ErrorMessageChanged;

        public LoginViewModel(AuthenticationService authenticationService)
        {
            this.authenticationService = authenticationService;

            ShowGuest = true;
            ShowGameCenter = false;
#if !UNITY_EDITOR && UNITY_ANDROID
            ShowGpgs = true;
#else
            ShowGpgs = false;
#endif
        }

        public async void RequestLogin(AuthProvider provider)
        {
            SetError(string.Empty);

            try
            {
                AuthSession session = await authenticationService.SignInAsync(provider);
                _result.TrySetResult(session);
            }
            catch (NotSupportedException)
            {
                SetError("준비 중입니다.");
            }
            catch (Exception exception)
            {
                //  실패해도 결과를 확정하지 않는다 — 확정하면 모달이 닫히고 사용자가 다시 시도할 수 없다.
                SetError("로그인에 실패했습니다. 다시 시도해 주세요.");
                UnityEngine.Debug.LogError(exception);
            }
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            ErrorMessageChanged?.Invoke();
        }

        public void Dispose()
        {
            _result.TrySetCanceled();
        }
    }
}
