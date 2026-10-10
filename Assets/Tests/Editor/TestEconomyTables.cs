using System.IO;
using Luban;

namespace LOP.Tests
{
    /// <summary>
    /// 경제 관련 시험이 쓰는 실제 마스터데이터 표. <c>LOPMasterData.LoadAsync()</c>는 UnityWebRequest라
    /// EditMode에서 기다리기 안전하지 않아, 패키지가 배포하는 <c>.bytes</c>를 직접 읽는다
    /// (TestRankTables와 같은 방식).
    /// </summary>
    public static class TestEconomyTables
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        public static LOP.MasterData.TbCosmetic Cosmetics => new LOP.MasterData.TbCosmetic(Read("tbcosmetic"));

        public static LOP.MasterData.TbCosmeticSlot CosmeticSlots => new LOP.MasterData.TbCosmeticSlot(Read("tbcosmeticslot"));

        public static LOP.MasterData.TbCurrency Currencies => new LOP.MasterData.TbCurrency(Read("tbcurrency"));

        private static ByteBuf Read(string file) => new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes")));
    }
}
