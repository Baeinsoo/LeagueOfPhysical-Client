namespace LOP
{
    /// <summary>
    /// 예측 엔티티마다 자기 렌더 보정 스무더를 하나씩 만들어 준다(튄 양이 엔티티마다 다르다).
    /// 내 것과 남의 것은 "보정이 이만큼 나는 게 정상인가"의 기준 자체가 달라, 텔레포트 컷오프를
    /// 따로 준다 — 그 근거는 아래 상수 옆에 적어 둔다.
    /// </summary>
    public class RenderCorrectionSmootherFactory
    {
        private const float Tau = 0.1f;              // 보정 오프셋이 녹는 시간상수(초)
        private const float MinCorrection = 0.025f;  // 2.5cm 미만은 녹일 것도 없다 — 그냥 새 위치를 따른다

        //  내 새 — 이보다 큰 보정은 녹이지 않고 그 자리에 붙인다. 내가 직접 굴리는 몸이 3m나
        //  틀렸다면 리스폰·순간이동처럼 "미끄러져 가는 쪽이 오히려 이상한" 사건이다.
        private const float LocalTeleport = 3f;

        //  남의 새 — 컷오프를 끈다(어떤 크기든 녹인다).
        //  남의 입력은 클라에 오지 않아 "안 눌렀다"로 가정하고 굴리는데, 클라는 서버보다 ~9틱
        //  (0.02초 × 9 = 0.18초) 앞서 달린다. 그 사이 남이 한 번이라도 날갯짓하면 세로 속도가
        //  +23으로 튀므로, 가정과의 차이는 (23 − 지금속도) × 0.18초가 된다:
        //    · 속도 0에서 날갯짓  → (23 − 0)   × 0.18 ≈ 4.1m
        //    · 최대 낙하속도에서  → (23 − −30) × 0.18 ≈ 9.5m
        //  즉 남의 날갯짓은 매번 3m를 훌쩍 넘긴다 — 컷오프를 두면 날갯짓마다 순간이동으로 보인다
        //  (이전에 같은 전환을 되돌리게 만든 바로 그 증상).
        //  값의 근거는 직전까지 이 새들을 그리던 ExtrapolatedEntityInterpolator다 — 크기 제한 없이
        //  0.1초에 걸쳐 무조건 섞었고(BlendDuration), 그 화면을 순간이동이라고 한 사람은 없었다.
        //  그래서 새 숫자를 지어내지 않고 그 동작(=컷오프 없음 + 같은 시간상수)에 맞춘다.
        private const float RemoteTeleport = float.PositiveInfinity;

        /// <param name="local">내가 조작하는 엔티티인가.</param>
        public GameFramework.Netcode.RenderCorrectionSmoother Create(bool local)
        {
            return new GameFramework.Netcode.RenderCorrectionSmoother(
                Tau, MinCorrection, local ? LocalTeleport : RemoteTeleport);
        }
    }
}
