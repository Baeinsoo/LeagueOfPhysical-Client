using System;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 정보 HUD 한 장을 월드에서 채운다.
    /// <para>x는 <b>그려진 자리</b>(<see cref="LOPActor.visualGameObject"/>)에서 읽는다 — 남의 새는 보간이라 시뮬 값과 0.23초쯤
    /// 어긋나는데, 막대·순위는 화면과 같은 말을 해야 한다(spec §1). 그림이 아직 안 붙었으면 시뮬 x로 메운다.</para>
    /// <para>"나"는 <see cref="IGameDataStore.userEntityId"/>로 본다(IPlayerContext가 아니라) — 몸이 사라져도 정체는 남는다
    /// (<see cref="RaceFinishViewModel"/>과 같은 이유).</para>
    /// </summary>
    public class RaceHudSource : IRaceHudSource
    {
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly ActorRegistry _actorRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly MatchStartState _matchStart;
        private readonly GameFramework.Runner.IRunner _runner;
        private readonly FinishLineBounds _finishLine;

        //  테스트 seam — 실제로는 맵 씬의 SpawnPoint 중 가장 뒤 x(서버가 새를 세우는 자리라 출발선이다).
        internal Func<float?> spawnStartX = FindSpawnStartX;

        //  한 번 정하면 판 내내 그대로 둔다 — 새가 앞으로 가도 출발 x가 따라오면 막대가 줄어든다.
        private float? _startX;

        public RaceHudSource(GameFramework.World.EntityRegistry entityRegistry,
                             ActorRegistry actorRegistry,
                             IGameDataStore gameDataStore,
                             MatchStartState matchStart,
                             GameFramework.Runner.IRunner runner,
                             FinishLineBounds finishLine)
        {
            _entityRegistry = entityRegistry;
            _actorRegistry = actorRegistry;
            _gameDataStore = gameDataStore;
            _matchStart = matchStart;
            _runner = runner;
            _finishLine = finishLine;
        }

        public void Fill(RaceHudFrame frame)
        {
            frame.MyId = _gameDataStore.userEntityId;
            frame.StartTick = _matchStart.StartTick.CurrentValue;
            //  스코프가 러너 초기화보다 먼저 화면을 열 수 있다 — 그 사이엔 tickUpdater가 없다.
            var ticker = _runner.tickUpdater;
            frame.NowSeconds = ticker?.elapsedTime ?? 0d;
            frame.SecondsPerTick = ticker?.interval ?? 0d;

            float rearmost = float.PositiveInfinity;
            foreach (var entity in _entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var body = entity.Get<GameFramework.World.Transform>();
                if (body == null)
                {
                    continue;
                }

                var visual = _actorRegistry.Get(entity.Id)?.visualGameObject;
                float x = visual != null ? visual.transform.position.x : body.Position.X;
                int placement = entity.Get<FinishPlacement>()?.Value ?? 0;
                frame.Birds.Add(new RaceBirdSample(entity.Id, placement, x));
                rearmost = Mathf.Min(rearmost, x);

                if (entity.Id == frame.MyId)
                {
                    //  클라 시뮬은 내 새만 굴리므로 FinishState는 내 것만 믿을 수 있다(FlappySpectate.Finished 주석).
                    frame.MyFinishedTick = entity.Get<FinishState>()?.FinishedTick ?? FinishState.NotFinished;
                }
            }

            if (_startX == null)
            {
                //  SpawnPoint가 없는 맵이면 처음 본 새들 중 가장 뒤를 출발로 삼는다(HUD는 내 새가 생길 때 열리므로
                //  첫 장엔 새가 있다 — 모두 출발선에 서 있을 때다).
                _startX = spawnStartX() ?? (frame.Birds.Count > 0 ? rearmost : (float?)null);
            }

            bool hasFinish = _finishLine.TryGet(out var bounds);
            frame.StartX = _startX ?? 0f;
            //  결승 판정이 앞면(min.x)에 닿는 순간이라 막대 끝도 앞면이다(FlappyChaserView의 정지선과 같은 값).
            frame.FinishX = hasFinish ? bounds.min.x : 0f;
            frame.HasCourse = _startX != null && hasFinish && frame.FinishX > frame.StartX;
        }

        private static float? FindSpawnStartX()
        {
            var points = UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (points.Length == 0)
            {
                return null;
            }
            float min = float.PositiveInfinity;
            foreach (var point in points)
            {
                min = Mathf.Min(min, point.transform.position.x);
            }
            return min;
        }
    }
}
