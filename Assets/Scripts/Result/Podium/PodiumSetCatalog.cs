using System.Collections.Generic;

namespace LOP
{
    /// <summary>종목·맵 → 시상대 세트. (종목,맵) 일치 → (종목, 아무 맵) → 공용 순으로 찾는다.</summary>
    public sealed class PodiumSetCatalog
    {
        private readonly IPodiumSet _common;
        private readonly Dictionary<(int mode, int map), IPodiumSet> _byMap = new Dictionary<(int, int), IPodiumSet>();
        private readonly Dictionary<int, IPodiumSet> _byMode = new Dictionary<int, IPodiumSet>();

        public PodiumSetCatalog(IPodiumSet common) => _common = common;

        /// <summary>전용 세트는 여기 한 곳에 등록한다.</summary>
        public static PodiumSetCatalog CreateDefault()
        {
            var catalog = new PodiumSetCatalog(new CommonPodiumSet());
            return catalog;
        }

        public void Register(int gameModeId, int? mapId, IPodiumSet set)
        {
            if (mapId.HasValue) _byMap[(gameModeId, mapId.Value)] = set;
            else _byMode[gameModeId] = set;
        }

        public IPodiumSet Resolve(int gameModeId, int mapId)
        {
            if (_byMap.TryGetValue((gameModeId, mapId), out var exact)) return exact;
            if (_byMode.TryGetValue(gameModeId, out var mode)) return mode;
            return _common;
        }
    }
}
