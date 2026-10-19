using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>재접속 실패 팝업. [다시 시도]를 누르면 닫히고, 재접속 진행자가 처음부터 다시 묻는다(실제 확인은 그쪽이 한다).</summary>
    public class ReconnectFailedViewModel
    {
        public const string Message = "경기 서버에 다시 연결할 수 없습니다.\n인터넷 연결을 확인하고 다시 시도해 주세요.";

        private readonly UniTaskCompletionSource<bool> _result = new();

        public UniTask<bool> ResultAsync => _result.Task;

        public void RequestRetry() => _result.TrySetResult(true);
    }
}
