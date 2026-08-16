namespace LOP.UI
{
    /// <summary>
    /// 로비에서 고를 수 있는 게임 하나. 입장에 필요한 게임과 맵이 짝지어져 있다 —
    /// 매칭 티켓은 둘을 함께 보내야 하고, 서버가 "이 맵이 이 게임 소속인지"를 검사한다.
    /// </summary>
    public readonly struct GameChoice
    {
        public readonly int GameModeId;
        public readonly int MapId;
        public readonly string Name;
        public readonly string Description;

        public GameChoice(int gameModeId, int mapId, string name, string description)
        {
            GameModeId = gameModeId;
            MapId = mapId;
            Name = name;
            Description = description;
        }
    }
}
