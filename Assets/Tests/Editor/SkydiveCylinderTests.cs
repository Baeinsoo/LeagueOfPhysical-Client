using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;
using Y = LOP.EditorTools.SkydiveCylinderLayout;

namespace LOP.Tests
{
    /// <summary>원통 시제품 — 큰 원통을 쭉 내려가며 움직이는 대형 장애물을 돌파하거나 기다린다(사용자 10-07).</summary>
    public class SkydiveCylinderTests
    {
        [Test]
        public void 굽기_검사를_통과한다()
        {
            Assert.IsNull(SkydiveCylinderBuilder.Verify());
        }

        [Test]
        public void 장애물은_세_종류가_다_있다()
        {
            Assert.GreaterOrEqual(Y.Discs.Length, 1, "도는 원판");
            Assert.GreaterOrEqual(Y.Irises.Length, 1, "조리개");
            Assert.GreaterOrEqual(Y.Windmills.Length, 1, "풍차 날개");
        }

        [Test]
        public void 세이브는_벽_선반_몇_개뿐이고_자동_체크포인트는_출발_하나다()
        {
            Assert.LessOrEqual(Y.Ledges.Length, 3, "적게 — 전략적으로 쓰게");
            CollectionAssert.AreEquivalent(new[] { Y.SpawnY }, Y.RespawnPoints.Keys.ToArray());
            foreach (var l in Y.Ledges)
            {
                float r = new Vector2(l.PadCenter.x, l.PadCenter.z).magnitude;
                Assert.Greater(r, Y.Radius - l.Depth, $"{l.Label}은 벽에 붙은 좁은 턱이어야");
            }
        }

        [Test]
        public void 장애물_윗면에는_판과_같이_도는_격자가_있다()
        {
            //  단색 큰 판은 가까워져도 거리가 안 읽힌다(사용자 10-07) — 칸이 커지는 게 보여야 한다.
            //  도는 판이라 월드 좌표 격자면 판만 돌고 격자는 멈춰 있다 → 메시 UV(미터) 기준.
            foreach (var name in new[] { "HazardDisc", "HazardIris", "HazardMill" })
            {
                var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/Pyramid/Pyramid{name}.mat");
                Assert.IsNotNull(m, name);
                Assert.Greater(m.GetFloat("_TopGrid"), 0f, $"{name} 격자");
                Assert.AreEqual(1f, m.GetFloat("_TopGridSpace"), $"{name} 격자는 메시 기준");
            }
        }

        [Test]
        public void 날개_메시의_UV는_미터라_격자가_날개를_따라간다()
        {
            var mesh = SkydiveCylinderBuilder.BarMesh("CylTestBar", new Vector3(10f, 2f, 4f), save: false);
            var uv = mesh.uv;
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.AreEqual(v[i].x, uv[i].x, 0.001f);
                Assert.AreEqual(v[i].z, uv[i].y, 0.001f);
            }
            Assert.AreEqual(10f, mesh.bounds.size.x, 0.001f);
        }

        [Test]
        public void 부채꼴_메시는_반지름과_두께가_맞다()
        {
            var mesh = SkydiveCylinderBuilder.Sector("CylTestSector", 10f, 20f, 0f, 90f, Vector3.zero, save: false);
            var b = mesh.bounds;
            Assert.AreEqual(Y.Thickness, b.size.y, 0.01f);
            Assert.AreEqual(20f, b.max.x, 0.05f);
            Assert.AreEqual(20f, b.max.z, 0.05f);
            Assert.AreEqual(0f, b.min.x, 0.05f, "90°까지면 x는 0 아래로 안 간다");
            //  면은 위를 본다 — 위에서 내려다본 삼각형이 앞면이어야 보인다
            var tris = mesh.triangles;
            var v = mesh.vertices;
            Vector3 normal = Vector3.Cross(v[tris[1]] - v[tris[0]], v[tris[2]] - v[tris[0]]);
            Assert.Greater(normal.y, 0f);
        }
    }
}
