using NUnit.Framework;

namespace LOP.Tests
{
    using Role = LOP.UI.ArcheryPointerRole;

    //  두 손 조작의 뼈대 — 왼손은 겨누기만, 오른손은 당기고 쏜다.
    //  손가락마다 **처음 닿은 쪽**으로 역할이 정해지고 뗄 때까지 안 바뀐다.
    //  이게 깨지면 왼손을 떼는 순간 오른손이 잡고 있던 화살이 나간다.
    public class ArcheryPadPointerRouterTests
    {
        const float Width = 1920f;
        const float Left = 300f;
        const float Right = 1500f;

        [Test]
        public void 왼쪽_절반에서_누르면_겨누기다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            Assert.AreEqual(Role.Aim, router.Down(1, Left, Width));
        }

        [Test]
        public void 오른쪽_절반에서_누르면_당기기다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            Assert.AreEqual(Role.Draw, router.Down(1, Right, Width));
        }

        //  ⭐ 이 조작의 핵심 계약. 왼손을 떼는 것은 절대 발사가 아니다.
        [Test]
        public void 왼손을_떼도_당기는_손은_그대로다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            router.Down(7, Right, Width);   // 오른손 엄지가 당긴다
            router.Down(3, Left, Width);    // 왼손 엄지가 겨눈다

            Assert.AreEqual(Role.Aim, router.Up(3), "왼손의 뗌이 당기기로 읽혔다 — 화살이 나간다");
            Assert.AreEqual(Role.Draw, router.RoleOf(7), "왼손을 뗐더니 오른손의 당김이 풀렸다");
        }

        [Test]
        public void 왼손이_먼저_닿아도_오른손이_당긴다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            Assert.AreEqual(Role.Aim, router.Down(3, Left, Width));
            Assert.AreEqual(Role.Draw, router.Down(7, Right, Width));
            Assert.AreEqual(Role.Draw, router.Up(7));
            Assert.AreEqual(Role.Aim, router.RoleOf(3));
        }

        //  손가락이 반대편으로 넘어가도 역할은 처음 닿은 쪽이 정한다 — 겨누다 화면 가운데를 넘었다고
        //  활이 올라오면 안 되고, 당기다 넘었다고 겨누기가 되면 안 된다. 라우터는 누를 때만 쪽을 본다.
        [Test]
        public void 역할은_뗄_때까지_안_바뀐다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            router.Down(3, Left, Width);
            Assert.AreEqual(Role.Aim, router.RoleOf(3));
            Assert.AreEqual(Role.Aim, router.Up(3));
            Assert.AreEqual(Role.None, router.RoleOf(3), "뗀 손가락을 아직 기억한다");
        }

        //  같은 쪽 두 번째 손가락은 첫 손가락을 가로채지 못한다(옛 화면의 _drawPointerId와 같은 규칙).
        [Test]
        public void 같은_쪽_두번째_손가락은_무시된다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            router.Down(1, Right, Width);
            Assert.AreEqual(Role.None, router.Down(2, Right + 100f, Width));
            Assert.AreEqual(Role.None, router.Up(2), "무시한 손가락의 뗌이 발사 판정으로 간다");
            Assert.AreEqual(Role.Draw, router.RoleOf(1));

            router.Down(5, Left, Width);
            Assert.AreEqual(Role.None, router.Down(6, Left, Width));
            Assert.AreEqual(Role.Aim, router.RoleOf(5));
        }

        [Test]
        public void 손가락을_떼면_그_쪽을_다시_쓸_수_있다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            router.Down(1, Right, Width);
            router.Up(1);
            Assert.AreEqual(Role.Draw, router.Down(2, Right, Width));
        }

        //  레이아웃이 아직 안 잡힌 프레임(폭 0)에 닿은 손가락은 아무 역할도 안 준다 —
        //  어느 쪽인지 모르는데 당기기로 치면 왼손이 활을 든다.
        [Test]
        public void 화면_폭을_모르면_아무_역할도_없다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            Assert.AreEqual(Role.None, router.Down(1, 10f, 0f));
        }

        [Test]
        public void 화면_한가운데는_오른쪽이다()
        {
            Assert.IsTrue(LOP.UI.ArcheryPointerRouter.IsLeftHalf(Width * 0.5f - 1f, Width));
            Assert.IsFalse(LOP.UI.ArcheryPointerRouter.IsLeftHalf(Width * 0.5f, Width));
        }

        //  모르는 손가락(누름을 못 본 것)의 뗌은 아무 일도 아니다.
        [Test]
        public void 모르는_손가락의_뗌은_아무것도_아니다()
        {
            var router = new LOP.UI.ArcheryPointerRouter();
            Assert.AreEqual(Role.None, router.Up(42));
        }
    }
}
