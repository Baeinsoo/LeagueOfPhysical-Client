using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>로비에서 고를 수 있는 맵 하나.</summary>
    public readonly struct MapChoice
    {
        public readonly int MapId;
        public readonly string Name;

        public MapChoice(int mapId, string name)
        {
            MapId = mapId;
            Name = name;
        }
    }

    /// <summary>
    /// 로비에서 고를 수 있는 게임 하나와 그 게임의 맵들. 입장에 필요한 둘이 함께 있어야 하는 이유는
    /// 매칭 티켓이 게임과 맵을 짝으로 보내고, 서버가 "이 맵이 이 게임 소속인지"를 검사하기 때문이다.
    /// </summary>
    public readonly struct GameChoice
    {
        public readonly int GameModeId;
        public readonly string Name;
        public readonly string Description;

        /// <summary>이 게임의 맵. id 오름차순이며 최소 한 개다(없는 게임은 목록에 들어오지 않는다).</summary>
        public readonly IReadOnlyList<MapChoice> Maps;

        public GameChoice(int gameModeId, string name, string description, IReadOnlyList<MapChoice> maps)
        {
            GameModeId = gameModeId;
            Name = name;
            Description = description;
            Maps = maps;
        }
    }
}
