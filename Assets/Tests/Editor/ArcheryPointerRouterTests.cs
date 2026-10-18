using NUnit.Framework;

namespace LOP.Tests
{
    using Router = LOP.UI.ArcheryPointerRouter;
    using Role = LOP.UI.ArcheryPointerRouter.Role;
    using Ending = LOP.UI.ArcheryPointerRouter.Ending;
    using Hit = LOP.UI.ArcheryButtonLayout.Hit;

    //  활 버튼 조작: 화면을 끌면 겨누기만 하고, 활은 오른쪽 버튼을 누른 손가락만 다룬다.
    //  손가락이 둘 이상 닿아도 버튼 밖 손가락이 활을 들거나 쏘면 안 된다 — 그게 이 조작의 약속이다.
    //  취소는 왼쪽 아래 "시위 풀기" 버튼이다 — 다른 손가락으로 누르거나, 활 손가락을 끌어와 그 위에서 뗀다.
    public class ArcheryPointerRouterTests
    {
        const int Thumb = 1;
        const int Other = 2;

        [Test]
        public void 버튼_밖에서_누르면_겨누기만_하고_활은_안_든다()
        {
            var router = new Router();

            Assert.AreEqual(Role.Look, router.Down(Thumb, Hit.None));
            Assert.IsFalse(router.Drawing, "버튼 밖 손가락이 활을 들었다");
            Assert.IsTrue(router.Aims(Thumb), "버튼 밖 손가락이 겨누지 못한다");
        }

