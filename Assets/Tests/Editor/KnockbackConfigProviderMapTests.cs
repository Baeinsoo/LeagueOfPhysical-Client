using System.Collections.Generic;
using System.IO;
using Luban;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>넉백 설정은 맵 id의 행을 읽는다 - 기본 원판(15·16)과 드럼통 창고(17·18)는 드럼통만 다르다. 쌍둥이 시험이 서버 쪽에도 있다.</summary>
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
        public void 기본_원판_맵은_드럼통이_없다()
        {
            Assert.AreEqual(0, ConfigOf(15).Barrels.Count);
            Assert.AreEqual(0, ConfigOf(16, 12).Barrels.Count);
        }

        [Test]
        public void 드럼통_창고_맵은_드럼통_넷과_표의_값을_읽는다()
        {
            foreach (var (map, mode) in new[] { (17, 11), (18, 12) })
            {
                var b = ConfigOf(map, mode).Barrels;
                Assert.AreEqual(4, b.Count, $"맵 {map}");
                Assert.AreEqual(0.55f, b.Radius, 1e-5f);
                Assert.AreEqual(9f, b.RingRadius, 1e-5f);
                Assert.AreEqual(2.5f, b.Friction, 1e-5f);
                Assert.AreEqual(0.6f, b.Restitution, 1e-5f);
                Assert.AreEqual(6f, b.ShotImpulse, 1e-5f);
                Assert.AreEqual(1f, b.HitScale, 1e-5f);
            }
        }

        [Test]
        public void 두_맵의_나머지_값은_같다()
        {
            var plain = ConfigOf(15);
            var barrels = ConfigOf(17);
            Assert.AreEqual(plain.ArenaRadius, barrels.ArenaRadius);
            Assert.AreEqual(plain.ShotRange, barrels.ShotRange);
            Assert.AreEqual(plain.Pillars.Count, barrels.Pillars.Count);
            Assert.AreEqual(plain.BlastRadius, barrels.BlastRadius);
        }

        [Test]
        public void 설정_행이_없는_맵은_크게_실패한다()
        {
            var e = Assert.Throws<System.InvalidOperationException>(() => ConfigOf(999));
            StringAssert.Contains("999", e.Message);
        }
    }
}
