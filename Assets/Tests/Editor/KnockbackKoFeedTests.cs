using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackKoFeedTests
    {
        static KoPlayer P(string id, long gone = -1, string by = "", bool chain = false, int team = 0)
            => new KoPlayer(id, gone, by, chain, team);

        [Test]
        public void 새로_탈락한_사람만_알린다()
        {
            var feed = new KnockbackKoFeed();
            feed.Observe(new[] { P("a"), P("b"), P("c") });

            var ev = feed.Observe(new[] { P("a"), P("b", 100, "a"), P("c") });
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual("b", ev[0].VictimId);
            Assert.AreEqual("a", ev[0].KillerId);

            // 판본만 올라가고 같은 탈락이 또 오면 알리지 않는다
            Assert.AreEqual(0, feed.Observe(new[] { P("a"), P("b", 100, "a"), P("c") }).Count);
        }

        [Test]
        public void 처음_받은_상태의_탈락자는_알리지_않는다()
        {
            var feed = new KnockbackKoFeed();
            var first = feed.Observe(new[] { P("a"), P("b", 100, "a") });
            Assert.AreEqual(0, first.Count);

            Assert.AreEqual(0, feed.Observe(new[] { P("a"), P("b", 100, "a"), P("c") }).Count);
        }

        [Test]
        public void 자멸은_장외시킨_사람이_없다()
        {
            var feed = new KnockbackKoFeed();
            feed.Observe(new[] { P("a"), P("b"), P("c") });
            var ev = feed.Observe(new[] { P("a"), P("b", 100, ""), P("c", 101, "c") });
            Assert.IsNull(ev[0].KillerId);
            Assert.IsNull(ev[1].KillerId);   // 제 이름이 적혀 와도 자멸
        }

        [Test]
        public void 문장_셋()
        {
            Assert.AreEqual("A ▶ B 장외!", KnockbackKoFeed.Line("A", "B", false, 1));
            Assert.AreEqual("A ▶ B 연쇄 장외!", KnockbackKoFeed.Line("A", "B", true, 1));
            Assert.AreEqual("B 선수, 스스로 장외", KnockbackKoFeed.Line(null, "B", false, 1));
        }

        [Test]
        public void 더블_트리플_문장()
        {
            Assert.AreEqual("A ▶ B 장외! 더블 장외!", KnockbackKoFeed.Line("A", "B", false, 2));
            Assert.AreEqual("A ▶ B 장외! 트리플 장외!", KnockbackKoFeed.Line("A", "B", false, 3));
            Assert.AreEqual("A ▶ B 장외! 트리플 장외!", KnockbackKoFeed.Line("A", "B", false, 5));
        }

        [Test]
        public void 직전_장외로부터_150틱_이내면_이어지고_넘으면_끊긴다()
        {
            var feed = new KnockbackKoFeed();
            feed.Observe(new[] { P("a"), P("b"), P("c"), P("d"), P("e") });

            var e1 = feed.Observe(new[] { P("a"), P("b", 100, "a"), P("c"), P("d"), P("e") });
            Assert.AreEqual(1, e1[0].Streak);
            var e2 = feed.Observe(new[] { P("a"), P("b", 100, "a"), P("c", 250, "a"), P("d"), P("e") });   // 정확히 150
            Assert.AreEqual(2, e2[0].Streak);
            var e3 = feed.Observe(new[] { P("a"), P("b", 100, "a"), P("c", 250, "a"), P("d", 400, "a"), P("e") });
            Assert.AreEqual(3, e3[0].Streak);
            var e4 = feed.Observe(new[] { P("a"), P("b", 100, "a"), P("c", 250, "a"), P("d", 400, "a"), P("e", 551, "a") });   // 151
            Assert.AreEqual(1, e4[0].Streak);
        }

        [Test]
        public void 연속_장외는_장외시킨_사람별로_센다()
        {
            var feed = new KnockbackKoFeed();
            feed.Observe(new[] { P("a"), P("b"), P("c"), P("d") });
            var ev = feed.Observe(new[] { P("a"), P("b"), P("c", 100, "a"), P("d", 110, "b") });
            Assert.AreEqual(1, ev[0].Streak);
            Assert.AreEqual(1, ev[1].Streak);
        }

        [Test]
        public void 같은_메시지의_둘은_탈락_틱_순서로_센다()
        {
            var feed = new KnockbackKoFeed();
            feed.Observe(new[] { P("a"), P("b"), P("c"), P("d") });
            var ev = feed.Observe(new[] { P("a"), P("b", 120, "a"), P("c", 100, "a"), P("d") });
            Assert.AreEqual("c", ev[0].VictimId);
            Assert.AreEqual(1, ev[0].Streak);
            Assert.AreEqual(2, ev[1].Streak);
        }

        [Test]
        public void 개인전_피니시는_한_명_남고_참가자가_둘_이상일_때()
        {
            Assert.IsTrue(KnockbackKoFeed.IsFinish(new[] { P("a"), P("b", 1), P("c", 2) }, 0));
            Assert.IsFalse(KnockbackKoFeed.IsFinish(new[] { P("a"), P("b"), P("c", 2) }, 0));
            Assert.IsFalse(KnockbackKoFeed.IsFinish(new[] { P("a", 1) }, 0));   // 한 명뿐인 연습 판
            Assert.IsFalse(KnockbackKoFeed.IsFinish(new[] { P("a") }, 0));
        }

        [Test]
        public void 팀전_피니시는_한_팀이_전원_탈락했을_때()
        {
            var running = new[] { P("a", team: 1), P("b", 5, team: 1), P("c", team: 2), P("d", team: 2) };
            Assert.IsFalse(KnockbackKoFeed.IsFinish(running, 2));
            var over = new[] { P("a", 9, team: 1), P("b", 5, team: 1), P("c", team: 2), P("d", 3, team: 2) };
            Assert.IsTrue(KnockbackKoFeed.IsFinish(over, 2));
        }
    }
}