        //  시위가 걸려 있어도(다른 손가락이 당기는 중이어도) 버튼 밖 손가락을 떼는 것은 발사가 아니다.
        [Test]
        public void 버튼_밖_손가락을_떼면_절대_안_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);
            router.Down(Other, Hit.None);

            Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelButton: false),
                "겨누던 손가락을 뗐더니 화살이 나갔다");
            Assert.IsTrue(router.Drawing, "겨누던 손가락을 뗐더니 활이 내려갔다");
        }

        [Test]
        public void 버튼을_누르면_활을_들고_그_손가락도_겨눈다()
        {
            var router = new Router();

            Assert.AreEqual(Role.Bow, router.Down(Thumb, Hit.Bow));
            Assert.IsTrue(router.Drawing);
            Assert.IsTrue(router.Aims(Thumb), "버튼을 쥔 손가락을 끌어도 안 겨눈다");
        }

        [Test]
        public void 시위가_걸린_채_버튼_손가락을_떼면_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);

            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelButton: false));
            Assert.IsFalse(router.Drawing);
        }

        [Test]
        public void 시위가_걸리기_전에_떼면_안_쏘고_내리기만_한다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);

            Assert.AreEqual(Ending.Lowered, router.Up(Thumb, drawArmed: false, overCancelButton: false));
            Assert.IsFalse(router.Drawing);
        }

        //  활 손가락을 취소 버튼까지 끌어와 그 위에서 떼면 만작이어도 안 쏜다.
        [Test]
        public void 취소_버튼_위에서_떼면_만작이어도_안_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);

            Assert.AreEqual(Ending.PutDown, router.Up(Thumb, drawArmed: true, overCancelButton: true));
            Assert.IsFalse(router.Drawing);
        }

        //  OS가 터치를 가져가면(알림·전화) 쏘겠다는 뜻이 아니다.
        [Test]
        public void 버튼_손가락을_뺏기면_안_쏘고_내려놓는다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);

            Assert.AreEqual(Ending.PutDown, router.Lost(Thumb));
            Assert.IsFalse(router.Drawing);
        }

        [Test]
        public void 겨누던_손가락을_뺏겨도_아무_일_없다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);
            router.Down(Other, Hit.None);

            Assert.AreEqual(Ending.None, router.Lost(Other));
            Assert.IsTrue(router.Drawing);
        }

        //  두 번째 손가락이 버튼에 닿아도 첫 손가락의 당김을 가로채지 않는다 — 겨누기만 한다.
        [Test]
        public void 이미_당기는_중에_버튼을_또_누르면_그_손가락은_겨누기만_한다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);

            Assert.AreEqual(Role.Look, router.Down(Other, Hit.Bow));
            Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelButton: false));
            Assert.IsTrue(router.Drawing, "두 번째 손가락이 첫 손가락의 활을 내렸다");
            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelButton: false));
        }

        //  한 손가락은 버튼, 다른 손가락은 화면 — 둘 다 겨눌 수 있다.
        [Test]
        public void 버튼을_쥔_채_다른_손가락으로_겨눌_수_있다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);
            router.Down(Other, Hit.None);

            Assert.IsTrue(router.Aims(Thumb));
            Assert.IsTrue(router.Aims(Other));
            Assert.AreEqual(Role.Bow, router.RoleOf(Thumb));
            Assert.AreEqual(Role.Look, router.RoleOf(Other));
        }

        [Test]
        public void 모르는_손가락은_아무것도_안_한다()
        {
            var router = new Router();

            Assert.IsFalse(router.Aims(Thumb));
            Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelButton: false));
            Assert.AreEqual(Ending.None, router.Lost(Thumb));
        }

        //  뗀 뒤 같은 id로 다시 누르면 새 누름이다 — 지난 역할이 남으면 안 된다.
        [Test]
        public void 뗀_뒤에는_같은_id라도_새로_정한다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);
            router.Up(Thumb, drawArmed: true, overCancelButton: false);

            Assert.AreEqual(Role.Look, router.Down(Thumb, Hit.None));
            Assert.IsFalse(router.Drawing);
        }

        //  데스크톱: 스페이스를 누르고 있는 동안 당기고, 마우스는 화면을 끌어 겨눈다.
        [Test]
        public void 스페이스는_버튼과_같고_마우스와_따로_논다()
        {
            var router = new Router();
            router.Down(Router.KeyboardPointerId, Hit.Bow);
            router.Down(0, Hit.None);

            Assert.AreEqual(Ending.None, router.Up(0, drawArmed: true, overCancelButton: false));
            Assert.AreEqual(Ending.Fire, router.Up(Router.KeyboardPointerId, drawArmed: true, overCancelButton: false));
        }

        //  ── 취소 버튼 ───────────────────────────────────────────────

        //  오른손 엄지로 당기는 채 왼손으로 취소를 톡 친다. 누르는 순간 내려놓고,
        //  그 뒤 활 손가락을 떼도 화살이 나가면 안 된다.
        [Test]
        public void 당기는_중_다른_손가락으로_취소를_누르면_내려놓고_활_손가락을_떼도_안_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);

            Assert.AreEqual(Role.Cancel, router.Down(Other, Hit.Cancel));
            Assert.IsFalse(router.Drawing, "취소를 눌렀는데 활이 그대로다");
            Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelButton: false),
                "취소한 뒤 활 손가락을 뗐더니 화살이 나갔다");
            Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelButton: true));
        }

        //  취소한 활 손가락은 더는 겨누지 않는다 — 조준이 활을 들기 전 자리로 돌아가는 중이다.
        [Test]
        public void 취소한_뒤_활_손가락은_겨누지_않는다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);
            router.Down(Other, Hit.Cancel);

            Assert.IsFalse(router.Aims(Thumb), "취소된 활 손가락이 계속 겨눈다");
            Assert.IsFalse(router.Aims(Other), "취소 버튼을 누른 손가락이 겨눈다");
        }

        //  활을 안 들었을 땐 취소 버튼이 안 보인다(View가 display:none) — 그 자리에서 시작한
        //  손가락은 평범한 화면 손가락과 같다. Hit.Cancel로 들어와도 무시한다.
        [Test]
        public void 안_당길_때_취소_버튼에서_시작한_손가락은_화면처럼_겨눈다()
        {
            var router = new Router();

            Assert.AreEqual(Role.Look, router.Down(Thumb, Hit.Cancel));
            Assert.IsTrue(router.Aims(Thumb), "취소 버튼 자리에서 시작한 손가락이 겨누지 못한다");
            Assert.IsFalse(router.Drawing);
            Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelButton: true),
                "겨누기만 하던 손가락을 뗐는데 화살이 나갔다");
            Assert.AreEqual(Ending.None, router.Lost(Thumb));
        }

        //  취소한 뒤 활 버튼을 다시 누르면 새로 든다.
        [Test]
        public void 취소한_뒤_다시_누르면_새로_든다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.Bow);
            router.Down(Other, Hit.Cancel);
            router.Up(Thumb, drawArmed: true, overCancelButton: false);
            router.Up(Other, drawArmed: true, overCancelButton: true);

            Assert.AreEqual(Role.Bow, router.Down(Thumb, Hit.Bow));
            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelButton: false));
        }

        //  에디터: 스페이스로 든 활을 Escape로 내려놓는다. 스페이스를 떼도 안 쏜다.
        [Test]
        public void Escape는_스페이스로_든_활을_안_쏘고_내려놓는다()
        {
            var router = new Router();
            router.Down(Router.KeyboardPointerId, Hit.Bow);

            Assert.AreEqual(Ending.PutDown, router.Cancel());
            Assert.IsFalse(router.Drawing);
            Assert.AreEqual(Ending.None, router.Up(Router.KeyboardPointerId, drawArmed: true, overCancelButton: false));
        }

        [Test]
        public void 안_당길_때_취소하면_아무_일_없다()
        {
            var router = new Router();

            Assert.AreEqual(Ending.None, router.Cancel());
        }
        //  ── 손잡이 ─────────────────────────────────────────────────
        //  손잡이는 버튼 자리를 옮기는 데만 쓴다. 그 손가락은 겨누지도, 활을 들지도, 쏘지도, 내려놓지도 않는다.

        [Test]
        public void 손잡이_손가락은_겨누지도_들지도_쏘지도_않는다()
        {
            foreach (var handle in new[] { Hit.BowHandle, Hit.CancelHandle })
            {
                var router = new Router();

                Assert.AreEqual(Role.Handle, router.Down(Thumb, handle), handle.ToString());
                Assert.IsFalse(router.Drawing, handle + " 활을 들었다");
                Assert.IsFalse(router.Aims(Thumb), handle + " 겨눈다");
                Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelButton: false), handle + " 뗐더니 일이 났다");
            }
        }

        [Test]
        public void 손잡이_손가락을_시위_풀기_위에서_떼거나_뺏겨도_아무_일_없다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.CancelHandle);
            Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelButton: true));

            router.Down(Other, Hit.BowHandle);
            Assert.AreEqual(Ending.None, router.Lost(Other));
        }

        //  당기는 중엔 손잡이를 무시한다 — 시위 풀기 손잡이를 눌러도 활이 안 내려가고, 활 손가락은 그대로 쏜다.
        [Test]
        public void 당기는_중_손잡이를_누르면_무시한다()
        {
            foreach (var handle in new[] { Hit.BowHandle, Hit.CancelHandle })
            {
                var router = new Router();
                router.Down(Thumb, Hit.Bow);

                Assert.AreEqual(Role.Inert, router.Down(Other, handle), handle.ToString());
                Assert.IsTrue(router.Drawing, handle + " 손잡이가 활을 내렸다");
                Assert.IsFalse(router.Aims(Other), handle + " 손잡이 손가락이 겨눈다");
                Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelButton: true));
                Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelButton: false), handle + " 활이 안 나갔다");
            }
        }

        //  손잡이를 쥔 채 다른 손가락으로 활을 들면 손잡이 손가락은 거기서 멈춘다(더 안 옮긴다).
        [Test]
        public void 손잡이를_쥔_채_활을_들면_손잡이_손가락은_멈춘다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.BowHandle);

            Assert.AreEqual(Role.Bow, router.Down(Other, Hit.Bow));
            Assert.AreEqual(Role.Inert, router.RoleOf(Thumb));
            Assert.AreEqual(Ending.Fire, router.Up(Other, drawArmed: true, overCancelButton: false));
        }

        //  손잡이는 한 번에 하나만 옮긴다.
        [Test]
        public void 손잡이는_한_번에_하나만_잡힌다()
        {
            var router = new Router();
            router.Down(Thumb, Hit.BowHandle);

            Assert.AreEqual(Role.Inert, router.Down(Other, Hit.CancelHandle));
            Assert.AreEqual(Role.Handle, router.RoleOf(Thumb));
        }
    }
}
