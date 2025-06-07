using GameFramework;

namespace LOP.MasterData
{
    public sealed class SkinAsset : IMasterData
    {
        public string Code { get; private set; }
        public string SkinCode { get; private set; }
        public string ModelPath { get; private set; }
    }
}