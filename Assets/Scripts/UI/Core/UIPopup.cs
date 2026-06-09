namespace LOP.UI
{
    /// <summary>모달 팝업 베이스. Popup 밴드 + 백드롭. AutoClose면 백드롭 클릭 시 닫힘.</summary>
    public abstract class UIPopup : UIView
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool IsModal => true;
        public virtual bool AutoClose => true;
    }
}
