using System;
using GameFramework.Http;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>퀘스트 응답(spec 2026-10-30 §6)을 HttpJson이 그대로 읽는지 — 날짜·칸·결과 진행.</summary>
    public class QuestDtoTests
    {
        private const string Quests =
            "{\"code\":200,\"serverNow\":\"2026-10-30T03:00:00.000Z\"," +
            "\"daily\":{\"periodKey\":\"2026-10-30\",\"endsAt\":\"2026-10-30T20:00:00.000Z\",\"rerollsLeft\":1,\"quests\":[" +
            "{\"id\":\"q1\",\"slot\":0,\"tier\":1,\"title\":\"경기 시간 5분 채우기\",\"unit\":\"minutes\",\"progress\":2,\"target\":5,\"completed\":false,\"rewardCoins\":50,\"rewardXp\":100,\"gameModeIds\":[]}]}," +
            "\"weekly\":{\"periodKey\":\"2026-10-26\",\"endsAt\":\"2026-11-01T20:00:00.000Z\",\"quests\":[" +
            "{\"id\":\"q4\",\"slot\":0,\"tier\":1,\"title\":\"플래피 레이스 5번 완주하기\",\"unit\":\"count\",\"progress\":5,\"target\":5,\"completed\":true,\"rewardCoins\":200,\"rewardXp\":400,\"gameModeIds\":[6]}]}}";

        [Test]
        public void 날짜는_UTC_DateTime으로_읽는다()
        {
            var r = HttpJson.DeserializeObject<GetQuestsResponse>(Quests);

            Assert.AreEqual(new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc), r.serverNow);
            Assert.AreEqual(DateTimeKind.Utc, r.daily.endsAt.Kind);
            Assert.AreEqual(new DateTime(2026, 10, 30, 20, 0, 0, DateTimeKind.Utc), r.daily.endsAt);
            Assert.AreEqual(new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc), r.weekly.endsAt);
        }

        [Test]
        public void 칸을_읽는다()
        {
            var r = HttpJson.DeserializeObject<GetQuestsResponse>(Quests);

            Assert.AreEqual(1, r.daily.rerollsLeft);
            var q = r.daily.quests[0];
            Assert.AreEqual("q1", q.id);
            Assert.AreEqual("minutes", q.unit);
            Assert.AreEqual(2, q.progress);
            Assert.AreEqual(50, q.rewardCoins);
            Assert.AreEqual(0, q.gameModeIds.Length);
            Assert.IsTrue(r.weekly.quests[0].completed);
            CollectionAssert.AreEqual(new[] { 6 }, r.weekly.quests[0].gameModeIds);
        }

        [Test]
        public void 결과_보상에_퀘스트_진행과_XP_몫을_읽는다()
        {
            const string json = "{\"userId\":\"U1\",\"placement\":1,\"reward\":{\"coins\":25,\"xp\":120,\"reason\":\"ok\"," +
                "\"extras\":[{\"kind\":\"combo\",\"coins\":7},{\"kind\":\"quest\",\"coins\":100,\"xp\":200}]," +
                "\"quests\":[{\"id\":\"q1\",\"period\":\"daily\",\"title\":\"아무 종목 4판 하기\",\"unit\":\"count\",\"before\":2,\"after\":3,\"target\":4,\"completed\":false,\"rewardCoins\":100,\"rewardXp\":200}]}}";

            var p = HttpJson.DeserializeObject<MatchHistoryParticipantDto>(json);

            Assert.AreEqual(0, p.reward.extras[0].xp, "콤보는 XP 칸이 없다 — 0");
            Assert.AreEqual(200, p.reward.extras[1].xp);
            Assert.AreEqual(1, p.reward.quests.Length);
            Assert.AreEqual(3, p.reward.quests[0].after);
            Assert.AreEqual("daily", p.reward.quests[0].period);
        }

        [Test]
        public void 퀘스트_칸이_없는_옛_응답은_quests가_null이다()
        {
            var p = HttpJson.DeserializeObject<MatchHistoryParticipantDto>("{\"userId\":\"U1\",\"placement\":1,\"reward\":{\"coins\":50,\"xp\":120,\"reason\":\"ok\"}}");
            Assert.IsNull(p.reward.quests);
        }

        [Test]
        public void 응답_코드_사본은_백엔드와_같다()
        {
            Assert.AreEqual(80012, ResponseCode.QUEST_NOT_FOUND);
            Assert.AreEqual(80013, ResponseCode.QUEST_ALREADY_COMPLETED);
            Assert.AreEqual(80014, ResponseCode.QUEST_REROLL_UNAVAILABLE);
        }
    }
}
