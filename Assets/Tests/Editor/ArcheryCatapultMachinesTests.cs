using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryCatapultMachinesTests
    {
        [Test]
        public void 투석기는_사대_너머_양옆에_표적_길_밖으로_선다()
        {
            var right = ArcheryCatapultMachines.Position(+1, Vector3.zero, Vector3.forward, Vector3.right);
            var left = ArcheryCatapultMachines.Position(-1, Vector3.zero, Vector3.forward, Vector3.right);
            Assert.AreEqual(22f, right.x, 1e-4f);
            Assert.AreEqual(-22f, left.x, 1e-4f);
            Assert.AreEqual(44f, right.z, 1e-4f);
            Assert.Greater(Mathf.Abs(right.x), 16f, "표적 길(±16 m)을 가리면 안 된다");
        }

        [Test]
        public void 가로_위치의_부호로_어느_투석기가_던질지_정한다()
        {
            Assert.AreEqual(+1, ArcheryCatapultMachines.SideOf(3f));
            Assert.AreEqual(-1, ArcheryCatapultMachines.SideOf(-0.5f));
            Assert.AreEqual(+1, ArcheryCatapultMachines.SideOf(0f));
        }

        [Test]
        public void 팔은_준비_전엔_쉬고_발사_틱에_휘둘러져_있다()
        {
            Assert.AreEqual(ArcheryCatapultMachines.ArmRestDeg, ArcheryCatapultMachines.ArmAngle(100, 200, 235), 1e-3f);
            Assert.Less(ArcheryCatapultMachines.ArmAngle(230, 200, 235), ArcheryCatapultMachines.ArmRestDeg);   // 뒤로 젖힘
            //  발사 틱 + 휘두르기 8틱 = 꼭대기.
            Assert.AreEqual(ArcheryCatapultMachines.ArmFiredDeg, ArcheryCatapultMachines.ArmAngle(243, 200, 235), 1e-3f);
            Assert.AreEqual(ArcheryCatapultMachines.ArmRestDeg, ArcheryCatapultMachines.ArmAngle(400, 200, 235), 1e-3f);   // 돌아왔다
        }

        [Test]
        public void 날아_들어오는_길은_바구니에서_출발점까지_위로_휜다()
        {
            var bucket = new Vector3(22f, 3f, 44f);
            var origin = new Vector3(5f, 8f, 40f);
            Assert.AreEqual(bucket, ArcheryCatapultMachines.Lob(bucket, origin, 0f));
            Assert.AreEqual(origin, ArcheryCatapultMachines.Lob(bucket, origin, 1f));
            var mid = ArcheryCatapultMachines.Lob(bucket, origin, 0.5f);
            Assert.Greater(mid.y, Mathf.Max(bucket.y, origin.y));
        }
    }
}
