using GameFramework;

namespace LOP.MasterData
{
    public sealed class Action : IMasterData
    {
        public string Code { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Class { get; private set; }
        public float Duration { get; private set; }
        public float CastTime { get; private set; }
        public float Cooldown { get; private set; }
        public float HpCost { get; private set; }
        public float MpCost { get; private set; }
    }
}