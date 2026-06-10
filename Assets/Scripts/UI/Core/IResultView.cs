using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>결과를 반환하는 모달 View 계약. 결과 출처는 ViewModel이며, View는 이를 포워딩한다.
    /// WindowManager.OpenModalAsync가 ResultAsync를 await해 소비자에게 결과만 돌려준다.</summary>
    public interface IResultView<TResult>
    {
        UniTask<TResult> ResultAsync { get; }
    }
}
