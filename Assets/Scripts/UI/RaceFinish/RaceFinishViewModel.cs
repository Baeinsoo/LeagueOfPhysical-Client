namespace LOP.UI
{
    /// <summary>내 등수를 화면 문구로 바꾼다. 등수는 서버가 정해 스냅샷으로 실려 온다.</summary>
    public class RaceFinishViewModel
    {
        private readonly IPlayerContext _playerContext;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;

        public RaceFinishViewModel(IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry)
        {
            _playerContext = playerContext;
            _entityRegistry = entityRegistry;
        }

        /// <summary>
        /// 지금 띄울 등수 문구. 빈 문자열이면 자리만 비워 둔다 — 결승선을 넘은 것은 시뮬이 바로
        /// 알지만 등수는 서버 답이라 0.2초쯤 늦게 온다. 그동안 아무 숫자도 지어내지 않는다.
        /// </summary>
        public string PlacementText()
        {
            var entity = string.IsNullOrEmpty(_playerContext.entityId)
                ? null
                : _entityRegistry.Get(_playerContext.entityId);

            int placement = entity?.Get<FinishPlacement>()?.Value ?? 0;
            return placement > 0 ? $"{placement}등" : string.Empty;
        }
    }
}
