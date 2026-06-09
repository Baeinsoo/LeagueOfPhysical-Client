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
    }
}
