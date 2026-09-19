using NUnit.Framework;

namespace LOP.Tests
{
    using Intent = LOP.UI.ArcheryPadViewModel.PressIntent;

    //  "누르고 가만히 있으면 활, 곧바로 끌면 시야" — 이 한 줄이 조작의 뼈대다.
    //  없으면 화면을 쓸어 둘러보려 할 때마다 활이 당겨지고, 화살이 유한한 사거리 맵에서는
    //  그게 곧 손해다.
    public class ArcheryPressIntentTests
    {
        const float Still = 0f;
        static float Slop => LOP.UI.ArcheryPadViewModel.DragSlopFraction;
        static float Hold => LOP.UI.ArcheryPadViewModel.PressHoldSeconds;

        static Intent Decide(float travel, float seconds)
            => LOP.UI.ArcheryPadViewModel.Decide(travel, seconds);

        [Test]
        public void 가만히_버티면_활이_된다()
        {
            Assert.AreEqual(Intent.Draw, Decide(Still, Hold));
            Assert.AreEqual(Intent.Draw, Decide(Still, Hold * 3f));
        }

        [Test]
        public void 곧바로_끌면_시야가_된다()
        {
            Assert.AreEqual(Intent.Look, Decide(Slop, 0f));
            Assert.AreEqual(Intent.Look, Decide(Slop * 5f, Hold * 0.5f));
        }

        //  ⭐ 순서가 핵심이다. 슬롭을 넘긴 뒤에는 **아무리 오래 눌러도** 시야여야 한다 —
        //  반대로 두면 "쓸어 둘러보다 손가락을 잠깐 멈췄는데 활이 올라오는" 일이 생긴다.
        [Test]
        public void 한번_끌었으면_그_뒤로_아무리_오래_눌러도_시야다()
        {
            Assert.AreEqual(Intent.Look, Decide(Slop * 2f, Hold * 10f),
                "움직임보다 시간이 이기고 있다 — 쓸다가 멈추면 활이 올라온다");
        }

        [Test]
        public void 아직은_정하지_않는다()
        {
            Assert.AreEqual(Intent.Undecided, Decide(Still, Hold * 0.5f));
            Assert.AreEqual(Intent.Undecided, Decide(Slop * 0.5f, Hold * 0.9f));
        }

        //  두 문턱이 서로를 무력화하면 안 된다 — 슬롭이 0이면 모든 누름이 시야가 되어
        //  활을 아예 못 들고, 홀드가 0이면 손가락이 닿는 순간 활이 올라온다.
        [Test]
        public void 두_문턱이_서로를_무력화하지_않는다()
        {
            Assert.Greater(Slop, 0f, "슬롭이 0이면 활을 아예 못 든다");
            Assert.Greater(Hold, 0f, "홀드가 0이면 닿는 순간 활이 올라온다");
            Assert.Less(Hold, 0.4f, "활이 올라오기까지 너무 오래 기다린다");
        }
    }
}
