using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShootOffResultTrackerTests
    {
        //  구독은 Initialize 때만 한다 — 시험은 의존 없이 메서드를 직접 부른다.
        private static ArcheryShootOffResultTracker Tracker() => new ArcheryShootOffResultTracker(null, null, null);

        private static ArcheryRoundResultEvent Result(params ArcheryRoundPlacement[] p)
            => new ArcheryRoundResultEvent(2, 1, new List<ArcheryRoundPlacement>(p));

        private static ArcheryRoundPlacement P(string id, bool hit, float d, int rank)
            => new ArcheryRoundPlacement(id, hit, new Vector2(d, 0f), d, rank, 0);

        [Test]
        public void 결과는_순위_순으로_정리된다()
        {
            var t = Tracker();
            t.OnRoundResult(Result(P("c", true, 0.3f, 2), P("a", true, 0.1f, 0), P("b", true, 0.2f, 1)), 5f, 1000L);
            Assert.AreEqual("a", t.ByRank[0].ShooterId);
            Assert.AreEqual("c", t.ByRank[2].ShooterId);
            Assert.AreEqual(1, t.Version);
        }

        [Test]
        public void 닫는_틱_전까지만_보인다()
        {
            var t = Tracker();
            Assert.IsFalse(t.IsShowing(0d));
            t.OnRoundResult(Result(P("a", true, 0.1f, 0)), 5f, 1000L);
            Assert.IsTrue(t.IsShowing(999.9d));
            Assert.IsFalse(t.IsShowing(1000d));
        }

        [Test]
        public void 닫는_틱이_지난_뒤_도착한_결과는_열리지_않는다()
        {
            var t = Tracker();
            t.OnRoundResult(Result(P("a", true, 0.1f, 0)), 5f, 1000L);
            Assert.IsFalse(t.IsShowing(1200d));
        }

        [Test]
        public void 결과_중에는_1등이_뛰고_꼴찌가_주저앉는다()
        {
            var t = Tracker();
            t.OnRoundResult(Result(P("a", true, 0.1f, 0), P("b", true, 0.2f, 1), P("c", false, 0f, 2)), 5f, 1000L);
            Assert.Greater(t.PoseOf("a", 5.1f, 900d).Lift, 0f);
            Assert.AreEqual(0f, t.PoseOf("b", 5.1f, 900d).Lift);
            Assert.AreEqual(12f, t.PoseOf("c", 5.1f, 900d).TiltDegrees, 1e-5f);
        }

        [Test]
        public void 결과가_닫히면_자세가_풀린다()
        {
            var t = Tracker();
            t.OnRoundResult(Result(P("a", true, 0.1f, 0), P("c", false, 0f, 1)), 5f, 1000L);
            Assert.AreEqual(0f, t.PoseOf("c", 9f, 1000d).TiltDegrees);
        }

        [Test]
        public void 누가_10점이면_그_사람만_잠깐_뛴다()
        {
            var t = Tracker();
            t.OnHit(new ArcheryTargetHitEvent("a", 100L, 10), 3f);
            t.OnHit(new ArcheryTargetHitEvent("b", 101L, 9), 3f);
            Assert.Greater(t.PoseOf("a", 3.1f, 0d).Lift, 0f);
            Assert.AreEqual(0f, t.PoseOf("b", 3.1f, 0d).Lift);
            Assert.AreEqual(0f, t.PoseOf("a", 4.5f, 0d).Lift);
        }

        [Test]
        public void 공동_꼴찌는_둘_다_주저앉는다()
        {
            var t = Tracker();
            t.OnRoundResult(Result(P("a", true, 0.1f, 0), P("b", true, 0.2f, 1),
                                    P("c", true, 0.3f, 2), P("d", true, 0.3f, 2)), 5f, 1000L);
            Assert.AreEqual(12f, t.PoseOf("c", 5.1f, 900d).TiltDegrees, 1e-5f);
            Assert.AreEqual(12f, t.PoseOf("d", 5.1f, 900d).TiltDegrees, 1e-5f);
        }

        [Test]
        public void 결과에_없는_사람은_리액션이_없다()
        {
            var t = Tracker();
            t.OnRoundResult(Result(P("a", true, 0.1f, 0)), 5f, 1000L);
            Assert.AreEqual(0f, t.PoseOf("stranger", 5.1f, 900d).Lift);
        }
    }
}
