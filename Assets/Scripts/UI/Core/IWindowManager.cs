using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    public interface IWindowManager
    {
        /// <summary>T를 UI 스코프에서 resolve(ViewModel 자동 주입) → T.Layer 밴드 스택에 push. 모달이면 백드롭.</summary>
        T Open<T>() where T : UIView;

        /// <summary>닫고 dispose. 밴드 스택 pop + (모달이면) 백드롭 갱신.</summary>
        void Close(UIView view);

        /// <summary>가장 높은 가시 밴드의 top을 닫는다(back/ESC). 닫았으면 true.</summary>
        bool Back();

        /// <summary>모달 View를 열고 ViewModel이 만든 결과를 await로 반환. 결과 확정 후 자동 Close.
        /// 소비자는 View/ViewModel을 만지지 않고 결과만 받는다(다이얼로그 서비스 패턴).</summary>
        UniTask<TResult> OpenModalAsync<TView, TResult>() where TView : UIView, IResultView<TResult>;
    }
}
