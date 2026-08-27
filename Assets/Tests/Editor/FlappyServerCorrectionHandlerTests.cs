using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyServerCorrectionHandlerTests
    {
        [Test]
        public void 둘_다_안_멈췄으면_맞는다()
        {
            Assert.IsTrue(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 서버는_멈췄는데_내가_안_멈췄으면_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 50, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 내가_멈췄는데_서버는_안_멈췄으면_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0.4f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 무적_여부가_달라도_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 30, tick: 10));
        }

        [Test]
        public void 종료틱이_이미_지났으면_안_멈춘_것이다()
        {
            //  같은 값이라도 틱이 지나면 뜻이 뒤집힌다 — 비교는 반드시 같은 시점 기준이어야 한다.
            Assert.IsTrue(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 50, snapInvulnEnd: 0, tick: 50));
        }
    }
}
