using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luban;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 제공자가 실제 표(맵 13 투석기 사냥)를 투석기 설정으로 옮기는지 본다. <see cref="ArcheryCatapultSettings"/>
    /// 생성자는 정수 칸 여덟 개를 자리로 받아서, 두 칸을 바꿔 넘겨도 컴파일이 되고 아무 시험도 안 깨진다 —
    /// 판 길이·무리 간격이 조용히 뒤바뀐다. 서버 쌍둥이 제공자에도 같은 시험이 있다.
    /// </summary>
    public class ArcheryConfigProviderCatapultTests
    {
        private const int CatapultMapId = 13;
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        private sealed class FakeRoomDataStore : IRoomDataStore
        {
            public Room room { get; set; }
            public Match match { get; set; }
            public void Clear() { }
        }

        //  LoadAsync는 UnityWebRequest라 EditMode에서 못 기다린다 — 패키지의 .bytes를 직접 읽어 같은 표를 만든다.
        private static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        private static ArcheryConfig CatapultConfig(LOP.MasterData.LOPMasterData md)
        {
            var store = new FakeRoomDataStore
            {
                match = new Match { rounds = new[] { new MatchRound { index = 0, gameModeId = 5, mapId = CatapultMapId } } },
            };
            return new ArcheryConfigProvider(md, store).Get();
        }

        [Test]
        public void 투석기_설정은_표의_칸을_제자리에_옮긴다()
        {
            var md = RealMasterData();
            var row = md.Tables.TbArcheryConfig.GetOrDefault(CatapultMapId);
            Assert.IsNotNull(row, "TbArcheryConfig에 맵 13 행이 없다");

            //  정수 칸 값이 서로 다를 때만 순서 바꿈이 잡힌다 — 같아지면 이 시험이 아무것도 못 지킨다.
            var ints = new[]
            {
                row.RoundCount, row.RoundIntroTicks, row.RoundHuntTicks, row.RoundResultTicks,
                row.VolleyPeriodTicks, row.VolleyCueTicks, row.VolleyQuietTicks, row.TargetLifeTicks,
            };
            Assert.AreEqual(ints.Length, ints.Distinct().Count(), "맵 13의 투석기 정수 칸에 같은 값이 있어 순서 바꿈을 못 잡는다");

            var config = CatapultConfig(md);
            Assert.AreEqual(ArcheryCourseKind.Catapult, config.CourseKind);
            var s = config.Catapult;
            Assert.AreEqual(row.RoundCount, s.RoundCount, nameof(s.RoundCount));
            Assert.AreEqual(row.RoundIntroTicks, s.IntroTicks, nameof(s.IntroTicks));
            Assert.AreEqual(row.RoundHuntTicks, s.HuntTicks, nameof(s.HuntTicks));
            Assert.AreEqual(row.RoundResultTicks, s.ResultTicks, nameof(s.ResultTicks));
            Assert.AreEqual(row.VolleyPeriodTicks, s.VolleyPeriodTicks, nameof(s.VolleyPeriodTicks));
            Assert.AreEqual(row.VolleyCueTicks, s.CueTicks, nameof(s.CueTicks));
            Assert.AreEqual(row.VolleyQuietTicks, s.QuietTicks, nameof(s.QuietTicks));
            Assert.AreEqual(row.TargetLifeTicks, s.TargetLifeTicks, nameof(s.TargetLifeTicks));
            Assert.AreEqual(row.TrapChance, s.TrapChance, nameof(s.TrapChance));
            Assert.AreEqual(row.SpawnRadius, s.LateralHalfRangeM, nameof(s.LateralHalfRangeM));
        }

        [Test]
        public void 투석기_표적은_거리대가_적힌_종류만이다()
        {
            var md = RealMasterData();
            var all = md.Tables.TbArcheryTarget.DataList;
            var banded = all.Where(x => x.DistanceMaxM > 0f).Select(x => x.Id).OrderBy(x => x).ToArray();
            //  거리대 없는 종류(한 발 승부 판)가 표에 있어야 거르기를 시험하는 셈이다.
            Assert.IsTrue(all.Any(x => x.DistanceMaxM <= 0f), "거리대 없는 종류가 표에 없어 거르기를 못 본다");
            Assert.IsNotEmpty(banded);

            var config = CatapultConfig(md);
            CollectionAssert.AreEqual(banded, config.Kinds.Select(k => k.Id).ToArray());
            Assert.IsTrue(config.Kinds.All(k => k.DistanceMaxM > 0f));
        }
    }
}
