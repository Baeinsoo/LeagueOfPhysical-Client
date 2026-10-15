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

        //  준비 200 → 발사 235(35틱). 던지기 전체가 준비 안에서 끝나야 그림이 바구니를 떠나는 게 보인다.
        [Test]
        public void 팔은_준비_안에서_젖혔다_휘두르고_발사_뒤에_제자리로_온다()
        {
            Assert.AreEqual(ArcheryCatapultMachines.ArmRestDeg, ArcheryCatapultMachines.ArmAngle(100, 200, 235), 1e-3f);
            Assert.AreEqual(ArcheryCatapultMachines.ArmRestDeg, ArcheryCatapultMachines.ArmAngle(200, 200, 235), 1e-3f);   // 준비 시작 = 쉼
            Assert.Less(ArcheryCatapultMachines.ArmAngle(205, 200, 235), ArcheryCatapultMachines.ArmRestDeg);              // 앞 30%: 뒤로 젖힘
            float swinging = ArcheryCatapultMachines.ArmAngle(213, 200, 235);                                               // 30~45%: 휘두르는 중
            Assert.Greater(swinging, ArcheryCatapultMachines.ArmCockedDeg);
            Assert.Greater(swinging, ArcheryCatapultMachines.ArmRestDeg);
            Assert.Less(swinging, ArcheryCatapultMachines.ArmFiredDeg);
            Assert.AreEqual(ArcheryCatapultMachines.ArmFiredDeg, ArcheryCatapultMachines.ArmAngle(220, 200, 235), 1e-3f);  // 준비 가운데에 이미 다 휘둘렀다
            Assert.AreEqual(ArcheryCatapultMachines.ArmFiredDeg, ArcheryCatapultMachines.ArmAngle(235, 200, 235), 1e-3f);  // 발사 틱까지 그대로
            float settling = ArcheryCatapultMachines.ArmAngle(255, 200, 235);
            Assert.Less(settling, ArcheryCatapultMachines.ArmFiredDeg);
            Assert.Greater(settling, ArcheryCatapultMachines.ArmRestDeg);
            Assert.AreEqual(ArcheryCatapultMachines.ArmRestDeg, ArcheryCatapultMachines.ArmAngle(275, 200, 235), 1e-3f);  // 발사 + 0.8초면 돌아왔다
            Assert.AreEqual(ArcheryCatapultMachines.ArmRestDeg, ArcheryCatapultMachines.ArmAngle(400, 200, 235), 1e-3f);
        }

        [Test]
        public void 그림은_팔이_다_휘둘린_순간_떠나_발사_틱에_닿는다()
        {
            Assert.Less(ArcheryCatapultMachines.LobProgress(205, 200, 235), 0f, "젖히는 동안은 아직 바구니 안");
            Assert.Less(ArcheryCatapultMachines.LobProgress(213, 200, 235), 0f, "휘두르는 동안도 아직 안 떠났다");
            Assert.AreEqual(1f, ArcheryCatapultMachines.LobProgress(235, 200, 235), 1e-4f);
            //  처음 떠나는 틱에 팔은 이미 꼭대기다 — 그림이 팔보다 먼저 날지 않는다.
            for (long t = 200; t < 235; t++)
            {
                if (ArcheryCatapultMachines.LobProgress(t, 200, 235) >= 0f)
                {
                    Assert.AreEqual(ArcheryCatapultMachines.ArmFiredDeg, ArcheryCatapultMachines.ArmAngle(t, 200, 235), 1e-3f, t.ToString());
                    Assert.Less(ArcheryCatapultMachines.LobProgress(t, 200, 235), 0.2f, "놓자마자 출발한다");
                    return;
                }
            }
            Assert.Fail("준비 안에 떠나는 틱이 없다");
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
