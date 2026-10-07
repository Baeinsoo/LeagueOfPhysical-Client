using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static Material Stone => Toon("Stone", "#D9B48A", topGrid: 8f, sideGrid: 4f);
        public static Material StoneDark => Toon("StoneDark", "#A9825A", sideGrid: 4f);
        public static Material Rock => Toon("Rock", "#B98F66", sideGrid: 7f);
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

            //  꼭대기 신전·피라미드 면 덩굴은 슬라이스 B에서 블렌더 부품으로 다시 한다(v2 나선엔 v1 자리가 없다).
            HoleRims(root);
            SetPieceDetails(root, rng);
            MossLumps(root, rng);
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
            float tz = 160f, ty = y;   // 맨 윗단 윗면(제단 높이) 위에 선다
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
            float[] tops = { L.SpawnY, 3200f, 2800f, 2400f };
            for (int i = 0; i < tops.Length; i++)
            {
                float w = 200f + 2f * 60f * i;
                Prim(root, $"TierBand_{i}", PrimitiveType.Cube, StoneDark, new Vector3(0f, tops[i] - 3f, 99f), new Vector3(w + 1f, 4f, 3f));
                Prim(root, $"TierBand2_{i}", PrimitiveType.Cube, Moss, new Vector3(0f, tops[i] - 8f, 99.5f), new Vector3(w + 0.5f, 1.2f, 2f));
                for (int k = 0; k < 26; k++)
                {
                    float x = ((float)rng.NextDouble() * 2f - 1f) * (w * 0.5f - 6f);
                    float len = 20f + (float)rng.NextDouble() * 90f;
                    Prim(root, $"Vine_{i}_{k}", PrimitiveType.Cube, Vine, new Vector3(x, tops[i] - 6f - len * 0.5f, 98.8f), new Vector3(2.4f, len, 2.4f));
                    Prim(root, $"VineLeaf_{i}_{k}", PrimitiveType.Sphere, Moss, new Vector3(x, tops[i] - 6f - len, 98.5f), new Vector3(5f, 4f, 3f));
                }
            }
        }

        //  구멍 = 신전 우물: 덩어리 돌 테두리(시안처럼 블록이 하나씩 보이게, 사암·짙은 사암 번갈아). 높이 1.2 — 낮게.
        private static void HoleRims(Transform root)
        {
            foreach (var t in L.Terraces)
            {
                foreach (var h in t.Holes)
                {
                    BlockRim(root, h.X, h.Z, h.Half + 1.2f, t.Y + 1.5f + 0.6f);
                }
                var pc = L.PlateCenter(System.Array.IndexOf(L.TerraceYs, t.Y) + 1);   // 판마다 중심이 다르다(나선)
                Frame(root, $"TerraceEdge_{t.Y:0}", StoneDark, pc.x, pc.y, 98f, t.Y + 1.75f, 2f, 0.5f);
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
            //  묶음마다 눈 하나(묶음 이름 = '_' 앞) — 빔마다 달면 빗살 끝에 돌이 줄줄이 뜬다. 묶음 피벗들의 가운데.
            var glow = Unlit("EyeGlow", LOP.SkydiveLaserView.LitColor);
            foreach (var group in L.Lasers.GroupBy(l => l.Name.Split('_')[0] == "Laser" ? l.Name : l.Name.Split('_')[0]))
            {
                var p = group.Aggregate(Vector3.zero, (acc, l) => acc + l.Pivot) / group.Count();
                var stone = Prim(root, $"Eye_{group.Key}", PrimitiveType.Cube, StoneDark, p, Vector3.one * 3f);
                stone.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
                Prim(root, $"EyeCore_{group.Key}", PrimitiveType.Sphere, glow, p, Vector3.one * 1.6f);
            }
        }

        //  경계 = 세로 레이저 울타리(파킹된 경계 아이디어 1번). 판정 벽은 빌더가 그대로 두고 그림만 이것으로.
        private static void BoundaryBeams(Transform root)
        {
            //  놀이 칸 둘레(±104)에만, 40m 간격으로 성기게 — 북쪽 끝까지 촘촘히 세웠더니 화면이 붉은 우리가 됐다(10-03 캡처).
            var beam = Unlit("FenceBeam", new Color(1f, 0.3f, 0.35f));
            foreach (var wall in L.BandWalls)
            {
                bool alongX = wall.size.x > wall.size.z;
                for (float s = -100f; s <= 100.01f; s += 40f)
                {
                    var p = alongX ? new Vector3(wall.center.x + s, wall.center.y, wall.center.z) : new Vector3(wall.center.x, wall.center.y, wall.center.z + s);
                    if (wall.Contains(p) == false) { continue; }
                    Prim(root, "FenceBeam", PrimitiveType.Cube, beam, p, new Vector3(0.35f, wall.size.y, 0.35f));
                }
            }
        }

        //  구름층(1500~1900): 층마다 메시 한 장. 코스 안엔 층마다 한 덩이 — 뚫고 지나가는 맛.
        private static void Clouds(Transform root, System.Random rng)
        {
            //  테라스 사이 바깥(코스 밖)에도 구름을 깔아 떨어지며 내려다볼 때 깊이가 보이게(시안의 아래 구름).
            float[] outerLayers = { 3000f, 2600f, 2200f };
            for (int k = 0; k < outerLayers.Length; k++)
            {
                var ring = new List<Vector4>();
                for (int i = 0; i < 16; i++)
                {
                    float x, z;
                    do { x = Rand(rng, 480f); z = Rand(rng, 480f); }
                    while (Mathf.Abs(x) < 200f && Mathf.Abs(z) < 200f);   // 나선 판들이 원점 ±170 안에 있다
                    ring.Add(new Vector4(x, outerLayers[k] + Rand(rng, 30f), z, 22f + (float)rng.NextDouble() * 28f));
                }
                MeshObj(root, $"OuterCloud_{outerLayers[k]:0}", SaveMesh(LOP.SkydiveSceneryLayout.BuildCloudLayer(ring, 200 + k), $"OuterCloud_{k}"), Cloud, Vector3.zero);
            }

            float[] layers = { 1520f, 1700f, 1850f };
            for (int k = 0; k < layers.Length; k++)
            {
                var o = L.PorchOffset;
                var spots = new List<Vector4> { new Vector4(o.x + Rand(rng, 60f), layers[k], o.z + Rand(rng, 60f), 22f) };
                for (int i = 0; i < 14; i++)
                {
                    float x, z;
                    do { x = Rand(rng, 520f); z = Rand(rng, 520f); }
                    while (Mathf.Abs(x) < 200f && Mathf.Abs(z) < 200f);
                    spots.Add(new Vector4(x, layers[k] + Rand(rng, 25f), z, 20f + (float)rng.NextDouble() * 25f));
                }
                var mesh = SaveMesh(LOP.SkydiveSceneryLayout.BuildCloudLayer(spots, 100 + k), $"CloudLayer_{k}");
                MeshObj(root, $"CloudLayer_{layers[k]:0}", mesh, Cloud, Vector3.zero);
            }
        }

        //  폭포: 하강풍 기둥 자리에 떨어지는 물줄기, 상승풍 자리에 물안개 뭉치
        private static void Waterfall(Transform root)
        {
            var o = L.PorchOffset;
            Prim(root, "Waterfall", PrimitiveType.Cube, Water, o + new Vector3(70f, 1745f, -40f), new Vector3(14f, 500f, 3f));
            var mist = new List<Vector4>
            {
                new Vector4(o.x + 70f, 1490f, o.z - 40f, 18f),
                new Vector4(o.x - 60f, 1400f, o.z - 40f, 16f),
                new Vector4(o.x - 60f, 1440f, o.z - 40f, 12f),
            };
            MeshObj(root, "Mist", SaveMesh(LOP.SkydiveSceneryLayout.BuildCloudLayer(mist, 7), "Mist"), Cloud, Vector3.zero);
        }

        //  섬 윗면 풀밭(피라미드 뒤) + 밑동 거꾸로 선 바위 — 코스 칸(z ≤ 100)은 가리지 않는다
        private static void IslandUnderside(Transform root)
        {
            //  갱도를 감싼 섬 밑동 — 출구(450) 아래로 거꾸로 선 바위
            var cone = SaveMesh(LOP.SkydiveSceneryLayout.BuildCone(160f, 420f, 14), "IslandCone");
            //  출구 구멍(앞마당 기준 z 70)의 낙하 기둥을 비켜 뒤쪽(z 250)에 — 덮으면 캐릭터가 바위 속으로 사라진다.
            MeshObj(root, "IslandCone", cone, Rock, L.PorchOffset + new Vector3(0f, L.ExitY, 250f));
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

        //  테라스 계단 신전 위 꾸밈: 앞 문, 꼭대기 석상 머리, 옆면 덩굴
        private static void SetPieceDetails(Transform root, System.Random rng)
        {
            foreach (var p in L.SetPieces)
            {
                float baseY = p.Y + 1.5f;
                float topY = baseY + p.Height;
                //  가운데(0,0)를 보는 면에 문
                var toCenter = new Vector3(-p.X, 0f, -p.Z).normalized;
                Vector3 face = Mathf.Abs(toCenter.x) > Mathf.Abs(toCenter.z) ? new Vector3(Mathf.Sign(toCenter.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toCenter.z));
                var c = new Vector3(p.X, 0f, p.Z);
                var door = Prim(root, "PieceDoor", PrimitiveType.Cube, Toon("Door", "#4A3A2C"), c + face * (p.Half + 0.3f) + Vector3.up * (baseY + 4f), Vector3.one);
                door.transform.localScale = face.x != 0f ? new Vector3(0.6f, 8f, 6f) : new Vector3(6f, 8f, 0.6f);
                Head(root, c + Vector3.up * (topY + 4f), 6f);
                for (int k = 0; k < 5; k++)
                {
                    float len = 6f + (float)rng.NextDouble() * 14f;
                    var side = face.x != 0f ? new Vector3(0f, 0f, Rand(rng, p.Half * 0.8f)) : new Vector3(Rand(rng, p.Half * 0.8f), 0f, 0f);
                    Prim(root, "PieceVine", PrimitiveType.Cube, Vine, c + side + face * (p.Half * 0.75f + 0.2f) + Vector3.up * (topY - 2f - len * 0.5f), new Vector3(1.4f, len, 1.4f));
                }
            }
        }

        //  테라스 위 이끼 덩어리(낮게 — 걸어 지나가도 발목 높이)
        private static void MossLumps(Transform root, System.Random rng)
        {
            foreach (var t in L.Terraces)
            {
                int placed = 0;
                for (int tries = 0; tries < 60 && placed < 8; tries++)
                {
                    var pc = L.PlateCenter(System.Array.IndexOf(L.TerraceYs, t.Y) + 1);
                    float x = pc.x + Rand(rng, 92f), z = pc.y + Rand(rng, 92f);
                    bool bad = false;
                    foreach (var h in t.Holes) { bad |= Mathf.Abs(x - h.X) < h.Half + 5f && Mathf.Abs(z - h.Z) < h.Half + 5f; }
                    foreach (var p in L.SetPieces) { bad |= p.Y == t.Y && Mathf.Abs(x - p.X) < p.Half + 3f && Mathf.Abs(z - p.Z) < p.Half + 3f; }
                    if (bad) { continue; }
                    //  덤불 덩어리 2~3개 — 납작한 원판은 위에서 보면 초록 점이었다
                    int n = 2 + rng.Next(2);
                    for (int k = 0; k < n; k++)
                    {
                        float r = 1.8f + (float)rng.NextDouble() * 1.6f;
                        Prim(root, "Moss", PrimitiveType.Sphere, k == 0 ? Moss : Vine, new Vector3(x + Rand(rng, 3f), t.Y + 1.5f + r * 0.5f, z + Rand(rng, 3f)), new Vector3(r * 2f, r * 1.4f, r * 2f));
                    }
                    placed++;
                }
            }
        }

        //  덩어리 돌 테두리 — 변마다 3.2m 블록을 0.3m 틈으로 늘어놓는다
        private static void BlockRim(Transform root, float cx, float cz, float half, float y)
        {
            const float len = 3.2f, gap = 0.3f, depth = 1.6f, height = 1.2f;
            int i = 0;
            var sides = new[] { (Vector3.right, Vector3.forward), (Vector3.right, Vector3.back), (Vector3.forward, Vector3.right), (Vector3.forward, Vector3.left) };
            foreach (var (dir, normal) in sides)
            {
                float span = half * 2f + depth * 2f;
                for (float s = -span * 0.5f + len * 0.5f; s <= span * 0.5f - len * 0.5f + 0.01f; s += len + gap)
                {
                    var p = new Vector3(cx, y, cz) + normal * (half + depth * 0.5f) + dir * s;
                    var size = dir.x != 0f ? new Vector3(len, height, depth) : new Vector3(depth, height, len);
                    Prim(root, "WellBlock", PrimitiveType.Cube, (i++ % 2 == 0) ? Stone : StoneDark, p, size);
                }
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
        internal static Mesh SaveMesh(Mesh mesh, string name)
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

        internal static Material Toon(string name, string hex, float topGrid = 0f, float sideGrid = 0f)
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
            m.SetFloat("_SideGrid", sideGrid);
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
