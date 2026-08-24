namespace LOP
{
    /// <summary>
    /// 클라가 이 엔티티를 어떻게 따라갈지. 게임마다 답이 다르므로 게임 스코프가 정책으로 고른다.
    /// (Unity Netcode for Entities의 <c>GhostMode</c>에 대응한다.)
    /// </summary>
    public enum EntitySyncMode
    {
        /// <summary>서버 스냅 두 개 사이를 지연된 시간에서 보간한다. 예측 없음.</summary>
        Interpolated,

        /// <summary>내 시간선에서 같이 굴린다. 스냅이 오면 그 틱으로 맞추고 지금까지 다시 굴린다.</summary>
        Predicted,

        /// <summary>
        /// 굴리지는 않고, 마지막 스냅에서 내 시각까지 이어 그린다. 남의 입력을 모르는 채로 굴리면
        /// 틀린 궤적을 확신 있게 그려 보정이 크게 튀는데, 외삽은 틀려도 방향이 대체로 맞아 오차가 작다.
        /// </summary>
        Extrapolated,
    }
}
