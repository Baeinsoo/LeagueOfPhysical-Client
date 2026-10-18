using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Router = LOP.UI.ArcheryPointerRouter;
    using Role = LOP.UI.ArcheryPointerRouter.Role;
    using Ending = LOP.UI.ArcheryPointerRouter.Ending;

    //  활 버튼 조작: 화면을 끌면 겨누기만 하고, 활은 오른쪽 아래 버튼을 누른 손가락만 다룬다.
    //  손가락이 둘 이상 닿아도 버튼 밖 손가락이 활을 들거나 쏘면 안 된다 — 그게 이 조작의 약속이다.
    public class ArcheryPointerRouterTests
    {
        const int Thumb = 1;
        const int Other = 2;

        [Test]
        public void 버튼_밖에서_누르면_겨누기만_하고_활은_안_든다()
        {
            var router = new Router();

            Assert.AreEqual(Role.Look, router.Down(Thumb, onBowButton: false));
            Assert.IsFalse(router.Drawing, "버튼 밖 손가락이 활을 들었다");
            Assert.IsTrue(router.Aims(Thumb), "버튼 밖 손가락이 겨누지 못한다");
        }

        //  시위가 걸려 있어도(다른 손가락이 당기는 중이어도) 버튼 밖 손가락을 떼는 것은 발사가 아니다.
        [Test]
        public void 버튼_밖_손가락을_떼면_절대_안_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);
            router.Down(Other, onBowButton: false);

            Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelTarget: false),
                "겨누던 손가락을 뗐더니 화살이 나갔다");
            Assert.IsTrue(router.Drawing, "겨누던 손가락을 뗐더니 활이 내려갔다");
        }

        [Test]
        public void 버튼을_누르면_활을_들고_그_손가락도_겨눈다()
        {
            var router = new Router();

            Assert.AreEqual(Role.Bow, router.Down(Thumb, onBowButton: true));
            Assert.IsTrue(router.Drawing);
            Assert.IsTrue(router.Aims(Thumb), "버튼을 쥔 손가락을 끌어도 안 겨눈다");
        }

        [Test]
        public void 시위가_걸린_채_버튼_손가락을_떼면_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelTarget: false));
            Assert.IsFalse(router.Drawing);
        }

        [Test]
        public void 시위가_걸리기_전에_떼면_안_쏘고_내리기만_한다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Ending.Lowered, router.Up(Thumb, drawArmed: false, overCancelTarget: false));
            Assert.IsFalse(router.Drawing);
        }

        //  만작이어도 내려놓기 원 안에서 떼면 안 쏜다.
        [Test]
        public void 내려놓기_원_안에서_떼면_만작이어도_안_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Ending.PutDown, router.Up(Thumb, drawArmed: true, overCancelTarget: true));
            Assert.IsFalse(router.Drawing);
        }

        //  OS가 터치를 가져가면(알림·전화) 쏘겠다는 뜻이 아니다.
        [Test]
        public void 버튼_손가락을_뺏기면_안_쏘고_내려놓는다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Ending.PutDown, router.Lost(Thumb));
            Assert.IsFalse(router.Drawing);
        }

        [Test]
        public void 겨누던_손가락을_뺏겨도_아무_일_없다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);
            router.Down(Other, onBowButton: false);

            Assert.AreEqual(Ending.None, router.Lost(Other));
            Assert.IsTrue(router.Drawing);
        }

        //  두 번째 손가락이 버튼에 닿아도 첫 손가락의 당김을 가로채지 않는다 — 겨누기만 한다.
        [Test]
        public void 이미_당기는_중에_버튼을_또_누르면_그_손가락은_겨누기만_한다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Role.Look, router.Down(Other, onBowButton: true));
            Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelTarget: false));
            Assert.IsTrue(router.Drawing, "두 번째 손가락이 첫 손가락의 활을 내렸다");
            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelTarget: false));
        }

        //  한 손가락은 버튼, 다른 손가락은 화면 — 둘 다 겨눌 수 있다.
        [Test]
        public void 버튼을_쥔_채_다른_손가락으로_겨눌_수_있다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);
            router.Down(Other, onBowButton: false);

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
            Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelTarget: false));
            Assert.AreEqual(Ending.None, router.Lost(Thumb));
        }

        //  뗀 뒤 같은 id로 다시 누르면 새 누름이다 — 지난 역할이 남으면 안 된다.
        [Test]
        public void 뗀_뒤에는_같은_id라도_새로_정한다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);
            router.Up(Thumb, drawArmed: true, overCancelTarget: false);

            Assert.AreEqual(Role.Look, router.Down(Thumb, onBowButton: false));
            Assert.IsFalse(router.Drawing);
        }

        //  데스크톱: 스페이스를 누르고 있는 동안 당기고, 마우스는 화면을 끌어 겨눈다.
        [Test]
        public void 스페이스는_버튼과_같고_마우스와_따로_논다()
        {
            var router = new Router();
            router.Down(Router.KeyboardPointerId, onBowButton: true);
            router.Down(0, onBowButton: false);

            Assert.AreEqual(Ending.None, router.Up(0, drawArmed: true, overCancelTarget: false));
            Assert.AreEqual(Ending.Fire, router.Up(Router.KeyboardPointerId, drawArmed: true, overCancelTarget: false));
        }

        //  ── 버튼 자리 ───────────────────────────────────────────────

        static readonly Vector2 Wide = new Vector2(1920f, 1080f);

        [Test]
        public void 버튼은_오른쪽_아래_화면_세로의_24퍼센트_지름이다()
        {
            var center = Router.BowButtonCenter(Wide, rightInset: 0f, bottomInset: 0f);

            Assert.AreEqual(1080f * 0.24f, Router.BowButtonRadius(Wide) * 2f, 0.5f);
            Assert.AreEqual(1920f * 0.86f, center.x, 0.5f, "가로 86% 자리가 아니다");
            Assert.AreEqual(1080f * 0.74f, center.y, 0.5f, "세로 74% 자리가 아니다(패널 좌표, 아래로 증가)");
        }

        //  노치·홈 막대 아래로 버튼이 들어가면 엄지가 못 누른다.
        [Test]
        public void 안전_영역을_비켜_선다()
        {
            float rightInset = 140f, bottomInset = 90f;
            var center = Router.BowButtonCenter(Wide, rightInset, bottomInset);
            float r = Router.BowButtonHitRadius(Wide);

            Assert.LessOrEqual(center.x + r, Wide.x - rightInset + 1e-3f, "오른쪽 안전 영역을 침범했다");
            Assert.LessOrEqual(center.y + r, Wide.y - bottomInset + 1e-3f, "아래 안전 영역을 침범했다");
        }

        [Test]
        public void 누르는_자리는_보이는_원보다_조금_넉넉하다()
        {
            Assert.Greater(Router.BowButtonHitRadius(Wide), Router.BowButtonRadius(Wide));
            Assert.Less(Router.BowButtonHitRadius(Wide), Router.BowButtonRadius(Wide) * 1.3f);
        }

        //  버튼을 누른 자리 기준으로 내려놓기 원이 화면 안에 서야 한다. 버튼이 화면 아래쪽이라
        //  아래로는 자리가 없으니 왼쪽에 선다(위로 서면 오른손 엄지가 화면을 가리며 올라가야 한다).
        [Test]
        public void 버튼_자리에서_누르면_내려놓기_원은_왼쪽_화면_안에_선다()
        {
            var center = Router.BowButtonCenter(Wide, 0f, 0f);
            //  패널 좌표(아래로 증가) → 세로를 1로 본 좌표(위가 양수)
            var press = new Vector2(center.x / Wide.y, (Wide.y - center.y) / Wide.y);
            float width = Wide.x / Wide.y;
            var target = LOP.UI.ArcheryPadViewModel.CancelTargetCenter(press, width);
            float r = LOP.UI.ArcheryPadViewModel.CancelRadiusInHeights;

            Assert.Less(target.x, press.x, "내려놓기 원이 버튼 왼쪽이 아니다");
            Assert.LessOrEqual(target.y, press.y + 1e-4f, "내려놓기 원이 버튼보다 위로 올라갔다");
            Assert.GreaterOrEqual(target.y, r - 1e-4f, "원이 화면 아래로 나갔다");
            Assert.GreaterOrEqual((target - press).magnitude,
                LOP.UI.ArcheryPadViewModel.CancelBelowPressInHeights - 1e-4f, "원이 버튼에 너무 붙었다");
        }
    }
}
