using NUnit.Framework;

namespace LOP.Tests
{
    public class ChibiReactionTests
    {
        [Test]
        public void 우승과_10점은_환호_빗나감과_꼴찌는_좌절()
        {
            Assert.AreEqual(ArcheryReactionCue.Cheer, ArcheryReaction.CueAt(5f, float.NegativeInfinity, ArcheryReactionRole.Winner));
            Assert.AreEqual(ArcheryReactionCue.Slump, ArcheryReaction.CueAt(5f, float.NegativeInfinity, ArcheryReactionRole.Slump));
            Assert.AreEqual(ArcheryReactionCue.Cheer, ArcheryReaction.CueAt(5.5f, 5f, ArcheryReactionRole.None));
            Assert.AreEqual(ArcheryReactionCue.None, ArcheryReaction.CueAt(6f, 5f, ArcheryReactionRole.None));   // 0.9초 지남
            Assert.AreEqual(ArcheryReactionCue.None, ArcheryReaction.CueAt(5f, float.NegativeInfinity, ArcheryReactionRole.None));
        }

        [Test]
        public void 신호마다_애니와_표정()
        {
            var cheer = ChibiReaction.Of(ArcheryReactionCue.Cheer, drawing: false);
            Assert.AreEqual("Happy", cheer.Trigger);
            Assert.AreEqual(ChibiExpression.Cheer, cheer.Expression);

            var slump = ChibiReaction.Of(ArcheryReactionCue.Slump, drawing: false);
            Assert.AreEqual("Sad", slump.Trigger);
            Assert.AreEqual(ChibiExpression.Despair, slump.Expression);

            var aim = ChibiReaction.Of(ArcheryReactionCue.None, drawing: true);
            Assert.IsNull(aim.Trigger);
            Assert.AreEqual(ChibiExpression.Focus, aim.Expression);

            var idle = ChibiReaction.Of(ArcheryReactionCue.None, drawing: false);
            Assert.IsNull(idle.Trigger);
            Assert.AreEqual(ChibiExpression.Normal, idle.Expression);
        }

        [Test]
        public void 사건이_조준보다_앞선다()
        {
            Assert.AreEqual(ChibiExpression.Cheer, ChibiReaction.Of(ArcheryReactionCue.Cheer, drawing: true).Expression);
        }

        [Test]
        public void 같은_신호면_트리거가_없다()
        {
            Assert.IsNull(ChibiReaction.TriggerOnChange(ArcheryReactionCue.Cheer, ArcheryReactionCue.Cheer));
            Assert.IsNull(ChibiReaction.TriggerOnChange(ArcheryReactionCue.Cheer, ArcheryReactionCue.None));
            Assert.AreEqual("Happy", ChibiReaction.TriggerOnChange(ArcheryReactionCue.None, ArcheryReactionCue.Cheer));
            Assert.AreEqual("Sad", ChibiReaction.TriggerOnChange(ArcheryReactionCue.Cheer, ArcheryReactionCue.Slump));
        }

        [Test]
        public void 놀람은_사건보다_뒤_조준보다_앞()
        {
            Assert.AreEqual(ChibiExpression.Surprise, ChibiReaction.Of(ArcheryReactionCue.None, drawing: true, surprised: true).Expression);
            Assert.AreEqual(ChibiExpression.Cheer, ChibiReaction.Of(ArcheryReactionCue.Cheer, drawing: false, surprised: true).Expression);
            Assert.AreEqual(ChibiExpression.Focus, ChibiReaction.Of(ArcheryReactionCue.None, drawing: true, surprised: false).Expression);
        }
    }
}
