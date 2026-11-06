using LOP.UI;

namespace LOP
{
    /// <summary>
    /// 카드 러시 정보 HUD 한 장을 월드에서 채운다(spec §7.3). <see cref="RaceHudViewModel"/>/<see cref="RaceHudView"/>는
    /// Flappy와 그대로 쓰고 소스만 이것으로 갈아 끼운다.
    ///
    /// <para>진행은 출발선에서 간 거리(mm)다(<c>StartX = 0</c>, <c>FinishX = TrackMm</c>, spec 2026-11-05 §10) — 레인이 씬마다
    /// 다른 방향을 보므로 Flappy의 <see cref="RaceHudSource"/>처럼 그려진 x(월드 좌표)를 그대로 쓸 수 없다. 상태의
    /// <see cref="CardRushRunnerState.PosMm"/>가 곧 진행이다(남의 몸은 최신 스냅 값 — 막대 점 하나라 보간 지연만큼의 차는 안 보인다).</para>
    /// </summary>
    public class CardRushRaceHudSource : IRaceHudSource
    {
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly MatchStartState _matchStart;
        private readonly GameFramework.Runner.IRunner _runner;
        private readonly CardRushWorld _world;

        public CardRushRaceHudSource(GameFramework.World.EntityRegistry entityRegistry,
                                     IGameDataStore gameDataStore,
                                     MatchStartState matchStart,
                                     GameFramework.Runner.IRunner runner,
                                     CardRushWorld world)
        {
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
            _matchStart = matchStart;
            _runner = runner;
            _world = world;
        }

        public void Fill(RaceHudFrame frame)
        {
            frame.MyId = _gameDataStore.userEntityId;
            frame.StartTick = _matchStart.StartTick.CurrentValue;
            //  스코프가 러너 초기화보다 먼저 화면을 열 수 있다 — 그 사이엔 tickUpdater가 없다(RaceHudSource와 같은 가드).
            var ticker = _runner.tickUpdater;
            frame.NowSeconds = ticker?.elapsedTime ?? 0d;
            frame.SecondsPerTick = ticker?.interval ?? 0d;

            int trackMm = _world.Track?.TrackMm ?? 0;

            foreach (var entity in _entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var runner = entity.Get<CardRushRunner>();
                if (runner == null)
                {
                    continue;
                }

                bool mine = entity.Id == frame.MyId;
                float x = runner.State.PosMm;
                int placement = entity.Get<FinishPlacement>()?.Value ?? 0;
                frame.Birds.Add(new RaceBirdSample(entity.Id, placement, x));

                if (mine)
                {
                    //  클라 시뮬은 내 몸만 굴리므로 FinishState는 내 것만 믿을 수 있다(FlappySpectate 주석과 같은 이유).
                    frame.MyFinishedTick = entity.Get<FinishState>()?.FinishedTick ?? FinishState.NotFinished;
                }
            }

            frame.StartX = 0f;
            frame.FinishX = trackMm;
            frame.HasCourse = trackMm > 0;
            //  카드 러시엔 Flappy식 "n막" 구간 표식이 없다(§7.3) — frame.Sections는 빈 채로 둔다.
            //  RaceHudViewModel은 빈 목록이면 띠·배너를 그리지 않는다(연출 절제와도 맞는다).
        }
    }
}
