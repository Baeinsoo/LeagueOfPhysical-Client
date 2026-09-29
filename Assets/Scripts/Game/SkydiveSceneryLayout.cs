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
                //  선반 30m 아래 ~ 60m 위는 레이저 문·구멍이 있는 곳 — 코스 안에 불투명 구름을 두면 예고를 못 본다.
                bool nearShelf = false;
                foreach (float shelf in SkydiveCourseLayout.ShelfYs)
                {
                    nearShelf |= y >= shelf - 30f && y <= shelf + 60f;
                }
                int count = 6 + rng.Next(5);   // 6~10
                for (int i = 0; i < count; i++)
                {
                    //  첫 덩이는 코스 안(뚫고 지나가게), 나머지는 안팎 400m 안에. 선반 근처 층은 코스 밖에만.
                    float range = i == 0 && nearShelf == false ? 80f : 400f;
                    float size = 18f + (float)rng.NextDouble() * 22f;
                    float x, z;
                    do
                    {
                        x = (float)(rng.NextDouble() * 2 - 1) * range;
                        z = (float)(rng.NextDouble() * 2 - 1) * range;
                    }
                    while (nearShelf && Mathf.Abs(x) <= 110f + size && Mathf.Abs(z) <= 110f + size);
                    list.Add(new Vector4(x, y, z, size));
                }
            }
            return list;
        }

        /// <summary>
        /// 구름 한 층을 메시 하나로 — 덩이마다 가운데 큰 공 + 둘레 다섯(납작한 낮은 다각형 타원체). 폰 부담 때문에 공 하나하나를
        /// 오브젝트로 두지 않는다. 공마다 자기 점을 쓰고 법선은 중심에서 바깥 방향이라 0이 되지 않는다.
        /// </summary>
        public static Mesh BuildCloudLayer(List<Vector4> clouds, int seed)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            foreach (var c in clouds)
            {
                var center = new Vector3(c.x, c.y, c.z);
                float size = c.w;
                AddBlob(verts, normals, tris, center, new Vector3(size, size * 0.6f, size) * 0.5f);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f + (float)rng.NextDouble() * 0.6f;
                    float k = 0.55f + 0.2f * (float)rng.NextDouble();
                    var offset = new Vector3(Mathf.Cos(a) * size * 0.6f, (float)(rng.NextDouble() - 0.3) * size * 0.2f, Mathf.Sin(a) * size * 0.6f);
                    AddBlob(verts, normals, tris, center + offset, new Vector3(size * k, size * k * 0.6f, size * k) * 0.5f);
                }
            }
            var mesh = new Mesh { name = "SkydiveCloudLayer", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        //  낮은 다각형 타원체(경도 7 × 위도 4).
        private static void AddBlob(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 center, Vector3 radii)
        {
            const int lon = 7, lat = 4;
            int start = verts.Count;
            for (int y = 0; y <= lat; y++)
            {
                float v = Mathf.PI * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float u = Mathf.PI * 2f * x / lon;
                    var dir = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                    verts.Add(center + Vector3.Scale(dir, radii));
                    //  타원체 법선 = 방향을 반지름²로 나눈 것(정규화)
                    normals.Add(new Vector3(dir.x / radii.x, dir.y / radii.y, dir.z / radii.z).normalized);
                }
            }
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int i0 = start + y * (lon + 1) + x, i1 = i0 + 1, i2 = i0 + lon + 1, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }
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
                verts.Add(p1); verts.Add(tip); verts.Add(p0);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                b = verts.Count;
                verts.Add(Vector3.zero); verts.Add(p1); verts.Add(p0);
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
