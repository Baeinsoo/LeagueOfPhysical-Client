namespace LOP.UI
{
    /// <summary>로딩 화면 오버레이. 정적 텍스트, 전체화면 입력 차단(화면 자체가 가림 — 별도 백드롭 없음).</summary>
    public class GameLoadingView : UIView
    {
        public override UILayer Layer => UILayer.Loading;
        public override bool BlocksUnderlyingInput => true;
    }
}
