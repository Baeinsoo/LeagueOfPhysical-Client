using System.IO;
using Luban;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 표 행 → <see cref="CardRushConfig"/> 연결(4차 spec §6). 새 열 둘(구덩이 비율·구덩이 기절)이 인수 순서대로 맞는 칸에 가는지 본다 —
    /// 둘 다 float이라 바꿔 넘겨도 컴파일이 되고, 시작 값(0.35 / 0.8)은 바꿔도 검증을 지나간다.
    /// </summary>
    public class CardRushConfigProviderTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        //  열 순서 = 생성된 CardRushConfig 생성자 읽기 순서. 값마다 달라 칸이 바뀌면 걸린다.
        private static ByteBuf Row(float barricadeShare, float pitShare, float pitStunSeconds)
        {
            var b = new ByteBuf(256);
            b.WriteSize(1);                 // 행 수
            b.WriteInt(1);                  // id
            b.WriteFloat(150f);             // match_duration_seconds
            b.WriteInt(5);                  // min_lanes
            b.WriteFloat(141f);             // track_length_m
            b.WriteFloat(12f);              // obstacle_gap_min_m
            b.WriteFloat(18f);              // obstacle_gap_max_m
            b.WriteFloat(barricadeShare);
            b.WriteFloat(29f);              // zone_gap_min_m
            b.WriteFloat(49f);              // zone_gap_max_m
            b.WriteFloat(2.5f);             // barricade_wait_seconds
            b.WriteFloat(1.4f);             // roll_knock_m
            b.WriteFloat(1.1f);             // roll_stun_seconds
            b.WriteFloat(2.8f);             // zone_knock_m
            b.WriteFloat(1.6f);             // zone_stun_seconds
            b.WriteFloat(0.23f);            // bot_react_min_seconds
            b.WriteFloat(0.41f);            // bot_react_max_seconds
            b.WriteFloat(0.21f);            // bot_react_extra_seconds
            b.WriteFloat(0.03f);            // bot_hesitate_chance
            b.WriteFloat(0.52f);            // bot_free_dash_chance
            b.WriteFloat(pitShare);
            b.WriteFloat(pitStunSeconds);
            return b;
        }

        private static CardRushConfigProvider ProviderWith(ByteBuf row)
        {
            var tables = new LOP.MasterData.Tables(file => file == "tbcardrushconfig"
                ? row
                : new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables)).SetValue(md, tables);
            return new CardRushConfigProvider(md);
        }

        [Test]
        public void 구덩이_비율과_구덩이_기절은_맞는_칸으로_간다()
        {
            //  세 값이 모두 다르다 — 구덩이 비율 0.25, 구덩이 기절 1.5초, 바리케이드 비율 0.4.
            var config = ProviderWith(Row(0.4f, 0.25f, 1.5f)).Get();
            Assert.AreEqual(0.25f, config.PitShare, 1e-6f);
            Assert.AreEqual(1.5f, config.PitStunSeconds, 1e-6f);
            Assert.AreEqual(0.4f, config.BarricadeShare, 1e-6f);
            Assert.AreEqual(250, config.Tuning.PitPermille, "판정이 보는 값도 같은 칸에서");
            Assert.AreEqual(75, config.Tuning.PitStunTicks, "1.5초 × 50Hz");
            Assert.AreEqual(400, config.Tuning.BarricadePermille);
        }

        [Test]
        public void 앞_열도_인수_순서대로_간다()
        {
            var config = ProviderWith(Row(0.4f, 0.25f, 1.5f)).Get();
            Assert.AreEqual(150f, config.MatchDurationSeconds);
            Assert.AreEqual(5, config.MinLanes);
            Assert.AreEqual(141f, config.TrackLengthM);
            Assert.AreEqual(2.5f, config.BarricadeWaitSeconds);
            Assert.AreEqual(1.1f, config.RollStunSeconds);
            Assert.AreEqual(1.6f, config.ZoneStunSeconds);
            Assert.AreEqual(0.52f, config.BotFreeDashChance, 1e-6f);
        }
    }
}
