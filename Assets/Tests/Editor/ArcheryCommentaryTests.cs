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

        [Test]
        public void 관중석_닭_로빈_후드_문장()
        {
            var c = First();
            c.TrySay(ArcheryLine.CrowdHit, "2P 선수", 0, now: 0f);
            Assert.AreEqual("아아— 화살이 관중석으로!", c.Text);
            c.TrySay(ArcheryLine.Chicken, "2P 선수", 0, now: 10f);
            Assert.AreEqual("닭이 제일 놀랐습니다", c.Text);
            c.TrySay(ArcheryLine.RobinHood, "2P 선수", 0, now: 20f);
            Assert.AreEqual("로빈 후드!!! 화살이 화살을 쪼갭니다!", c.Text);
        }

        [Test]
        public void 로빈_후드는_10점_해설을_덮고_관중석은_10점과_같은_급()
        {
            Assert.AreEqual(7, ArcheryCommentary.PriorityOf(ArcheryLine.RobinHood));
            Assert.AreEqual(3, ArcheryCommentary.PriorityOf(ArcheryLine.CrowdHit));
            Assert.AreEqual(3, ArcheryCommentary.PriorityOf(ArcheryLine.Chicken));
            var c = First();
            c.TrySay(ArcheryLine.Bull, "a", 0, now: 0f);
            Assert.IsTrue(c.TrySay(ArcheryLine.RobinHood, "b", 0, now: 0.1f));
        }

        [Test]
        public void 새_문장도_이름을_채운다()
        {
            var c = new ArcheryCommentary(n => n - 1);   // 마지막 문장
            c.TrySay(ArcheryLine.RobinHood, "3P 선수", 0, now: 0f);
            Assert.AreEqual("숲의 의적이 돌아왔습니다, 3P 선수", c.Text);
        }

        [Test]
        public void 바람_문장에_관중석_깃발이_더해졌다()
        {
            var c = new ArcheryCommentary(n => n - 1);   // 마지막 문장
            c.TrySay(ArcheryLine.Wind, "오른", 0, now: 0f);
            Assert.AreEqual("관중석 깃발 보세요, 바람이 오른쪽입니다", c.Text);
        }

        [Test]
        public void NoHit_문장에_바람을_못_읽었다가_더해졌다()
        {
            var c = new ArcheryCommentary(n => n - 1);   // 마지막 문장
            c.TrySay(ArcheryLine.NoHit, "민수 선수", 0, now: 0f);
            Assert.AreEqual("민수 선수, 바람을 못 읽었네요", c.Text);
        }
    }
}
