using System.IO;
using Luban;

namespace LOP.Tests
{
    /// <summary>
    /// 랭크 시험이 쓰는 실제 마스터데이터 표. <c>LOPMasterData.LoadAsync()</c>는 UnityWebRequest라 EditMode에서
    /// 기다리기 안전하지 않아, 패키지가 배포하는 <c>.bytes</c>를 직접 읽는다(SkydiveLandingMasterDataConsistencyTests와 같은 방식).
    /// </summary>
    public static class TestRankTables
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        public static LOP.MasterData.TbRankDivision Divisions => new LOP.MasterData.TbRankDivision(Read("tbrankdivision"));

        public static LOP.MasterData.TbQueue Queues => new LOP.MasterData.TbQueue(Read("tbqueue"));

        private static ByteBuf Read(string file) => new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes")));
    }
}
