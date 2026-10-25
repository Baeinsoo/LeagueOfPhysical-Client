using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 화면 순위의 규칙(spec §1): 결승한 새는 서버 등수, 아직 달리는 새는 그려진 x로 매긴다.
    /// 같은 x로 출발하는 판이라 동률이 기본값이다 — 동률에서 순서가 흔들리면 숫자가 프레임마다 깜빡인다.
    /// </summary>
    public class RaceStandingsTests
    {
        private static List<string> Ids(List<RaceBirdSample> birds)
        {
            var ids = new List<string>();
            foreach (var b in birds) { ids.Add(b.Id); }
            return ids;
        }

        [Test]
        public void 결승한_새가_달리는_새보다_앞선다()
        {
            var birds = new List<RaceBirdSample> { new RaceBirdSample("runner", 0, 500f), new RaceBirdSample("done", 2, 100f) };

            RaceStandings.Order(birds);

            CollectionAssert.AreEqual(new[] { "done", "runner" }, Ids(birds));
        }

        [Test]
        public void 결승한_새끼리는_등수_순()
        {
            var birds = new List<RaceBirdSample> { new RaceBirdSample("second", 2, 900f), new RaceBirdSample("first", 1, 10f) };

            RaceStandings.Order(birds);

            CollectionAssert.AreEqual(new[] { "first", "second" }, Ids(birds));
        }

        [Test]
        public void 달리는_새는_x가_큰_쪽이_앞()
        {
            var birds = new List<RaceBirdSample>
            {
                new RaceBirdSample("back", 0, 10f), new RaceBirdSample("front", 0, 30f), new RaceBirdSample("mid", 0, 20f),
            };

            RaceStandings.Order(birds);

            CollectionAssert.AreEqual(new[] { "front", "mid", "back" }, Ids(birds));
        }

        [Test]
        public void x가_같으면_id_순으로_고정()
        {
            var forward = new List<RaceBirdSample> { new RaceBirdSample("a", 0, 5f), new RaceBirdSample("b", 0, 5f) };
            var reverse = new List<RaceBirdSample> { new RaceBirdSample("b", 0, 5f), new RaceBirdSample("a", 0, 5f) };

            RaceStandings.Order(forward);
            RaceStandings.Order(reverse);

            CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(forward));
            CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(reverse), "들어온 순서와 상관없이 같은 답");
        }

        [Test]
        public void 순위는_1부터_없는_id는_0()
        {
            var birds = new List<RaceBirdSample> { new RaceBirdSample("a", 0, 30f), new RaceBirdSample("me", 0, 10f) };
            RaceStandings.Order(birds);

            Assert.AreEqual(2, RaceStandings.RankOf(birds, "me"));
            Assert.AreEqual(1, RaceStandings.RankOf(birds, "a"));
            Assert.AreEqual(0, RaceStandings.RankOf(birds, "ghost"));
            Assert.AreEqual(0, RaceStandings.RankOf(birds, null));
        }
    }
}
