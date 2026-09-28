using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryCrowdDirectorTests
    {
        //  주사위를 늘 0.5로 — 비율 0.6이면 모두 반응, 0.2면 아무도 안 한다. 시간 배수는 0.75 + 0.5·0.5 = 1.
        private static ArcheryCrowdDirector Half(int count = 4) => new ArcheryCrowdDirector(count, () => 0.5f);

        private static int Count(ArcheryCrowdDirector d, ArcheryCrowdMood mood, float now)
        {
            int n = 0;
            for (int i = 0; i < d.Count; i++) { if (d.MoodOf(i, now) == mood) { n++; } }
            return n;
        }

        [Test]
        public void 처음엔_모두_대기()
        {
            Assert.AreEqual(4, Count(Half(), ArcheryCrowdMood.Idle, 0f));
        }

        [Test]
        public void 십점이면_60퍼센트_주사위를_넘은_사람이_환호하고_1_4초_뒤_돌아온다()
        {
            var d = Half();
            d.OnBandHit(10, now: 1f);
            Assert.AreEqual(4, Count(d, ArcheryCrowdMood.Cheer, 1.1f));
            Assert.AreEqual(4, Count(d, ArcheryCrowdMood.Cheer, 2.39f));
            Assert.AreEqual(4, Count(d, ArcheryCrowdMood.Idle, 2.41f));
        }

        [Test]
        public void 비율보다_큰_주사위는_반응하지_않는다()
        {
            var d = Half();
            d.OnBandHit(9, now: 0f);   // 20% — 0.5는 못 넘는다
            Assert.AreEqual(4, Count(d, ArcheryCrowdMood.Idle, 0.1f));
        }

        [Test]
        public void 주사위_순서대로_일부만_반응한다()
        {
            //  관중마다 "반응하나" 주사위 → 반응하면 "얼마나" 주사위. 0.1(반응)·0.5(시간), 0.9(안 함), 0.1·0.5, 0.9
            var rolls = new Queue<float>(new[] { 0.1f, 0.5f, 0.9f, 0.1f, 0.5f, 0.9f });
            var d = new ArcheryCrowdDirector(4, () => rolls.Dequeue());
            d.OnMiss(now: 0f);   // 20% 웃음
            Assert.AreEqual(ArcheryCrowdMood.Laugh, d.MoodOf(0, 0.1f));
            Assert.AreEqual(ArcheryCrowdMood.Idle, d.MoodOf(1, 0.1f));
            Assert.AreEqual(ArcheryCrowdMood.Laugh, d.MoodOf(2, 0.1f));
            Assert.AreEqual(ArcheryCrowdMood.Idle, d.MoodOf(3, 0.1f));
        }

        [Test]
        public void 기본_기분이_정적이면_반응이_끝나고_정적으로_돌아간다()
        {
            var d = Half();
            d.BaseMood = ArcheryCrowdMood.Hush;
            d.OnBandHit(10, now: 0f);
            Assert.AreEqual(ArcheryCrowdMood.Cheer, d.MoodOf(0, 1f));
            Assert.AreEqual(ArcheryCrowdMood.Hush, d.MoodOf(0, 2f));
        }

        [Test]
        public void 관중석에_맞은_사람은_계속_환호하고_다른_반응에_덮이지_않는다()
        {
            var d = Half();
            d.OnCrowdHit(victim: 2, now: 0f);
            Assert.IsTrue(d.HasArrow(2));
            Assert.AreEqual(ArcheryCrowdMood.Cheer, d.MoodOf(2, 100f));
            d.OnResult(ArcheryLine.NoHit, finalRound: false, now: 101f);
            Assert.AreEqual(ArcheryCrowdMood.Cheer, d.MoodOf(2, 101.1f));
            Assert.AreEqual(ArcheryCrowdMood.Boo, d.MoodOf(0, 101.1f));
        }

        [Test]
        public void 관중석_명중은_주변이_웃는다()
        {
            var d = new ArcheryCrowdDirector(4, () => 0.3f);   // 40% 웃음 — 0.3은 넘는다
            d.OnCrowdHit(victim: 0, now: 0f);
            Assert.AreEqual(ArcheryCrowdMood.Laugh, d.MoodOf(1, 0.1f));
            Assert.AreEqual(ArcheryCrowdMood.Cheer, d.MoodOf(0, 0.1f));
        }

        [TestCase(ArcheryLine.Close, ArcheryCrowdMood.Gasp)]
        [TestCase(ArcheryLine.Streak, ArcheryCrowdMood.Chant)]
        [TestCase(ArcheryLine.Comeback, ArcheryCrowdMood.Cheer)]
        [TestCase(ArcheryLine.NoHit, ArcheryCrowdMood.Boo)]
        public void 결과_분류마다_기분이_정해져_있다(ArcheryLine line, ArcheryCrowdMood mood)
        {
            var d = Half();
            d.OnResult(line, finalRound: false, now: 0f);
            Assert.AreEqual(mood, d.MoodOf(0, 0.1f));
        }

        [Test]
        public void 그냥_승리는_반응하지_않는다()
        {
            var d = Half();
            d.OnResult(ArcheryLine.Win, finalRound: false, now: 0f);
            Assert.AreEqual(ArcheryCrowdMood.Idle, d.MoodOf(0, 0.1f));
        }

        [Test]
        public void 마지막_라운드는_전원_환호_위에_분류_반응을_덮는다()
        {
            var d = Half();
            d.OnResult(ArcheryLine.Win, finalRound: true, now: 0f);
            Assert.AreEqual(ArcheryCrowdMood.Cheer, d.MoodOf(3, 2.5f));
            var e = Half();
            e.OnResult(ArcheryLine.Close, finalRound: true, now: 0f);
            Assert.AreEqual(ArcheryCrowdMood.Gasp, e.MoodOf(0, 0.1f));
        }

        [Test]
        public void 판_끝과_로빈_후드는_전원_환호()
        {
            var d = new ArcheryCrowdDirector(3, () => 0.99f);
            d.OnMatchEnd(now: 0f);
            Assert.AreEqual(ArcheryCrowdMood.Cheer, d.MoodOf(0, 0.1f));
            var e = new ArcheryCrowdDirector(3, () => 0.99f);
            e.OnRobinHood(now: 0f);
            Assert.AreEqual(ArcheryCrowdMood.Cheer, e.MoodOf(2, 0.1f));
        }
    }
}
