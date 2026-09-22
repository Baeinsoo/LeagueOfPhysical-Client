using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  스틱 방향이 뒤집히면 "조작이 이상하다"까지만 보이고 어디가 뒤집혔는지는 안 보인다.
    public class MoveStickDirectionTests
    {
        [Test]
        public void 카메라가_정면이면_스틱_위는_앞이다()
        {
            var v = MoveStickDirection.ToWorld(Vector2.up, 0f);

            Assert.AreEqual(0f, v.x, 1e-4f);
            Assert.AreEqual(1f, v.z, 1e-4f, "스틱을 위로 밀었는데 앞으로 안 간다");
        }

        [Test]
        public void 카메라가_정면이면_스틱_오른쪽은_오른쪽이다()
        {
            var v = MoveStickDirection.ToWorld(Vector2.right, 0f);

            Assert.AreEqual(1f, v.x, 1e-4f, "스틱을 오른쪽으로 밀었는데 오른쪽으로 안 간다");
            Assert.AreEqual(0f, v.z, 1e-4f);
        }

        //  카메라를 오른쪽으로 90도 돌리면 "앞"은 월드 +x가 된다.
        [Test]
        public void 카메라를_돌리면_앞도_같이_돈다()
        {
            var v = MoveStickDirection.ToWorld(Vector2.up, 90f);

            Assert.AreEqual(1f, v.x, 1e-4f, "카메라 기준 앞이 아니다");
            Assert.AreEqual(0f, v.z, 1e-4f);
        }

        //  뒤로 돈 카메라에서 스틱 오른쪽은 월드 왼쪽이다 — 한 축만 돌리면 여기서 걸린다.
        [Test]
        public void 카메라가_뒤를_보면_좌우도_뒤집힌다()
        {
            var v = MoveStickDirection.ToWorld(Vector2.right, 180f);

            Assert.AreEqual(-1f, v.x, 1e-4f, "카메라가 뒤를 보는데 좌우가 그대로다");
        }

        //  가운데면 0. 이 0을 그대로 밀어야 손을 뗐을 때 캐릭터가 선다.
        [Test]
        public void 가운데면_0이다()
        {
            var v = MoveStickDirection.ToWorld(Vector2.zero, 37f);

            Assert.AreEqual(0f, v.magnitude, 1e-4f);
        }
    }
}
