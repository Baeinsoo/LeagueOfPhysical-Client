using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryReactionTests
    {
        private const float None = float.NegativeInfinity;

        [Test]
        public void 역할_1등은_뜀_꼴찌와_빗나감은_주저앉음()
        {
            Assert.AreEqual(ArcheryReactionRole.Winner, ArcheryReaction.RoleOf(0, 4, true));
            Assert.AreEqual(ArcheryReactionRole.None, ArcheryReaction.RoleOf(1, 4, true));
            Assert.AreEqual(ArcheryReactionRole.Slump, ArcheryReaction.RoleOf(3, 4, true));
            Assert.AreEqual(ArcheryReactionRole.Slump, ArcheryReaction.RoleOf(1, 4, false));
        }

        [Test]
        public void 혼자면_뜀만()
        {
            Assert.AreEqual(ArcheryReactionRole.Winner, ArcheryReaction.RoleOf(0, 1, true));
        }

        [Test]
        public void 명중_10점_뒤_0_9초_안에만_뛴다()
        {
            Assert.Greater(ArcheryReaction.PoseAt(10.1f, 10f, ArcheryReactionRole.None, 0f).Lift, 0f);
            Assert.AreEqual(0f, ArcheryReaction.PoseAt(10.95f, 10f, ArcheryReactionRole.None, 0f).Lift);
            Assert.AreEqual(0f, ArcheryReaction.PoseAt(10f, None, ArcheryReactionRole.None, 0f).Lift);
        }

        [Test]
        public void 뛰는_높이는_0_25m를_넘지_않는다()
        {
            for (float t = 0f; t < 0.9f; t += 0.01f)
            {
                Assert.LessOrEqual(ArcheryReaction.PoseAt(t, 0f, ArcheryReactionRole.None, 0f).Lift, 0.25f + 1e-5f);
            }
        }

        [Test]
        public void 결과_중_1등은_계속_뛰고_꼴찌는_가라앉아_기운다()
        {
            Assert.Greater(ArcheryReaction.PoseAt(5.1f, None, ArcheryReactionRole.Winner, 5f).Lift, 0f);
            var slump = ArcheryReaction.PoseAt(5.1f, None, ArcheryReactionRole.Slump, 5f);
            Assert.AreEqual(-0.15f, slump.Lift, 1e-5f);
            Assert.AreEqual(12f, slump.TiltDegrees, 1e-5f);
        }

        [Test]
        public void 흔들림은_0_3초_뒤_0이고_6cm를_넘지_않는다()
        {
            Assert.AreEqual(Vector3.zero, ArcheryBullseyeFx.ShakeOffset(0.3f));
            Assert.AreEqual(Vector3.zero, ArcheryBullseyeFx.ShakeOffset(-1f));
            for (float t = 0f; t < 0.3f; t += 0.005f)
            {
                var o = ArcheryBullseyeFx.ShakeOffset(t);
                Assert.LessOrEqual(Mathf.Abs(o.x), 0.06f + 1e-5f);
                Assert.LessOrEqual(Mathf.Abs(o.y), 0.06f + 1e-5f);
                Assert.AreEqual(0f, o.z);
            }
            Assert.Greater(ArcheryBullseyeFx.ShakeOffset(0.02f).magnitude, 0f);
        }

        [Test]
        public void 번쩍임은_0_1초_동안만()
        {
            Assert.Greater(ArcheryBullseyeFx.FlashAlpha(0f), 0f);
            Assert.AreEqual(0f, ArcheryBullseyeFx.FlashAlpha(0.1f));
            Assert.AreEqual(0f, ArcheryBullseyeFx.FlashAlpha(-0.01f));
        }
    }
}
