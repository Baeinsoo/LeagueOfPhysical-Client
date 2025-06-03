using GameFramework;

namespace LOP.MasterData
{
    public sealed class Action : IMasterData
    {
        public string code { get; private set; }
        public string name { get; private set; }
        public string description { get; private set; }
        public string @class { get; private set; }
        public float duration { get; private set; }
        public float cast_time { get; private set; }
        public float cooldown { get; private set; }
        public float hp_cost { get; private set; }
        public float mp_cost { get; private set; }
    }
}
