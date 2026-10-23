using NUnit.Framework;

namespace LOP.Tests
{
    public class MountedArcheryHudCoordinatorTests
    {
        private static bool Decide(bool opened = false, bool spectating = false, bool gameInfo = true,
                                   bool state = true, string userEntityId = "", bool meEliminated = false)
            => MountedArcheryHudCoordinator.ShouldSpectateWithoutBody(opened, spectating, gameInfo, state, userEntityId, meEliminated);

        // 탈락 뒤 재접속: 서버가 배정을 지워 엔티티 id가 비고 몸도 안 생긴다.
        [Test]
        public void 엔티티_없이_판_상태가_오면_관전으로_간다()
        {
            Assert.IsTrue(Decide(userEntityId: ""));
            Assert.IsTrue(Decide(userEntityId: null));
        }

        [Test]
        public void 엔티티가_있어도_상태가_내_탈락을_알리면_관전으로_간다()
        {
            Assert.IsTrue(Decide(userEntityId: "e1", meEliminated: true));
        }

        // 양성 대조: 평범한 살아 있는 경로는 바뀌지 않는다.
        [Test]
        public void 살아_있으면_몸을_기다린다()
        {
            Assert.IsFalse(Decide(userEntityId: "e1", meEliminated: false));
        }

        [Test]
        public void 게임_정보나_상태가_아직_없으면_기다린다()
        {
            Assert.IsFalse(Decide(gameInfo: false));
            Assert.IsFalse(Decide(state: false));
        }

        [Test]
        public void 이미_열렸거나_관전_중이면_다시_하지_않는다()
        {
            Assert.IsFalse(Decide(opened: true));
            Assert.IsFalse(Decide(spectating: true));
        }
    }
}
