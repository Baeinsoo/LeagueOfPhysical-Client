namespace LOP.UI
{
    /// <summary>
    /// 추격자에게 잡혔음을 알린다. 바뀌는 값이 없어 ViewModel이 없다 — 라벨은 UXML에 박혀 있고
    /// 이 클래스는 밴드와 입력 규칙만 정한다.
    ///
    /// <para>Notification이 아니라 Window인 이유는 <see cref="RaceStartView"/>와 같다:
    /// 이건 토스트가 아니라 게임 화면이라 로딩·결과 같은 전체화면 오버레이에 <b>가려져야</b> 한다.</para>
    /// </summary>
    public class RaceEliminatedView : UIView
    {
        public override UILayer Layer => UILayer.Window;
    }
}
