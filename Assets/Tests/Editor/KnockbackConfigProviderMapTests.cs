using System.Collections.Generic;
using System.IO;
using Luban;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>넉백 설정은 맵 id의 행을 읽는다 - 행이 없는 맵은 크게 실패한다. 쌍둥이 시험이 서버 쪽에도 있다.</summary>
    public class KnockbackConfigProviderMapTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        private sealed class FakeRoomDataStore : IRoomDataStore
        {
            public Room room { get; set; }
            public Match match { get; set; }

            public void Clear() { }
        }

        //  LoadAsync는 UnityWebRequest라 EditMode에서 못 기다린다 - 패키지의 .bytes를 직접 읽어 같은 표를 만든다.
        private static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        private static KnockbackConfig ConfigOf(int mapId, int gameModeId = 11)
        {
            var store = new FakeRoomDataStore
            {
                match = new Match { rounds = new[] { new MatchRound { index = 0, gameModeId = gameModeId, mapId = mapId } } },
            };
            return new KnockbackConfigProvider(RealMasterData(), store).Get();
        }

        [Test]
        public void 기본_원판_맵은_자기_id의_행을_읽는다()
        {
            Assert.AreEqual(4, ConfigOf(15).Pillars.Count);
            Assert.AreEqual(4, ConfigOf(16, 12).Pillars.Count);
        }

        [Test]
        public void 설정_행이_없는_맵은_크게_실패한다()
        {
            var e = Assert.Throws<System.InvalidOperationException>(() => ConfigOf(999));
            StringAssert.Contains("999", e.Message);
        }
    }
}
