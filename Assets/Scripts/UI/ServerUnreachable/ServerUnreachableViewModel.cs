using System;
using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>"서버에 연결할 수 없습니다" 팝업 ViewModel. [다시 시도]로 서버에 다시 묻고, 닿으면 결과를 확정한다(= 팝업 닫힘).
    /// 자동 재시도는 하지 않는다 — 서버가 오래 꺼져 있을 때 요청을 계속 보내지 않게.</summary>
    public class ServerUnreachableViewModel
    {
        public const string FirstMessage = "서버에 연결할 수 없습니다.\n인터넷 연결을 확인하거나 잠시 후 다시 시도해 주세요.";
        public const string StillDownMessage = "아직 연결되지 않습니다. 잠시 후 다시 시도해 주세요.";

        private readonly ServerReachability reachability;
        private readonly UniTaskCompletionSource<bool> _result = new();

        public UniTask<bool> ResultAsync => _result.Task;
        public string Message { get; private set; } = FirstMessage;
        public bool IsBusy { get; private set; }
        public event Action Changed;

        public ServerUnreachableViewModel(ServerReachability reachability)
        {
            this.reachability = reachability;
        }

        public async void RequestRetry()
        {
            if (IsBusy)
            {
                return;
            }
            IsBusy = true;
            Changed?.Invoke();
            try
            {
                if (await reachability.IsReachableAsync())
                {
                    _result.TrySetResult(true);
                    return;
                }
                Message = StillDownMessage;
            }
            finally
            {
                IsBusy = false;
                Changed?.Invoke();
            }
        }
    }
}
