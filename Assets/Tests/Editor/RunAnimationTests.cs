using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class RunAnimationTests
    {
        [Test]
        public void 도는_판_위에_가만히_서_있으면_달리지_않는다()
        {
            //  리뷰 6차: 몸 속도가 세계 기준이 되자 판 위에 서 있기만 해도 판 속도(초속 10m)로 달리기 애니가 돌았다.
            Assert.IsFalse(RunAnimation.ShouldRun(new Vector3(-1.2f, 0f, -10.4f), new Vector3(-1.2f, 0f, -10.4f), grounded: true));
        }

        [Test]
        public void 판_위에서_걸으면_달린다()
        {
            Assert.IsTrue(RunAnimation.ShouldRun(new Vector3(-1.2f, 0f, -6.4f), new Vector3(-1.2f, 0f, -10.4f), grounded: true));
        }

        [Test]
        public void 가만한_땅에서_걸으면_달리고_공중이면_안_달린다()
        {
            Assert.IsTrue(RunAnimation.ShouldRun(new Vector3(4f, 0f, 0f), Vector3.zero, grounded: true));
            Assert.IsFalse(RunAnimation.ShouldRun(new Vector3(4f, 0f, 0f), Vector3.zero, grounded: false));
        }
    }
}
