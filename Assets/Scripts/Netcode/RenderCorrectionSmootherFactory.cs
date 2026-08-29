namespace LOP
{
    /// <summary>
    /// 예측 엔티티마다 자기 렌더 보정 스무더를 하나씩 만들어 준다.
    ///
    /// 내 것과 남의 것을 다르게 만든다 — 언리얼이 스무딩을 simulated proxy에만 거는 것과 같은 이유다.
    /// 내가 조종하는 몸을 녹이면 그 시간 동안 입력과 화면이 어긋나 조작감이 무너진다. 남의 몸은
    /// 아무도 조종하지 않으니 그 대가가 없다.
    /// 상수 근거는 docs/superpowers/specs/2026-08-28-remote-render-smoothing-design.md §6.
    /// </summary>
    public class RenderCorrectionSmootherFactory
    {
        //  2.5cm는 눈에 안 보인다. 숨길 튐이 없는데 녹이면 그동안 계속 조금씩 틀린 자리에 있게 돼
        //  오히려 오차가 는다.
        private const float MinCorrection = 0.025f;

        //  언리얼 NetworkSimulatedSmoothLocationTime.
        private const float SmoothTime = 0.1f;

        //  언리얼 NetworkMaxSmoothUpdateDistance. 남의 새 정상 오차 최대가 4.788m(실측)이라
        //  그 위로 잡아 정상 구간에서는 목줄이 당겨지지 않게 한다.
        private const float MaxSmoothUpdateDistance = 5f;

        //  언리얼 NetworkNoSmoothUpdateDistance. 이 위로 벌어지는 것은 날갯짓으로 설명되지 않는다
        //  (리스폰·스폰 직후·큰 랙) — 녹이면 맵을 가로질러 미끄러지므로 즉시 간다.
        private const float NoSmoothUpdateDistance = 8f;

        //  0 = 스무딩 끔(언리얼 NetworkSmoothingMode.Disabled).
        private const float LocalSmoothTime = 0f;

        /// <param name="local">내가 조작하는 엔티티인가.</param>
        public GameFramework.Netcode.RenderCorrectionSmoother Create(bool local)
        {
            return new GameFramework.Netcode.RenderCorrectionSmoother(
                local ? LocalSmoothTime : SmoothTime,
                MinCorrection, MaxSmoothUpdateDistance, NoSmoothUpdateDistance);
        }
    }
}
