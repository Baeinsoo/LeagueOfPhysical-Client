using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>
    /// 마스터데이터에서 "지금 실제로 입장할 수 있는 게임" 목록을 뽑는 side-local 어댑터.
    ///
    /// 두 조건을 모두 만족해야 목록에 넣는다. 둘 다 없으면 골라도 못 들어가기 때문이다:
    ///   - 게임 씬 경로가 있어야 한다. 비어 있으면 로더가 그대로 예외를 던진다(MatchSceneResolver).
    ///   - 그 게임에 속한 맵이 하나는 있어야 한다. 없으면 티켓에 실을 mapId가 없다.
    /// 그래서 아직 만들지 않은 게임(씬 경로가 빈 행)은 저절로 빠진다.
    /// </summary>
    public class PlayableGameProvider
    {
        private readonly List<GameChoice> _games = new List<GameChoice>();

        public PlayableGameProvider(LOP.MasterData.LOPMasterData md)
        {
            //  게임별 기본 맵 = id가 가장 작은 맵. 지금은 게임당 맵이 하나뿐이라 사실상 그 맵이다.
            var defaultMaps = new Dictionary<int, LOP.MasterData.GameMap>();
            foreach (var map in md.Tables.TbMap.DataList)
            {
                if (defaultMaps.TryGetValue(map.GameModeId, out var current) == false || map.Id < current.Id)
                {
                    defaultMaps[map.GameModeId] = map;
                }
            }

            foreach (var gameMode in md.Tables.TbGameMode.DataList)
            {
                if (string.IsNullOrEmpty(gameMode.ScenePath))
                {
                    continue;
                }

                if (defaultMaps.TryGetValue(gameMode.Id, out var map) == false)
                {
                    continue;
                }

                _games.Add(new GameChoice(gameMode.Id, map.Id, gameMode.Name, gameMode.Description));
            }
        }

        /// <summary>입장 가능한 게임 목록. 마스터데이터는 런타임에 변하지 않으므로 한 번 만들어 둔다.</summary>
        public IReadOnlyList<GameChoice> Games => _games;
    }
}
