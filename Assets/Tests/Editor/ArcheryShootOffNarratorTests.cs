using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShootOffNarratorTests
    {
        private static ArcheryRoundResultEvent Round(int index, params (string id, bool hit, float d)[] shots)
        {
            var list = new List<ArcheryRoundShot>();
            foreach (var s in shots) { list.Add(new ArcheryRoundShot(s.id, s.hit, Vector2.zero, s.d)); }
            return new ArcheryRoundResultEvent(index, 1, ArcheryShootOffRanking.Rank(list, 1));
        }

        [Test]
        public void 차이가_3cm_이하면_접전()
        {
            var n = new ArcheryShootOffNarrator();
            var line = n.LineFor(Round(0, ("a", true, 0.10f), ("b", true, 0.12f), ("me", true, 0.5f)), "me",
                                 out string winner, out int cm);
            Assert.AreEqual(ArcheryLine.Close, line);
            Assert.AreEqual("a", winner);
            Assert.AreEqual(2, cm);
        }

        [Test]
        public void 세_번_연속_1등이면_연승()
        {
            var n = new ArcheryShootOffNarrator();
            for (int i = 0; i < 2; i++) { n.LineFor(Round(i, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out _); }
            var line = n.LineFor(Round(2, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out int k);
            Assert.AreEqual(ArcheryLine.Streak, line);
            Assert.AreEqual(3, k);
        }

        [Test]
        public void 지난_라운드_꼴찌가_1등이면_역전()
        {
            var n = new ArcheryShootOffNarrator();
            n.LineFor(Round(0, ("a", true, 0.1f), ("b", true, 0.5f)), "me", out _, out _);
            var line = n.LineFor(Round(1, ("a", true, 0.5f), ("b", true, 0.1f)), "me", out string winner, out _);
            Assert.AreEqual(ArcheryLine.Comeback, line);
            Assert.AreEqual("b", winner);
        }

        [Test]
        public void 첫_라운드는_역전이_아니다()
        {
            var n = new ArcheryShootOffNarrator();
            var line = n.LineFor(Round(0, ("a", true, 0.1f), ("b", true, 0.5f)), "me", out _, out _);
            Assert.AreEqual(ArcheryLine.Win, line);
        }

        [Test]
        public void 내가_못_맞히면_그_얘기()
        {
            var n = new ArcheryShootOffNarrator();
            var line = n.LineFor(Round(0, ("a", true, 0.1f), ("b", true, 0.5f), ("me", false, 0f)), "me",
                                 out string who, out _);
            Assert.AreEqual(ArcheryLine.NoHit, line);
            Assert.AreEqual("me", who);
        }

        [Test]
        public void 전원_못_맞히면_연승이_끊긴다()
        {
            var n = new ArcheryShootOffNarrator();
            for (int i = 0; i < 2; i++) { n.LineFor(Round(i, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out _); }
            n.LineFor(Round(2, ("a", false, 0f), ("me", false, 0f)), "me", out _, out _);
            var line = n.LineFor(Round(3, ("a", true, 0.1f), ("me", true, 0.5f)), "me", out _, out _);
            Assert.AreNotEqual(ArcheryLine.Streak, line);
        }
    }
}
