using GameFramework;

namespace LOP.UI
{
    /// <summary>
    /// 디버그/유틸 HUD ViewModel. tick·경과시간·RTT는 변경을 통지하는 이벤트 소스가 없는
    /// 샘플링 값이라 R3(push) 대신 평범한 getter로 노출하고, View가 매 프레임 pull한다.
    /// (이벤트가 있으면 R3, 없으면 폴링 — 아키텍처 가이드라인 "흐름의 경계" 결.)
    /// </summary>
    public class DebugHudViewModel
    {
        public bool IsRunning => GameEngine.current != null;

        public long Tick => GameEngine.Time.tick;

        public double ElapsedTime => GameEngine.Time.elapsedTime;

        public double RttMs => Mirror.NetworkTime.rtt * 1000;
    }
}
