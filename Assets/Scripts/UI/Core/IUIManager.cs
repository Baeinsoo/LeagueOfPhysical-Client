namespace LOP.UI
{
    public interface IUIManager
    {
        /// <summary>이미 DI로 생성된 view를 해당 레이어에 열고, 모달이면 스택 push + 백드롭 표시.</summary>
        void Open(UIView view, UILayer layer);

        /// <summary>view를 닫고 dispose. 모달이면 스택 pop + 백드롭 갱신.</summary>
        void Close(UIView view);

        /// <summary>모달 스택 최상단을 닫는다(back/ESC). 닫았으면 true.</summary>
        bool CloseTop();
    }
}
