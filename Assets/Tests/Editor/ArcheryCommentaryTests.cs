using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryCommentaryTests
    {
        private static ArcheryCommentary First() => new ArcheryCommentary(n => 0);

        [Test]
        public void 이름과_숫자를_채운다()
        {
            var c = First();
            Assert.IsTrue(c.TrySay(ArcheryLine.Close, "민수 선수", 2, now: 0f));
            Assert.AreEqual("단 2cm!! 숨막히는 차이입니다", c.Text);
        }

        [Test]
        public void 떠_있는_자막보다_낮은_우선순위는_못_덮는다()
        {
            var c = First();
            c.TrySay(ArcheryLine.Comeback, "당신", 0, now: 0f);
            Assert.IsFalse(c.TrySay(ArcheryLine.Win, "민수 선수", 0, now: 1f));
            Assert.AreEqual("꼴찌에서 1등으로! 당신 대역전!", c.Text);
        }

        [Test]
        public void 시간이_지나면_무엇이든_뜬다()
        {
            var c = First();
            c.TrySay(ArcheryLine.Comeback, "당신", 0, now: 0f);
            Assert.IsFalse(c.IsShowing(3f));
            Assert.IsTrue(c.TrySay(ArcheryLine.Win, "민수 선수", 0, now: 3f));
        }

        [Test]
        public void 같은_우선순위는_덮는다()
        {
            var c = First();
            c.TrySay(ArcheryLine.Streak, "a", 3, now: 0f);
            Assert.IsTrue(c.TrySay(ArcheryLine.Comeback, "b", 0, now: 0.5f));
        }
    }
}
