using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 빌드에 지연 시뮬레이터가 실려 나가지 않게 하는 벗기기 규칙. 실제 갈아타기는
    /// <c>#if !UNITY_EDITOR</c>라 에디터에서 못 돌리지만, 판단 자체는 여기서 고정한다.
    /// </summary>
    public class LOPNetworkManagerTransportTests
    {
        private GameObject host;

        [SetUp]
        public void SetUp() => host = new GameObject("transport-test");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void 지연_시뮬레이터가_감싸고_있으면_속에_있는_것을_돌려준다()
        {
            var real = host.AddComponent<kcp2k.KcpTransport>();
            var simulation = host.AddComponent<LatencySimulation>();
            simulation.wrap = real;

            Assert.AreSame(real, LOPNetworkManager.Unwrap(simulation));
        }

        [Test]
        public void 감싸는_것이_없으면_그대로_돌려준다()
        {
            var real = host.AddComponent<kcp2k.KcpTransport>();

            Assert.AreSame(real, LOPNetworkManager.Unwrap(real));
        }

        [Test]
        public void 시뮬레이터가_아무것도_안_감싸고_있으면_그대로_돌려준다()
        {
            //  씬 배선이 깨진 경우 — 여기서 null을 돌려주면 NetworkManager가 트랜스포트를 잃는다.
            var simulation = host.AddComponent<LatencySimulation>();
            simulation.wrap = null;

            Assert.AreSame(simulation, LOPNetworkManager.Unwrap(simulation));
        }
    }
}
