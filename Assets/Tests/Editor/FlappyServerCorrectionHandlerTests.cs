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

        // ApplyAuthoritative가 얼마나 오래 얼릴지 정하는 계산이다 — 부호와 경계를 틀리면
        // "멈춰야 하는데 안 멈춘다"가 아니라 "멈추는 시간 자체가 잘못된다"로 새서 더 눈에 띈다.

        [Test]
        public void 남은_틱만큼_초로_바뀐다()
        {
            // 끝나는 틱이 지금 틱보다 10틱 앞이고 한 틱이 0.02초면 정확히 0.2초가 남아야 한다.
            Assert.AreEqual(0.2f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 20, tick: 10, interval: 0.02f), 1e-4f);
        }

        [Test]
        public void 종료틱과_지금틱이_같으면_0이다()
        {
            // 경계값 — 막 끝난 순간이지 한 틱 더 남은 게 아니다. 여기서 하나 밀리면(한 틱분 초가
            // 남는 쪽으로) 다음 틱에도 계속 얼어 있는 것처럼 보인다.
            Assert.AreEqual(0f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 10, tick: 10, interval: 0.02f), 1e-4f);
        }

        [Test]
        public void 종료틱이_지금틱보다_과거면_음수_대신_0이다()
        {
            // 경계를 넘어 이미 지난 경우 — 뺄셈 결과가 음수가 나올 수 있는데, 그걸 그대로
            // 돌려주면 "음수 초만큼 얼어 있다"는 값이 새 나간다. 클램프로 막는다.
            Assert.AreEqual(0f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 5, tick: 10, interval: 0.02f), 1e-4f);
        }

        [Test]
        public void 종료틱이_0이면_안_멈춘_것이라_0이다()
        {
            // 0은 와이어 계약상 "그 상태가 아님"이다 — 과거 케이스와 같은 코드 경로를 타지만,
            // 그 사실 자체가 계약을 지킨다는 걸 보여 주려 별도로 남겨 둔다.
            Assert.AreEqual(0f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 0, tick: 10, interval: 0.02f), 1e-4f);
        }
    }
}
