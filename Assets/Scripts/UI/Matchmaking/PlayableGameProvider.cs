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
            var mapsByGameMode = new Dictionary<int, List<MapChoice>>();
            foreach (var map in md.Tables.TbMap.DataList)
            {
                if (mapsByGameMode.TryGetValue(map.GameModeId, out var maps) == false)
                {
                    maps = new List<MapChoice>();
                    mapsByGameMode[map.GameModeId] = maps;
                }

                maps.Add(new MapChoice(map.Id, map.Name));
            }

            //  화면에 뜨는 순서가 실행 때마다 달라지지 않도록 id로 정렬해 둔다.
            foreach (var maps in mapsByGameMode.Values)
            {
                maps.Sort((left, right) => left.MapId.CompareTo(right.MapId));
            }

            foreach (var gameMode in md.Tables.TbGameMode.DataList)
            {
                if (string.IsNullOrEmpty(gameMode.ScenePath))
                {
                    continue;
                }

                if (mapsByGameMode.TryGetValue(gameMode.Id, out var maps) == false)
                {
                    continue;
                }

                _games.Add(new GameChoice(gameMode.Id, gameMode.Name, gameMode.Description, maps));
            }
        }

        /// <summary>입장 가능한 게임 목록. 마스터데이터는 런타임에 변하지 않으므로 한 번 만들어 둔다.</summary>
        public IReadOnlyList<GameChoice> Games => _games;
    }
}
