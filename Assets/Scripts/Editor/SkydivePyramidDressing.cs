using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;
using L = LOP.EditorTools.SkydivePyramidLayout;

namespace LOP.EditorTools
{
    /// <summary>
    /// 피라미드 맵 꾸밈 1차(시안 https://claude.ai/artifact/BKgky8i3htNnU1AgLyEGDZ 1번) — 보이는 것만, 판정은 안 건드린다.
    /// 모든 꾸밈은 충돌체가 없다(보이는 것과 판정이 어긋나면 안 보이는 곳에서 걸린다). 코드로 만든 메시는 에셋으로 덮어써 GUID를 지킨다.
    /// </summary>
    internal static class SkydivePyramidDressing
    {
        private const string MaterialDir = "Assets/Art/Materials/Pyramid";
        private const string MeshDir = "Assets/Art/Models/Pyramid";

        public static Material Stone => Toon("Stone", "#D9B48A", topGrid: 5f);
        public static Material StoneDark => Toon("StoneDark", "#A9825A");
        public static Material Rock => Toon("Rock", "#B98F66");
        public static Material Jungle => Toon("Jungle", "#5FA35A");
        private static Material Moss => Toon("Moss", "#7DB46A");
        private static Material Vine => Toon("Vine", "#5E9E55");
        private static Material Tree => Toon("Tree", "#3F8A4A");
        private static Material Trunk => Toon("Trunk", "#8B5E34");
        private static Material Plaza => Toon("Plaza", "#E2C9A0", topGrid: 4f);
        private static Material Water => Toon("Water", "#BFE3F2");
        private static Material Cloud => Toon("Cloud", "#FFFFFF");

        public static void Dress(Transform course)
        {
            var root = new GameObject("Dressing").transform;
            root.SetParent(course, false);
            var rng = new System.Random(20261003);

            Summit(root);
            PyramidFaces(root, rng);
            HoleRims(root);
            LaserEyes(root);
            BoundaryBeams(root);
            Clouds(root, rng);
            Waterfall(root);
            IslandUnderside(root);
            JunglePlaza(root, rng);
            FarIslands(root, rng);
        }

        //  꼭대기: 제단 테두리 + 뒤쪽 신전(문 · 지붕) + 석상 머리 둘
        private static void Summit(Transform root)
        {
            float y = L.SpawnY;
            Frame(root, "AltarRim", StoneDark, 0f, 0f, L.AltarHalf, y + 1.6f, 2f, 0.6f);
            float tz = 160f, ty = y + 60f;
            Prim(root, "Temple", PrimitiveType.Cube, Stone, new Vector3(0f, ty + 15f, tz), new Vector3(70f, 30f, 60f));
            Prim(root, "TempleRoof", PrimitiveType.Cube, StoneDark, new Vector3(0f, ty + 32f, tz), new Vector3(80f, 4f, 70f));
            Prim(root, "TempleDoor", PrimitiveType.Cube, Toon("Door", "#4A3A2C"), new Vector3(0f, ty + 9f, tz - 30.2f), new Vector3(12f, 18f, 1f));
            foreach (float x in new[] { -28f, 28f })
            {
                Head(root, new Vector3(x, ty + 5f, tz - 34f), 8f);
            }
        }

        //  계단 단 앞면(남쪽, z=100): 위 끝 문양 띠 + 늘어진 덩굴
        private static void PyramidFaces(Transform root, System.Random rng)
        {
            float[] tops = { L.SpawnY + 60f, 3200f, 2800f, 2400f };
            for (int i = 0; i < tops.Length; i++)
            {
                float w = 200f + 2f * 60f * i;
                Prim(root, $"TierBand_{i}", PrimitiveType.Cube, StoneDark, new Vector3(0f, tops[i] - 3f, 99f), new Vector3(w + 1f, 4f, 3f));
                Prim(root, $"TierBand2_{i}", PrimitiveType.Cube, Moss, new Vector3(0f, tops[i] - 8f, 99.5f), new Vector3(w + 0.5f, 1.2f, 2f));
                for (int k = 0; k < 12; k++)
                {
                    float x = ((float)rng.NextDouble() * 2f - 1f) * (w * 0.5f - 6f);
                    float len = 15f + (float)rng.NextDouble() * 55f;
                    Prim(root, $"Vine_{i}_{k}", PrimitiveType.Cube, Vine, new Vector3(x, tops[i] - 6f - len * 0.5f, 99.4f), new Vector3(1.2f, len, 1.2f));
                }
            }
        }

        //  구멍 = 신전 우물: 짙은 돌 테두리. 판 가장자리에도 띠. 높이 0.5 — 걸어 지나가도 거슬리지 않게 낮게.
        private static void HoleRims(Transform root)
        {
            foreach (var t in L.Terraces)
            {
                foreach (var h in t.Holes)
                {
                    Frame(root, $"Well_{t.Y:0}_{h.X:0}_{h.Z:0}", StoneDark, h.X, h.Z, h.Half + 1f, t.Y + 1.75f, 2f, 0.5f);
                }
                Frame(root, $"TerraceEdge_{t.Y:0}", StoneDark, 0f, 0f, 99f, t.Y + 1.75f, 2f, 0.5f);
            }
            foreach (var t in L.ShaftLedges)
            {
                foreach (var h in t.Holes)
                {
                    Frame(root, $"Well_{t.Y:0}_{h.X:0}_{h.Z:0}", StoneDark, h.X, h.Z, h.Half + 1f, t.Y + 1.75f, 2f, 0.5f);
                }
            }
        }

