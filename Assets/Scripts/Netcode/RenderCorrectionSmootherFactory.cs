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
        //  (Flappy에서 0.35초까지 늘려 봤다가 되돌렸다 — 그때 남의 새 보정이 5m였는데, 짧게 녹이면
        //  순간이동으로 보이고 길게 녹이면 그 틀린 자리를 오래 그려 벽을 통과해 보였다. 스무딩은
        //  튐을 없애는 게 아니라 거짓말을 길게 늘일 뿐이라 큰 오차엔 답이 못 된다.)
        //
        //  Skydive에서 0.2초로 올린다. 위 Flappy 사례("길게 녹이면 틀린 자리를 오래 그린다")와
        //  조건이 다르기 때문이다 — 거긴 보정이 5m였고 여기선 실측 0.1~0.7m다. 오차가 크면
        //  늘이는 게 거짓말을 늘이는 것이지만, 이만큼 작으면 그게 바로 녹여야 할 몫이다.
        //  늘리는 이유: 남의 입력이 <b>한 번도 제때 도착하지 않아</b>(실측 0/100) 보정이 몰려 온다
        //  (2초 창에 최대 30회). 0.1초로는 앞 보정을 다 녹이기 전에 다음이 와서 이음매가 계속
        //  새로 시작된다. 이 값이 걸리는 곳은 남을 예측하는 게임(Skydive)뿐이다.
        private const float SmoothTime = 0.2f;

        //  언리얼 NetworkMaxSmoothUpdateDistance. 녹이는 내내 "화면이 시뮬에서 이보다 멀어지지
        //  않게" 잡아당기는 목줄이다 — 아래 NoSmooth(한 번의 판단)와 다른 물건이다.
        //  Flappy에서 잰 남의 새 정상 오차 최대 4.788m 위로 잡아, 정상 구간에선 안 당겨지게 했다.
        //  Flappy는 그 뒤 남을 예측하지 않게 됐으므로(스냅샷 보간) 지금 이 값이 걸리는 곳은 남을
        //  예측하는 게임(Skydive)뿐이다.
        //  Skydive에서 실측(RemoteSyncProbe): 예측 오차 중앙 0.00m·p95 0.02m·최대 0.05m,
        //  되감기가 화면을 옮긴 거리 0.1~0.7m. 5m 근처에도 가지 않아 목줄이 당겨지는 일이 없다 —
        //  그래서 값을 그대로 둔다. 초속 45m 게임이라 커야 할 것 같지만, 재 보니 그렇지 않았다.
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
