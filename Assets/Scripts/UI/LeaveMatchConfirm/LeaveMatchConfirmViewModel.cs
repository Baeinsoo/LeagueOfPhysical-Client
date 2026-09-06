using System;
using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>
    /// 나가기 확인 ViewModel. 결과를 1회성으로 확정한다(확정 = 모달 닫기 신호).
    /// 나가면 true, 취소하거나 백드롭으로 닫으면 false.
    /// </summary>
    public class LeaveMatchConfirmViewModel : IDisposable
    {
        private readonly UniTaskCompletionSource<bool> _result = new();

        private bool _disposed;

        public UniTask<bool> ResultAsync => _result.Task;

        public void Confirm() => _result.TrySetResult(true);

        public void Cancel() => _result.TrySetResult(false);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            //  백드롭으로 닫혔을 수도 있다. 아직 안 정해졌으면 취소로 끝낸다 —
            //  안 그러면 나가기를 누른 쪽이 영원히 기다린다.
            _result.TrySetResult(false);
        }
    }
}