        //  레이저는 떠 있는 석상 눈에서 나온다 — 작은 돌(마름모) + 빛나는 붉은 심. 충돌체 없음, 몸보다 작게(3m).
        private static void LaserEyes(Transform root)
        {
            var glow = Unlit("EyeGlow", LOP.SkydiveLaserView.LitColor);
            foreach (var l in L.Lasers)
            {
                var stone = Prim(root, $"Eye_{l.Name}", PrimitiveType.Cube, StoneDark, l.Pivot, Vector3.one * 3f);
                stone.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
                Prim(root, $"EyeCore_{l.Name}", PrimitiveType.Sphere, glow, l.Pivot, Vector3.one * 1.6f);
            }
        }

        //  경계 = 세로 레이저 울타리(파킹된 경계 아이디어 1번). 판정 벽은 빌더가 그대로 두고 그림만 이것으로.
        private static void BoundaryBeams(Transform root)
        {
            var beam = Unlit("FenceBeam", LOP.SkydiveLaserView.LitColor * 0.8f);
            foreach (var wall in L.BoundaryWalls)
            {
                bool alongX = wall.size.x > wall.size.z;
                float span = alongX ? wall.size.x : wall.size.z;
                for (float s = -span * 0.5f + 4f; s <= span * 0.5f - 4f; s += 13f)
                {
                    var p = wall.center + (alongX ? new Vector3(s, 0f, 0f) : new Vector3(0f, 0f, s));
                    Prim(root, "FenceBeam", PrimitiveType.Cube, beam, p, new Vector3(0.5f, wall.size.y, 0.5f));
                }
            }
        }

        //  구름층(1500~1900): 층마다 메시 한 장. 코스 안엔 층마다 한 덩이 — 뚫고 지나가는 맛.
        private static void Clouds(Transform root, System.Random rng)
        {
            float[] layers = { 1520f, 1700f, 1850f };
            for (int k = 0; k < layers.Length; k++)
            {
                var spots = new List<Vector4> { new Vector4(Rand(rng, 60f), layers[k], Rand(rng, 60f), 22f) };
                for (int i = 0; i < 14; i++)
                {
                    float x, z;
                    do { x = Rand(rng, 520f); z = Rand(rng, 520f); }
                    while (Mathf.Abs(x) < 140f && Mathf.Abs(z) < 140f);
                    spots.Add(new Vector4(x, layers[k] + Rand(rng, 25f), z, 20f + (float)rng.NextDouble() * 25f));
                }
                var mesh = SaveMesh(LOP.SkydiveSceneryLayout.BuildCloudLayer(spots, 100 + k), $"CloudLayer_{k}");
                MeshObj(root, $"CloudLayer_{layers[k]:0}", mesh, Cloud, Vector3.zero);
            }
        }

        //  폭포: 하강풍 기둥 자리에 떨어지는 물줄기, 상승풍 자리에 물안개 뭉치
        private static void Waterfall(Transform root)
        {
            Prim(root, "Waterfall", PrimitiveType.Cube, Water, new Vector3(70f, 1745f, -40f), new Vector3(14f, 500f, 3f));
            var mist = new List<Vector4>
            {
                new Vector4(70f, 1490f, -40f, 18f),
                new Vector4(-60f, 1400f, -40f, 16f),
                new Vector4(-60f, 1440f, -40f, 12f),
            };
            MeshObj(root, "Mist", SaveMesh(LOP.SkydiveSceneryLayout.BuildCloudLayer(mist, 7), "Mist"), Cloud, Vector3.zero);
        }

        //  섬 윗면 풀밭(피라미드 뒤) + 밑동 거꾸로 선 바위 — 코스 칸(z ≤ 100)은 가리지 않는다
        private static void IslandUnderside(Transform root)
        {
            Prim(root, "IslandGrass", PrimitiveType.Cylinder, Moss, new Vector3(0f, 2001f, 270f), new Vector3(320f, 1.5f, 320f));
            var cone = SaveMesh(LOP.SkydiveSceneryLayout.BuildCone(160f, 420f, 14), "IslandCone");
            MeshObj(root, "IslandCone", cone, Rock, new Vector3(0f, L.ExitY, 250f));
        }

