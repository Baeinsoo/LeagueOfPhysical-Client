namespace LOP.UI
{
    /// <summary>내 등수를 화면 문구로 바꾼다. 등수는 서버가 정해 스냅샷으로 실려 온다.</summary>
    public class RaceFinishViewModel
    {
        private readonly IGameDataStore _gameDataStore;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;

        //  IPlayerContext가 아니라 IGameDataStore를 읽는다. "내 등수"는 정체 질문이라
        //  몸이 사라진 뒤에도 답이 있어야 하는데, playerContext는 몸이 없어지면 내려간다
        //  — 판이 끝나며 엔티티가 정리될 때 화면의 등수가 사라진다.
        public RaceFinishViewModel(IGameDataStore gameDataStore,
                                   GameFramework.World.EntityRegistry entityRegistry)
        {
            _gameDataStore = gameDataStore;
            _entityRegistry = entityRegistry;
        }

        /// <summary>
        /// 지금 띄울 등수 문구. 빈 문자열이면 자리만 비워 둔다 — 결승선을 넘은 것은 시뮬이 바로
        /// 알지만 등수는 서버 답이라 0.2초쯤 늦게 온다. 그동안 아무 숫자도 지어내지 않는다.
        /// </summary>
        public string PlacementText()
        {
            var entity = string.IsNullOrEmpty(_gameDataStore.userEntityId)
                ? null
                : _entityRegistry.Get(_gameDataStore.userEntityId);

            int placement = entity?.Get<FinishPlacement>()?.Value ?? 0;
            return placement > 0 ? $"{placement}등" : string.Empty;
        }
    }
}
