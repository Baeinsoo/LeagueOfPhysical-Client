using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public readonly struct SkydiveIsland
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly int Trees;

        public SkydiveIsland(Vector3 center, float radius, int trees)
        {
            Center = center;
            Radius = radius;
            Trees = trees;
        }
    }

    /// <summary>스카이다이브 젤다풍 풍경 배치 — 고정값·고정 시드라 모든 클라에서 같다. 판정과 무관(충돌체 없음).</summary>
    public static class SkydiveSceneryLayout
    {
        public const float LandingRadius = 130f;

        public static List<SkydiveIsland> IslandSpots()
        {
            //  (각도°, 수평 거리, 높이, 반지름, 나무) — 여러 방향·높이로 흩어 떨어지는 내내 원경이 보이게.
            var spots = new (float deg, float dist, float y, float r, int trees)[]
            {
                (20f, 320f, 2600f, 45f, 2), (95f, 520f, 2200f, 70f, 3), (160f, 280f, 1750f, 30f, 1),
                (215f, 640f, 1300f, 60f, 3), (275f, 380f, 900f, 40f, 2), (330f, 700f, 500f, 65f, 3), (60f, 450f, 350f, 25f, 0),
            };
            var list = new List<SkydiveIsland>();
            foreach (var s in spots)
            {
                float a = s.deg * Mathf.Deg2Rad;
                list.Add(new SkydiveIsland(new Vector3(Mathf.Sin(a) * s.dist, s.y, Mathf.Cos(a) * s.dist), s.r, s.trees));
            }
            return list;
        }

        public static List<Vector4> CloudSpots()
        {
            var rng = new System.Random(20260930);
            var list = new List<Vector4>();
            for (int k = 0; k < 17; k++)
            {
                float y = 2900f - 170f * k;
                int count = 6 + rng.Next(5);   // 6~10
                for (int i = 0; i < count; i++)
                {
                    //  첫 덩이는 코스 안(뚫고 지나가게), 나머지는 안팎 400m 안에.
                    float range = i == 0 ? 80f : 400f;
                    float x = (float)(rng.NextDouble() * 2 - 1) * range;
                    float z = (float)(rng.NextDouble() * 2 - 1) * range;
                    float size = 18f + (float)rng.NextDouble() * 22f;
                    list.Add(new Vector4(x, y, z, size));
                }
            }
            return list;
        }

        /// <summary>꼭짓점이 아래인 원뿔(하늘섬 밑 바위). 면마다 점을 따로 둬서 법선이 0이 되지 않는다.</summary>
        public static Mesh BuildCone(float radius, float height, int segments)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var tip = new Vector3(0f, -height, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                //  옆면(바깥을 보게), 윗면(위를 보게) — 각자 점 셋.
                int b = verts.Count;
                verts.Add(p0); verts.Add(tip); verts.Add(p1);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                b = verts.Count;
                verts.Add(Vector3.zero); verts.Add(p0); verts.Add(p1);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            }
            var mesh = new Mesh { name = "SkyIslandCone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
