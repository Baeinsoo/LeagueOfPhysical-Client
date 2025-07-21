using GameFramework;

namespace LOP.MasterData
{
    public sealed class Item : IMasterData
    {
        public string Code { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string SkinCode { get; private set; }
    }
}