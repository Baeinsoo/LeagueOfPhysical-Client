namespace LOP.UI
{
    /// <summary>UI 레이어. 열거 순서가 z-order(낮음→높음).</summary>
    public enum UILayer
    {
        Hud = 0,     // (M3) 인게임 월드/오버레이
        Popup = 1,   // 모달 팝업 스택
        Loading = 2, // (M2) 로딩
        Toast = 3,   // (M2) 토스트
        System = 4,  // (M2) 시스템
    }
}
