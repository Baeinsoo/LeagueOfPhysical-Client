using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShootOffIntroTests
    {
        [Test]
        public void 자리가_바뀌는_틱에는_카드가_화면을_다_덮고_있다()
        {
            Assert.AreEqual(1f, ArcheryShootOffIntro.AlphaAt(1000d, 1000L), 1e-5f);
            Assert.AreEqual(1f, ArcheryShootOffIntro.AlphaAt(1001.5d, 1000L), 1e-5f);   // 보간이 한 틱 늦게 그려도 덮여 있다
        }

        [Test]
        public void 카드는_조금_전에_덮이기_시작해_뒤에_걷힌다()
        {
            Assert.AreEqual(0f, ArcheryShootOffIntro.AlphaAt(990d, 1000L), 1e-5f);
            Assert.AreEqual(0.5f, ArcheryShootOffIntro.AlphaAt(996d, 1000L), 1e-5f);
            Assert.AreEqual(1f, ArcheryShootOffIntro.AlphaAt(1035d, 1000L), 1e-5f);
            Assert.AreEqual(0.5f, ArcheryShootOffIntro.AlphaAt(1042.5d, 1000L), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffIntro.AlphaAt(1050d, 1000L), 1e-5f);
        }

        [Test]
        public void 과녁이_서기_전에_카드가_다_걷힌다()
        {
            //  결과 화면이 닫히고 60틱 뒤에 과녁이 선다.
            Assert.AreEqual(0f, ArcheryShootOffIntro.AlphaAt(1060d, 1000L), 1e-5f);
        }

        [Test]
        public void 지금_어느_라운드_카드인가()
        {
            var changes = new[] { 0L, 1000L, 2000L };
            Assert.AreEqual(0, ArcheryShootOffIntro.ActiveRound(10d, changes));
            Assert.AreEqual(-1, ArcheryShootOffIntro.ActiveRound(500d, changes));
            Assert.AreEqual(1, ArcheryShootOffIntro.ActiveRound(995d, changes));
            Assert.AreEqual(2, ArcheryShootOffIntro.ActiveRound(2040d, changes));
            Assert.AreEqual(-1, ArcheryShootOffIntro.ActiveRound(3000d, changes));
        }

        [Test]
        public void 과녁_쪽_방향은_유니티_요_각도()
        {
            Assert.AreEqual(0f, ArcheryShootOffIntro.YawToward(Vector3.zero, new Vector3(0f, 1f, 20f)), 1e-3f);
            Assert.AreEqual(-4.574f, ArcheryShootOffIntro.YawToward(new Vector3(1.6f, 0f, 0f), new Vector3(0f, 1f, 20f)), 1e-2f);
        }
    }
}
