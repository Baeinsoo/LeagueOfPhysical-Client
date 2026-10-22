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

            //  맵 13은 무리 간격과 수명이 같은 값이라(둘 다 3.6초) 그대로면 그 두 칸을 바꿔 넘겨도 못 잡는다.
            //  이 시험에서만 수명을 다른 칸과 겹치지 않는 값으로 옮겨 놓는다 — 제공자가 옮기는 자리만 보면 되므로 값 자체는 상관없다.
            var others = new HashSet<int>
            {
                row.RoundCount, row.RoundIntroTicks, row.RoundHuntTicks, row.RoundResultTicks,
                row.VolleyPeriodTicks, row.VolleyCueTicks, row.VolleyQuietTicks,
            };
            int life = row.TargetLifeTicks;
            while (others.Contains(life)) { life++; }
            typeof(LOP.MasterData.ArcheryConfig).GetField(nameof(row.TargetLifeTicks)).SetValue(row, life);
            Assert.AreEqual(life, row.TargetLifeTicks, "수명 칸을 옮겨 놓지 못했다 — 이 시험이 간격·수명 바꿈을 못 잡는다");

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

        [Test]
        public void 투석기_표적의_움직임과_대박_칸을_제자리에_옮긴다()
        {
            var md = RealMasterData();
            var config = CatapultConfig(md);
            var row13 = md.Tables.TbArcheryConfig.GetOrDefault(CatapultMapId);
            Assert.AreEqual(row13.JackpotChance, config.Catapult.JackpotChance);
            Assert.Greater(config.Catapult.JackpotChance, 0f, "맵 13의 대박 확률이 0이면 이 시험이 아무것도 못 지킨다");
            Assert.AreNotEqual(row13.TrapChance, row13.JackpotChance, "두 확률이 같으면 둘을 바꿔 넘겨도 못 잡는다");
            foreach (var k in config.Kinds)
            {
                var row = md.Tables.TbArcheryTarget.GetOrDefault(k.Id);
                string c = row.Code;
                Assert.AreEqual(row.IsJackpot, k.IsJackpot, c);
                Assert.AreEqual(row.HeightMinM, k.Motion.HeightMinM, c); Assert.AreEqual(row.HeightMaxM, k.Motion.HeightMaxM, c);
                Assert.AreEqual(row.ArcMinM, k.Motion.ArcMinM, c); Assert.AreEqual(row.ArcMaxM, k.Motion.ArcMaxM, c);
                Assert.AreEqual(row.ClimbMinMps, k.Motion.ClimbMinMps, c); Assert.AreEqual(row.ClimbMaxMps, k.Motion.ClimbMaxMps, c);
                Assert.AreEqual(row.BobAmpM, k.Motion.BobAmplitudeM, c); Assert.AreEqual(row.BobHz, k.Motion.BobHz, c);
                Assert.AreEqual(row.SpinHz, k.Motion.SpinHz, c); Assert.AreEqual(row.LateralHalfM, k.Motion.LateralHalfM, c);
                //  3차 칸.
                Assert.AreEqual(row.SpawnSideMinM, k.Motion.SpawnSideMinM, c); Assert.AreEqual(row.SpawnSideMaxM, k.Motion.SpawnSideMaxM, c);
                Assert.AreEqual(row.LifeTicks, k.Motion.LifeTicks, c);
                Assert.AreEqual(row.SwingAmpM, k.Motion.SwingAmplitudeM, c); Assert.AreEqual(row.SwingDipM, k.Motion.SwingDipM, c);
            }
            //  역할이 실제로 갈리는지 — 다 같으면 위 비교가 칸 바꿔치기를 못 잡는다.
            Assert.AreEqual(4, config.NormalKinds.Count);
            Assert.AreEqual(2, config.JackpotKinds.Count);
            Assert.AreEqual(3, config.TrapKinds.Count);
            //  3차 칸끼리 값이 달라야 자리 바꿈이 잡힌다(바깥 띠 min ≠ max, 그네 폭 ≠ 깊이, 수명이 있는 것과 없는 것).
            Assert.IsTrue(config.Kinds.Any(k => k.Motion.SpawnSideMinM > 0f && k.Motion.SpawnSideMinM != k.Motion.SpawnSideMaxM));
            Assert.IsTrue(config.Kinds.Any(k => k.Motion.SwingAmplitudeM > 0f && k.Motion.SwingAmplitudeM != k.Motion.SwingDipM));
            Assert.IsTrue(config.Kinds.Any(k => k.Motion.LifeTicks > 0) && config.Kinds.Any(k => k.Motion.LifeTicks == 0));
        }
    }
}
