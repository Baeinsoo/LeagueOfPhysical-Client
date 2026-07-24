using System;
using R3;

namespace LOP.UI
{
    /// <summary>
    /// 로비 홈 허브의 ViewModel. 네비게이션 "요청"을 도메인 신호로만 노출한다(화면 교체는 하지 않음).
    /// 신호를 듣고 실제 윈도우를 여는 것은 FrontEndCoordinator의 책임(아키텍처: VM=신호 / 코디네이터=네비게이션).
    /// 일회성 이벤트라 ReactiveProperty가 아니라 Subject/Observable을 쓴다.
    /// </summary>
    public class LobbyHomeViewModel : IDisposable
    {
        private readonly Subject<FrontEndDestination> _navigationRequested = new();

        /// <summary>네비 버튼이 눌렸을 때의 목적지 신호. FrontEndCoordinator가 구독한다.</summary>
        public Observable<FrontEndDestination> NavigationRequested => _navigationRequested;

        /// <summary>네비 커맨드(View가 버튼 클릭 시 호출).</summary>
        public void Navigate(FrontEndDestination destination) => _navigationRequested.OnNext(destination);

        public void Dispose()
        {
            _navigationRequested.Dispose();
        }
    }
}
