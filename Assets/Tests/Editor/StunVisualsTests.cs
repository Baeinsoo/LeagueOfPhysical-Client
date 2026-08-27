using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 스턴/무적 값이 화면 상태로 어떻게 정리되는지 고정한다. 이게 흐트러지면 내 새와 남의 새가
    /// 서로 다르게 보이는데, 그건 두 클라를 동시에 띄워야만 눈에 띈다.
    /// </summary>
    public class StunVisualsTests
    {
        [Test]
        public void 스냅에_아무것도_없으면_평소다()
        {
            Assert.AreEqual(StunVisual.None, StunVisuals.Of(new EntitySnap(), 0));
        }

        [Test]
        public void 스냅의_멈춤은_정지로_보인다()
        {
            Assert.AreEqual(StunVisual.Stunned, StunVisuals.Of(new EntitySnap { stunEndTick = 100 }, 50));
        }

        [Test]
        public void 종료틱이_지났으면_평소다()
        {
            Assert.AreEqual(StunVisual.None, StunVisuals.Of(new EntitySnap { stunEndTick = 100 }, 100));
        }

        [Test]
        public void 스냅의_무적은_깜빡임으로_보인다()
        {
            Assert.AreEqual(StunVisual.Invulnerable, StunVisuals.Of(new EntitySnap { invulnEndTick = 100 }, 50));
        }

        [Test]
        public void 둘_다_남아_있으면_멈춤이_이긴다()
        {
            var snap = new EntitySnap { stunEndTick = 100, invulnEndTick = 120 };

            Assert.AreEqual(StunVisual.Stunned, StunVisuals.Of(snap, 50));
        }

        [Test]
        public void 내_새와_남의_새가_같은_규칙으로_보인다()
        {
            //  같은 상황을 로컬 컴포넌트로 읽었을 때와 스냅으로 읽었을 때가 갈리면,
            //  내 화면의 내 새와 남의 화면의 내 새가 다르게 보인다.
            var stunned = new FlappyStun { StunRemaining = 0.5f };
            var invuln = new FlappyStun { InvulnRemaining = 0.5f };

            Assert.AreEqual(StunVisuals.Of(new EntitySnap { stunEndTick = 100 }, 0), StunVisuals.Of(stunned));
            Assert.AreEqual(StunVisuals.Of(new EntitySnap { invulnEndTick = 100 }, 0), StunVisuals.Of(invuln));
            Assert.AreEqual(StunVisuals.Of(new EntitySnap(), 0), StunVisuals.Of(new FlappyStun()));
        }

        [Test]
        public void 스턴이_없는_캐릭터는_평소다()
        {
            //  FlapWang처럼 FlappyStun을 안 붙이는 캐릭터는 Get이 null을 준다.
            Assert.AreEqual(StunVisual.None, StunVisuals.Of((FlappyStun)null));
        }
    }
}
