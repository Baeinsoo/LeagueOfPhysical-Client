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

        /// <summary>로그인 요청이 진행 중인지. 진행 중일 때 View는 버튼을 비활성화해 중복 클릭을 막는다.</summary>
        public bool IsBusy { get; private set; }

        public event Action IsBusyChanged;

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
            if (IsBusy)
            {
                //  이미 로그인 중이면 무시한다(큐잉·재시작 안 함) — 두 번째 클릭이 두 번째
                //  SignInAsync를 띄우면 서버 계정이 두 개 생기고, 나중 응답이 저장된 자격증명을
                //  덮어써서 실행 중 세션과 다음 실행 로그인 계정이 어긋나는 계정 유실로 이어진다.
                return;
            }

            SetBusy(true);
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
            finally
            {
                //  finally라 성공/실패/예외 어느 경로든 반드시 풀린다 — 여기서 안 풀리면 사용자가
                //  다시는 로그인을 시도할 수 없다.
                SetBusy(false);
            }
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            ErrorMessageChanged?.Invoke();
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            IsBusyChanged?.Invoke();
        }

        public void Dispose()
        {
            _result.TrySetCanceled();
        }
    }
}
