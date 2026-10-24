using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 말 탄 몸의 이동용 캡슐은 땅·바위와 같은 Default 층에 있다. 서버는 "지형이 먼저 막았나"를 잴 때 몸을 뺀다 —
    /// 클라도 빼야 화살이 상대 판정 구에 닿기 전에 더 큰 캡슐을 "땅"으로 만나 막히지 않는다.
    /// </summary>
    public class MountedTerrainTests
    {
        private GameObject wall;
        private GameObject actor;

        [SetUp]
        public void SetUp()
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0f, 10f);

            actor = new GameObject("Actor");
            actor.AddComponent<LOPActor>();
            var body = new GameObject("Body");
            body.transform.SetParent(actor.transform, false);
            body.AddComponent<CapsuleCollider>().radius = 0.8f;
            actor.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(actor);
        }

        [Test]
        public void 몸은_건너뛰고_뒤의_벽에_닿는다()
        {
            //  대조: 그냥 재면 몸 캡슐에 먼저 걸린다 — 이 시험이 실제로 몸을 지나는지 확인.
            Assert.IsTrue(Physics.Linecast(Vector3.zero, new Vector3(0f, 0f, 20f), out RaycastHit plain));
            Assert.Less(plain.point.z, 5f);

            Assert.IsTrue(MountedTerrain.Linecast(Vector3.zero, new Vector3(0f, 0f, 20f), out RaycastHit hit));
            Assert.AreEqual(9.5f, hit.point.z, 1e-3f);
        }

        [Test]
        public void 레이도_몸은_건너뛴다()
        {
            Assert.IsTrue(MountedTerrain.Raycast(Vector3.zero, Vector3.forward, 20f, out RaycastHit hit));
            Assert.AreEqual(9.5f, hit.point.z, 1e-3f);
        }

        [Test]
        public void 몸만_있으면_안_닿는다()
        {
            Assert.IsFalse(MountedTerrain.Linecast(Vector3.zero, new Vector3(0f, 0f, 8f), out _));
        }
    }
}
