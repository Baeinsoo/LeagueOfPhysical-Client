using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Router = LOP.UI.ArcheryPointerRouter;
    using Role = LOP.UI.ArcheryPointerRouter.Role;
    using Ending = LOP.UI.ArcheryPointerRouter.Ending;

    //  활 버튼 조작: 화면을 끌면 겨누기만 하고, 활은 오른쪽 버튼을 누른 손가락만 다룬다.
    //  손가락이 둘 이상 닿아도 버튼 밖 손가락이 활을 들거나 쏘면 안 된다 — 그게 이 조작의 약속이다.
    //  취소는 왼쪽 아래에 고정된 "취소" 버튼이다 — 다른 손가락으로 누르거나, 활 손가락을 끌어와 그 위에서 뗀다.
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

            Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelButton: false),
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

            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelButton: false));
            Assert.IsFalse(router.Drawing);
        }

        [Test]
        public void 시위가_걸리기_전에_떼면_안_쏘고_내리기만_한다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Ending.Lowered, router.Up(Thumb, drawArmed: false, overCancelButton: false));
            Assert.IsFalse(router.Drawing);
        }

        //  활 손가락을 취소 버튼까지 끌어와 그 위에서 떼면 만작이어도 안 쏜다.
        [Test]
        public void 취소_버튼_위에서_떼면_만작이어도_안_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Ending.PutDown, router.Up(Thumb, drawArmed: true, overCancelButton: true));
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
            Assert.AreEqual(Ending.None, router.Up(Other, drawArmed: true, overCancelButton: false));
            Assert.IsTrue(router.Drawing, "두 번째 손가락이 첫 손가락의 활을 내렸다");
            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelButton: false));
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
            Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelButton: false));
            Assert.AreEqual(Ending.None, router.Lost(Thumb));
        }

        //  뗀 뒤 같은 id로 다시 누르면 새 누름이다 — 지난 역할이 남으면 안 된다.
        [Test]
        public void 뗀_뒤에는_같은_id라도_새로_정한다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);
            router.Up(Thumb, drawArmed: true, overCancelButton: false);

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

            Assert.AreEqual(Ending.None, router.Up(0, drawArmed: true, overCancelButton: false));
            Assert.AreEqual(Ending.Fire, router.Up(Router.KeyboardPointerId, drawArmed: true, overCancelButton: false));
        }

        //  ── 버튼 자리 ───────────────────────────────────────────────

        static readonly Vector2 Wide = new Vector2(1920f, 1080f);

        [Test]
        public void 버튼은_오른쪽_세로의_24퍼센트_지름이다()
        {
            var center = Router.BowButtonCenter(Wide, rightInset: 0f, bottomInset: 0f);

            Assert.AreEqual(1080f * 0.24f, Router.BowButtonRadius(Wide) * 2f, 0.5f);
            Assert.AreEqual(1920f * 0.78f, center.x, 0.5f, "가로 78% 자리가 아니다");
            Assert.AreEqual(1080f * 0.62f, center.y, 0.5f, "세로 62% 자리가 아니다(패널 좌표, 아래로 증가)");
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

        //  왼쪽 아래 엄지 자리 — 가로 12%·세로 74%, 지름은 세로의 18%.
        [Test]
        public void 취소_버튼은_왼쪽_아래_세로의_18퍼센트_지름이다()
        {
            var center = Router.CancelButtonCenter(Wide, leftInset: 0f, bottomInset: 0f);

            Assert.AreEqual(1080f * 0.18f, Router.CancelButtonRadius(Wide) * 2f, 0.5f);
            Assert.AreEqual(1920f * 0.12f, center.x, 0.5f, "가로 12% 자리가 아니다");
            Assert.AreEqual(1080f * 0.74f, center.y, 0.5f, "세로 74% 자리가 아니다(패널 좌표, 아래로 증가)");
        }

        //  가로로 쥐면 노치가 왼쪽에 올 수도 있다 — 그 밑으로 들어가면 못 누른다.
        [Test]
        public void 취소_버튼도_안전_영역을_비켜_선다()
        {
            float leftInset = 300f, bottomInset = 200f;
            var center = Router.CancelButtonCenter(Wide, leftInset, bottomInset);
            float r = Router.CancelButtonHitRadius(Wide);

            Assert.GreaterOrEqual(center.x - r, leftInset - 1e-3f, "왼쪽 안전 영역을 침범했다");
            Assert.LessOrEqual(center.y + r, Wide.y - bottomInset + 1e-3f, "아래 안전 영역을 침범했다");
        }

        //  두 버튼의 누르는 자리가 겹치면 한 손가락이 어느 쪽인지 갈라지지 않는다. 좁은 4:3에서도.
        [Test]
        public void 두_버튼의_누르는_자리는_겹치지_않는다()
        {
            foreach (var size in new[] { Wide, new Vector2(1440f, 1080f) })
            {
                var bow = Router.BowButtonCenter(size, 0f, 0f);
                var cancel = Router.CancelButtonCenter(size, 0f, 0f);
                Assert.Greater((bow - cancel).magnitude,
                    Router.BowButtonHitRadius(size) + Router.CancelButtonHitRadius(size), "겹친다: " + size);
            }
        }

        //  ── 취소 버튼 ───────────────────────────────────────────────

        //  오른손 엄지로 당기는 채 왼손으로 취소를 톡 친다. 누르는 순간 내려놓고,
        //  그 뒤 활 손가락을 떼도 화살이 나가면 안 된다.
        [Test]
        public void 당기는_중_다른_손가락으로_취소를_누르면_내려놓고_활_손가락을_떼도_안_쏜다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);

            Assert.AreEqual(Role.Cancel, router.Down(Other, onBowButton: false, onCancelButton: true));
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
            router.Down(Thumb, onBowButton: true);
            router.Down(Other, onBowButton: false, onCancelButton: true);

            Assert.IsFalse(router.Aims(Thumb), "취소된 활 손가락이 계속 겨눈다");
            Assert.IsFalse(router.Aims(Other), "취소 버튼을 누른 손가락이 겨눈다");
        }

        //  활을 안 들었을 땐 취소 버튼은 그냥 막힌 자리다 — 겨누지도, 활을 들지도 않는다.
        [Test]
        public void 안_당길_때_취소_버튼에서_시작한_손가락은_아무것도_안_한다()
        {
            var router = new Router();

            Assert.AreEqual(Role.Inert, router.Down(Thumb, onBowButton: false, onCancelButton: true));
            Assert.IsFalse(router.Aims(Thumb), "취소 버튼에서 시작한 손가락이 겨눈다");
            Assert.IsFalse(router.Drawing);
            Assert.AreEqual(Ending.None, router.Up(Thumb, drawArmed: true, overCancelButton: true));
            Assert.AreEqual(Ending.None, router.Lost(Thumb));
        }

        //  취소한 뒤 활 버튼을 다시 누르면 새로 든다.
        [Test]
        public void 취소한_뒤_다시_누르면_새로_든다()
        {
            var router = new Router();
            router.Down(Thumb, onBowButton: true);
            router.Down(Other, onBowButton: false, onCancelButton: true);
            router.Up(Thumb, drawArmed: true, overCancelButton: false);
            router.Up(Other, drawArmed: true, overCancelButton: true);

            Assert.AreEqual(Role.Bow, router.Down(Thumb, onBowButton: true));
            Assert.AreEqual(Ending.Fire, router.Up(Thumb, drawArmed: true, overCancelButton: false));
        }

        //  에디터: 스페이스로 든 활을 Escape로 내려놓는다. 스페이스를 떼도 안 쏜다.
        [Test]
        public void Escape는_스페이스로_든_활을_안_쏘고_내려놓는다()
        {
            var router = new Router();
            router.Down(Router.KeyboardPointerId, onBowButton: true);

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
    }
}
