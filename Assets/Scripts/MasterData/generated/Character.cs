using GameFramework;

namespace LOP.MasterData
{
    public sealed class Character : IMasterData
    {
        public string Code { get; private set; }
        public string Name { get; private set; }
        public float Speed { get; private set; }
        public float JumpPower { get; private set; }
        public string Description { get; private set; }
        public string DefaultSkinCode { get; private set; }
    }
}