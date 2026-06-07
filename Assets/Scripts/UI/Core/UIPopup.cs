namespace LOP.UI
{
    /// <summary>모달 팝업 베이스. 백드롭은 UIManager가 제공. AutoClose면 백드롭 클릭/스택 정리 시 닫힘.</summary>
    public abstract class UIPopup : UIView
    {
        public virtual bool AutoClose => true;
    }
}
