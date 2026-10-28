using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    // 머리 위 라벨이 보간·카메라보다 먼저 놓이면 한 프레임 늦어 떨린다 — 순서가 조용히 바뀌지 않게 못 박는다.
    public class KnockbackLateUpdateDriverTests
    {
        [Test]
        public void Driver_RunsAt3100_AfterInterpolatorAndCamera()
        {
            var attr = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(
                typeof(KnockbackLateUpdateDriver), typeof(DefaultExecutionOrder));
            Assert.NotNull(attr);
            Assert.AreEqual(3100, attr.order);
        }
    }
}
