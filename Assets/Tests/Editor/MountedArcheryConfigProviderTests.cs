using System.IO;
using Luban;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>클라 제공자가 표의 만작 시간을 설정에 옮기는지 본다 — 빠뜨리면 서버(0.35)와 클라 예측(0.5)이 갈린다.</summary>
    public class MountedArcheryConfigProviderTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        private static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        [Test]
        public void 표에서_만작_시간을_읽는다()
        {
            var cfg = new MountedArcheryConfigProvider(RealMasterData()).Get();

            Assert.AreEqual(0.35f, cfg.FullDrawSeconds, 1e-5f);
        }
    }
}
