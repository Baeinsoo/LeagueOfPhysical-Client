using GameFramework.Http;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>2a 로비 응답을 클라가 쓰는 역직렬화기 그대로 읽는다 — 특히 첫 배치 판의 null 앞 단계.</summary>
    public class RankDtoTests
    {
        [Test]
        public void 배치_첫_판의_null_앞_단계를_읽는다()
        {
            const string json = "{\"code\":200,\"match\":{\"matchId\":\"R1\",\"queueId\":2,\"endedAt\":\"2026-10-07T00:00:00.000Z\",\"rounds\":[{\"index\":0,\"gameModeId\":7,\"mapId\":1}],"
                + "\"participants\":[{\"userId\":\"U1\",\"displayName\":\"a#AAAAAA\",\"placement\":1,\"mmrBefore\":1000,\"mmrAfter\":1100,\"stats\":{},"
                + "\"rank\":{\"divisionBefore\":null,\"lpBefore\":0,\"divisionAfter\":4,\"lpAfter\":80,\"placementPlayed\":1,\"placementGames\":5,\"promoted\":false,\"demoted\":false,\"wasPlacement\":true}}]}}";

            var r = HttpJson.DeserializeObject<GetMyMatchResponse>(json);

            Assert.IsNull(r.match.participants[0].rank.divisionBefore);
            Assert.AreEqual(80, r.match.participants[0].rank.lpAfter);
            Assert.IsTrue(r.match.participants[0].rank.wasPlacement);
        }

        [Test]
        public void 캐주얼_참가자는_rank가_없다()
        {
            const string json = "{\"userId\":\"U1\",\"displayName\":\"a\",\"placement\":1,\"mmrBefore\":1000,\"mmrAfter\":1100,\"stats\":{}}";

            var p = HttpJson.DeserializeObject<MatchHistoryParticipantDto>(json);

            Assert.IsNull(p.rank);
        }

        [Test]
        public void 내_랭크_응답을_읽는다()
        {
            const string json = "{\"code\":200,\"rank\":{\"season\":1,\"split\":3,\"divisionIndex\":-1,\"tierCode\":\"\",\"division\":0,\"lp\":0,\"placementPlayed\":0,\"placementGames\":5,\"peakDivisionIndex\":-1,\"gamesPlayed\":0}}";

            var r = HttpJson.DeserializeObject<GetRankResponse>(json);

            Assert.AreEqual(-1, r.rank.divisionIndex);
            Assert.AreEqual(5, r.rank.placementGames);
        }
    }
}