        //  정글: 신전 광장(가운데) + 테두리 + 북쪽 작은 계단 신전 + 나무
        private static void JunglePlaza(Transform root, System.Random rng)
        {
            Prim(root, "Plaza", PrimitiveType.Cube, Plaza, new Vector3(0f, 1.7f, 0f), new Vector3(120f, 0.4f, 120f));
            Frame(root, "PlazaRim", StoneDark, 0f, 0f, 61f, 1.9f, 2f, 0.6f);
            for (int k = 0; k < 4; k++)
            {
                float w = 40f - k * 9f;
                Prim(root, $"JungleTemple_{k}", PrimitiveType.Cube, k % 2 == 0 ? Stone : StoneDark, new Vector3(0f, 1.5f + 3f + k * 6f, 110f), new Vector3(w, 6f, w));
            }
            for (int i = 0; i < 70; i++)
            {
                float x, z;
                do { x = Rand(rng, 280f); z = Rand(rng, 280f); }
                while (Mathf.Abs(x) < 75f && Mathf.Abs(z) < 75f);
                float s = 3f + (float)rng.NextDouble() * 3f;
                var p = new Vector3(x, 1.5f, z);
                Prim(root, "Trunk", PrimitiveType.Cylinder, Trunk, p + new Vector3(0f, s, 0f), new Vector3(s * 0.4f, s, s * 0.4f));
                Prim(root, "Canopy", PrimitiveType.Sphere, Tree, p + new Vector3(0f, s * 2.6f, 0f), Vector3.one * s * 2.4f);
                Prim(root, "Canopy", PrimitiveType.Sphere, Tree, p + new Vector3(s * 0.7f, s * 2.1f, s * 0.3f), Vector3.one * s * 1.5f);
            }
        }

        //  먼 하늘섬(배경): 풀 원판 + 거꾸로 선 바위 + 작은 계단 신전
        private static void FarIslands(Transform root, System.Random rng)
        {
            int n = 0;
            foreach (var island in LOP.SkydiveSceneryLayout.IslandSpots())
            {
                Vector3 c = island.Center;
                float r = island.Radius;
                Prim(root, $"FarIsland_{n}", PrimitiveType.Cylinder, Moss, c, new Vector3(r * 2f, 3f, r * 2f));
                MeshObj(root, $"FarIslandRock_{n}", SaveMesh(LOP.SkydiveSceneryLayout.BuildCone(r, r * 1.6f, 12), $"FarIslandCone_{n}"), Rock, c - new Vector3(0f, 3f, 0f));
                for (int k = 0; k < 3; k++)
                {
                    float w = r * (0.7f - k * 0.18f);
                    Prim(root, $"FarTemple_{n}_{k}", PrimitiveType.Cube, k % 2 == 0 ? Stone : StoneDark, c + new Vector3(0f, 1.5f + r * 0.08f * (k + 0.5f) * 2f, 0f), new Vector3(w, r * 0.16f, w));
                }
                n++;
            }
        }

        // ---- 도우미 ----

        private static float Rand(System.Random rng, float range) => ((float)rng.NextDouble() * 2f - 1f) * range;

        //  정사각 테두리(네 막대). half = 안쪽 반폭.
        private static void Frame(Transform root, string name, Material m, float cx, float cz, float half, float y, float width, float height)
        {
            float outer = half + width;
            Prim(root, name + "_N", PrimitiveType.Cube, m, new Vector3(cx, y, cz + half + width * 0.5f), new Vector3(outer * 2f, height, width));
            Prim(root, name + "_S", PrimitiveType.Cube, m, new Vector3(cx, y, cz - half - width * 0.5f), new Vector3(outer * 2f, height, width));
            Prim(root, name + "_E", PrimitiveType.Cube, m, new Vector3(cx + half + width * 0.5f, y, cz), new Vector3(width, height, half * 2f));
            Prim(root, name + "_W", PrimitiveType.Cube, m, new Vector3(cx - half - width * 0.5f, y, cz), new Vector3(width, height, half * 2f));
        }

        private static void Head(Transform root, Vector3 p, float size)
        {
            Prim(root, "Head", PrimitiveType.Cube, Stone, p, new Vector3(size, size * 1.1f, size));
            Prim(root, "HeadBrow", PrimitiveType.Cube, StoneDark, p + new Vector3(0f, size * 0.35f, -size * 0.5f), new Vector3(size * 1.05f, size * 0.15f, 0.6f));
            var glow = Unlit("EyeGlow", LOP.SkydiveLaserView.LitColor);
            foreach (float dx in new[] { -size * 0.22f, size * 0.22f })
            {
                Prim(root, "HeadEye", PrimitiveType.Cube, glow, p + new Vector3(dx, size * 0.12f, -size * 0.51f), new Vector3(size * 0.16f, size * 0.12f, 0.3f));
            }
        }

        private static GameObject Prim(Transform root, string name, PrimitiveType type, Material m, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 꾸밈 — 판정과 무관
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static void MeshObj(Transform root, string name, Mesh mesh, Material m, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        //  메시 에셋 덮어쓰기 — 지우고 새로 만들면 GUID가 바뀌어 씬 참조·원격 에셋이 깨진다.
        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.colors = mesh.colors;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Material Toon(string name, string hex, float topGrid = 0f)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/Pyramid{name}.mat";
            ColorUtility.TryParseHtmlString(hex, out var color);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("LOP/Toon"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_TopGrid", topGrid);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 외곽선은 캐릭터만
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Unlit(string name, Color color)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/Pyramid{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
