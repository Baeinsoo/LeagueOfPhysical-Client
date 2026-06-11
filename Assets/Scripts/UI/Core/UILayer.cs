namespace LOP.UI
{
    /// <summary>UI 밴드. 열거 순서가 z-order(낮음→높음). 하위 밴드는 상위 밴드 위로 못 올라간다.</summary>
    public enum UILayer
    {
        Window = 0,        // 주 화면/페이지
        Popup = 1,         // 모달 다이얼로그 (백드롭)
        Loading = 2,       // 로딩/매칭 등 전체화면 전이 오버레이
        Notification = 3,  // 토스트/일시 알림
        System = 4,        // 시스템/치명/항상 최상단
    }
}
