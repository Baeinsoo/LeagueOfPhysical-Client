using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>
    /// 이름 바꾸기 ViewModel. 서버에 개명을 요청하고 결과를 1회성으로 확정한다(확정 = 모달 닫기 신호).
    /// 성공하면 true, 사용자가 취소하면 false.
    /// </summary>
    public class ChangeDisplayNameViewModel : IDisposable
    {
        //  서버(DisplayNameService)와 같은 규칙이다. 여기서 먼저 막는 건 왕복을 아끼려는 것이고,
        //  진짜 게이트는 서버다 — 서버가 거절하면 클라가 뭘 통과시켰든 그 결과를 따른다.
        public const int MinLength = 2;
        public const int MaxLength = 12;

        private readonly IUserDataStore _userDataStore;
        private readonly UniTaskCompletionSource<bool> _result = new();
        private readonly CancellationTokenSource _cts = new();

        private bool _disposed;

        public UniTask<bool> ResultAsync => _result.Task;

        /// <summary>입력란의 시작값. 지금 이름을 그대로 보여준다(태그는 안 바뀌므로 뺀다).</summary>
        public string CurrentDisplayName => _userDataStore.user?.displayName ?? string.Empty;

        /// <summary>안내 문구. 비어 있으면 표시하지 않는다.</summary>
        public string Message { get; private set; } = string.Empty;

        public event Action MessageChanged;

        /// <summary>요청이 진행 중인지. View는 이때 버튼을 잠가 중복 요청을 막는다.</summary>
        public bool IsBusy { get; private set; }

        public event Action IsBusyChanged;

        public ChangeDisplayNameViewModel(IUserDataStore userDataStore)
        {
            _userDataStore = userDataStore;
        }

        public async void Submit(string raw)
        {
            if (IsBusy)
            {
                return;
            }

            string displayName = (raw ?? string.Empty).Trim();

            string localError = Validate(displayName);
            if (localError != null)
            {
                SetMessage(localError);
                return;
            }

            if (displayName == CurrentDisplayName)
            {
                //  같은 이름이면 요청 자체를 보내지 않는다. 서버는 이걸 성공으로 처리하므로
                //  보내도 되지만, 아무것도 안 바뀐 왕복이라 그냥 닫는다.
                _result.TrySetResult(false);
                return;
            }

            string userId = _userDataStore.user?.id;
            if (string.IsNullOrEmpty(userId))
            {
                SetMessage("로그인 정보를 확인할 수 없습니다.");
                return;
            }

            SetBusy(true);
            SetMessage(string.Empty);

            try
            {
                var response = await WebAPI.ChangeDisplayName(userId, displayName, _cts.Token);

                if (response.code == ResponseCode.SUCCESS)
                {
                    //  스토어 갱신은 여기서 하지 않는다 — 응답이 전역 발행되고 UserDataStore가
                    //  구독해서 채운다(GetUser와 같은 경로). 여기서 또 쓰면 진실이 둘이 된다.
                    _result.TrySetResult(true);
                    return;
                }

                //  실패해도 결과를 확정하지 않는다 — 확정하면 모달이 닫혀 다시 시도할 수 없다.
                SetMessage(response.code == ResponseCode.INVALID_DISPLAY_NAME
                    ? $"{MinLength}~{MaxLength}자, 공백 없이 입력해 주세요."
                    : "이름을 바꾸지 못했습니다. 다시 시도해 주세요.");
            }
            catch (OperationCanceledException)
            {
                //  모달이 먼저 닫힌 것뿐이다. 아래 프로퍼티를 건드리지 않는다.
            }
            catch (Exception exception)
            {
                SetMessage("이름을 바꾸지 못했습니다. 다시 시도해 주세요.");
                UnityEngine.Debug.LogError(exception);
            }
            finally
            {
                //  _cts는 Dispose에서 이미 dispose됐을 수 있어 읽지 않는다 — 평범한 플래그로 판단한다.
                if (_disposed == false)
                {
                    SetBusy(false);
                }
            }
        }

        public void Cancel() => _result.TrySetResult(false);

        /// <summary>통과하면 null, 아니면 사용자에게 보여줄 사유.</summary>
        public static string Validate(string trimmed)
        {
            //  글자 수는 UTF-16 단위가 아니라 코드포인트로 센다 — 이모지 하나는 사람 눈에 한 글자인데
            //  UTF-16으로는 둘이라, 안 세면 "12글자인데 왜 안 되냐"가 된다(서버도 같은 기준).
            int length = 0;
            for (int i = 0; i < trimmed.Length; i += char.IsHighSurrogate(trimmed[i]) ? 2 : 1)
            {
                length++;
            }

            if (length < MinLength || length > MaxLength)
            {
                return $"{MinLength}~{MaxLength}자로 입력해 주세요.";
            }

            foreach (char c in trimmed)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    return "공백은 쓸 수 없습니다.";
                }
            }

            return null;
        }

        private void SetMessage(string message)
        {
            Message = message;
            MessageChanged?.Invoke();
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            IsBusyChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            //  받아오는 도중에 모달이 사라질 수 있다. 먼저 끊는다.
            _cts.Cancel();
            _cts.Dispose();

            _result.TrySetResult(false);
        }
    }
}
