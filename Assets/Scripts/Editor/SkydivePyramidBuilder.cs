using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;
using L = LOP.EditorTools.SkydivePyramidLayout;

namespace LOP.EditorTools
{
    /// <summary>
    /// 피라미드 맵을 표(<see cref="SkydivePyramidLayout"/>)에서 굽는다 — 단계 1은 회색 블록 아웃. 씬은 굽기 결과라 손으로 고치지 않는다.
    /// 대화상자를 띄우지 않는다(CLI로 돌린다). 검사가 걸리면 씬을 건드리지 않고 콘솔에 남긴다.
    /// </summary>
    public static class SkydivePyramidBuilder
    {
        public const string ScenePath = "Assets/Art/Scenes/SkydivePyramidMap.unity";
        private const float Thickness = 3f;

        [MenuItem("LOP/Skydive/피라미드 맵 굽기")]
        public static void Build()
        {
            string error = Verify();
            if (error != null)
            {
                Debug.LogError($"[SkydivePyramid] 검사 실패 — {error}. 굽지 않는다.");
                return;
            }

            var scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(go);   // 다시 구울 때 옛 것이 겹쳐 남지 않게
            }

            var gray = EnsureMaterial("Assets/Art/Materials/SkydivePyramidBlockout.mat", new Color(0.62f, 0.62f, 0.62f));
            var green = EnsureMaterial("Assets/Art/Materials/SkydivePyramidBlockoutGround.mat", new Color(0.45f, 0.6f, 0.45f));

            var root = new GameObject("Course").transform;

            //  구간 1: 꼭대기 제단 + 스폰
            Box(root, "Altar", gray, new Vector3(0f, L.SpawnY, 0f), new Vector3(L.AltarHalf * 2f, Thickness, L.AltarHalf * 2f));
            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(root, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(spawns, false);
                sp.transform.localPosition = new Vector3(Mathf.Cos(a) * 8f, L.SpawnY + 2f, Mathf.Sin(a) * 8f);
                sp.AddComponent<LOP.SpawnPoint>().Order = i;
            }

            //  구간 2: 테라스
            foreach (var t in L.Terraces)
            {
                foreach (var p in Carve(FullSlab(), t.Holes))
                {
                    Box(root, $"Terrace_{t.Y:0}_{p.Name}", gray, new Vector3((p.XMin + p.XMax) * 0.5f, t.Y, (p.ZMin + p.ZMax) * 0.5f),
                        new Vector3(p.Width, Thickness, p.Depth));
                }
            }

            //  구간 3: 앞마당 + 갱도 입구 지붕·옆벽
            Box(root, "Porch", gray, Center(L.Porch, L.PorchY), new Vector3(L.Porch.Width, Thickness, L.Porch.Depth));
            foreach (var side in L.PorchSides)
            {
                Box(root, side.Name, gray, Center(side, L.PorchY), new Vector3(side.Width, Thickness, side.Depth));
            }
            Box(root, "Roof", gray, new Vector3(0f, L.RoofY, (L.ShaftZMin - L.ShaftWall + L.ShaftZMax + L.ShaftWall) * 0.5f),
                new Vector3((L.ShaftXHalf + L.ShaftWall) * 2f, Thickness, L.ShaftZMax - L.ShaftZMin + L.ShaftWall * 2f));
            foreach (float sx in new[] { -1f, 1f })
            {
                Box(root, $"EntranceWall_{sx:0}", gray, new Vector3(sx * (L.ShaftXHalf + L.ShaftWall * 0.5f), (L.PorchY + L.RoofY) * 0.5f, (L.ShaftZMin + L.ShaftZMax) * 0.5f),
                    new Vector3(L.ShaftWall, L.RoofY - L.PorchY, L.ShaftZMax - L.ShaftZMin));
            }

            //  구간 4: 갱도 벽 + 턱
            float shaftH = L.PorchY - L.ExitY, shaftCY = (L.PorchY + L.ExitY) * 0.5f, zc = (L.ShaftZMin + L.ShaftZMax) * 0.5f, zl = L.ShaftZMax - L.ShaftZMin;
            Box(root, "ShaftWall_W", gray, new Vector3(-L.ShaftXHalf - L.ShaftWall * 0.5f, shaftCY, zc), new Vector3(L.ShaftWall, shaftH, zl));
            Box(root, "ShaftWall_E", gray, new Vector3(L.ShaftXHalf + L.ShaftWall * 0.5f, shaftCY, zc), new Vector3(L.ShaftWall, shaftH, zl));
            Box(root, "ShaftWall_N", gray, new Vector3(0f, shaftCY, L.ShaftZMax + L.ShaftWall * 0.5f), new Vector3(L.ShaftXHalf * 2f + L.ShaftWall * 2f, shaftH, L.ShaftWall));
            Box(root, "ShaftWall_S", gray, new Vector3(0f, shaftCY, L.ShaftZMin - L.ShaftWall * 0.5f), new Vector3(L.ShaftXHalf * 2f + L.ShaftWall * 2f, shaftH, L.ShaftWall));
            foreach (var ledge in L.ShaftLedges)
            {
                foreach (var p in Carve(L.ShaftFloor, ledge.Holes))
                {
                    Box(root, $"Ledge_{ledge.Y:0}_{p.Name}", gray, new Vector3((p.XMin + p.XMax) * 0.5f, ledge.Y, (p.ZMin + p.ZMax) * 0.5f),
                        new Vector3(p.Width, Thickness, p.Depth));
                }
            }

            //  구간 5: 정글 바닥 + 결승
            Box(root, "Ground", green, Vector3.zero, new Vector3(L.GroundHalf * 2f, Thickness, L.GroundHalf * 2f));
            var finish = new GameObject("FinishLine");
            finish.transform.SetParent(root, false);
            finish.transform.localPosition = new Vector3(0f, Thickness * 0.5f, 0f);
            finish.AddComponent<LOP.FinishLine>();

            //  장식(충돌체 없음): 피라미드 몸체 · 섬 바위
            float[] tiers = { L.SpawnY, 3200f, 2800f, 2400f, 2000f };
            for (int i = 0; i < tiers.Length - 1; i++)
            {
                float w = 200f + 2f * 60f * i, d = 120f + 60f * i;
                Deco(root, $"PyramidTier_{i}", gray, new Vector3(0f, (tiers[i] + tiers[i + 1]) * 0.5f, 100f + d * 0.5f), new Vector3(w, tiers[i] - tiers[i + 1], d));
            }
            Deco(root, "IslandRock", gray, new Vector3(0f, 1225f, 250f), new Vector3(400f, 1550f, 300f));

            //  레이저·문·바람·체크포인트
            var lasers = new GameObject("Lasers").transform;
            lasers.SetParent(root, false);
            foreach (var l in L.Lasers) { CreateLaserVolume(lasers, l); }
            var doors = new GameObject("Doors").transform;
            doors.SetParent(root, false);
            foreach (var d in L.TerraceDoors) { CreateDoorVolume(doors, d, gray); }
            var winds = new GameObject("Winds").transform;
            winds.SetParent(root, false);
            var windAssets = SkydiveWindAssets.EnsureAssets();
            foreach (var w in L.Winds) { CreateWindVolume(winds, w.Name, w.Center, w.Radius, w.Height, w.Wind, windAssets); }
            CreateCheckpointMarkers(root, L.SpawnY, L.RespawnPoints);

            //  CreateCheckpointMarkers는 스폰 표식을 (0,spawnY,0)에 한 번 더 둔다 — 표에도 같은 값이 있어 같은 자리에 둘이 생긴다. 하나만 남긴다.
            var seen = new HashSet<Vector3>();
            foreach (var m in root.GetComponentsInChildren<LOP.CheckpointMarker>())
            {
                if (seen.Add(m.transform.position) == false) { Object.DestroyImmediate(m.gameObject); }
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SkydivePyramid] 구웠다 — {ScenePath}");
        }

        internal static string Verify()
        {
            if (ReachableChain(false, L.Terraces, L.TerraceWinds, out string report, L.SpawnY) == false) { return "테라스 다이브 길 — " + report; }
            if (ReachableChain(true, L.Terraces, L.TerraceWinds, out report, L.SpawnY) == false) { return "테라스 안전한 길 — " + report; }
            return FindRouteNotSplit(L.Terraces, L.TerraceWinds, L.SpawnY)
                ?? FindDoorHoleMismatch(L.Terraces, L.TerraceDoors)
                ?? FindDoorSizeMismatch(L.Terraces, L.TerraceDoors)
                ?? FindDoorNeverCloses(L.TerraceDoors)
                ?? FindDoorPanelOnSafeHole(L.Terraces, L.TerraceDoors)
                ?? FindTooFastLaser(L.Lasers)
                ?? FindBadRespawn();
        }

        /// <summary>부활 지점이 자기 판 위, 구멍 밖인지. 판: 3600 제단 · 테라스 · 1300 앞마당 · 갱도 턱.</summary>
        internal static string FindBadRespawn()
        {
            foreach (var pair in L.RespawnPoints)
            {
                Vector3 p = pair.Value;
                if (Mathf.Abs(p.y - pair.Key) > 0.001f) { return $"{pair.Key:0} 부활 지점 고도가 다르다"; }
                if (pair.Key == L.SpawnY)
                {
                    if (Mathf.Abs(p.x) > L.AltarHalf || Mathf.Abs(p.z) > L.AltarHalf) { return "제단 밖 스폰"; }
                    continue;
                }
                if (pair.Key == L.PorchY)
                {
                    var q = L.Porch;
                    if (p.x < q.XMin || p.x > q.XMax || p.z < q.ZMin || p.z > q.ZMax) { return "앞마당 밖 부활"; }
                    continue;
                }
                Shelf? shelf = null;
                foreach (var t in L.Terraces) { if (t.Y == pair.Key) { shelf = t; } }
                foreach (var t in L.ShaftLedges) { if (t.Y == pair.Key) { shelf = t; } }
                if (shelf == null) { return $"{pair.Key:0}에 판이 없다"; }
                bool inShaft = pair.Key <= L.PorchY;
                float xh = inShaft ? L.ShaftXHalf : 100f;
                float zMin = inShaft ? L.ShaftZMin : -100f, zMax = inShaft ? L.ShaftZMax : 100f;
                if (Mathf.Abs(p.x) > xh || p.z < zMin || p.z > zMax) { return $"{pair.Key:0} 부활 지점이 판 밖"; }
                foreach (var h in shelf.Value.Holes)
                {
                    //  흩뿌림 반경(SkydiveRespawn.SpreadRadius)까지 구멍에서 떨어져 있어야 한다.
                    float r = h.Half + LOP.SkydiveRespawn.SpreadRadius;
                    if (Mathf.Abs(p.x - h.X) <= r && Mathf.Abs(p.z - h.Z) <= r) { return $"{pair.Key:0} 부활 지점이 구멍({h.X:0},{h.Z:0}) 위"; }
                }
            }
            return null;
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { return m; }
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Vector3 Center(in Plate p, float y) => new Vector3((p.XMin + p.XMax) * 0.5f, y, (p.ZMin + p.ZMax) * 0.5f);

        private static GameObject Box(Transform parent, string name, Material m, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.layer = LayerMask.NameToLayer("Default");   // 낙하 sweep 마스크가 보는 레이어
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        //  장식 — 판정과 무관하니 충돌체를 지운다(남으면 몸이 그 위에 선다).
        private static void Deco(Transform parent, string name, Material m, Vector3 center, Vector3 size)
        {
            var go = Box(parent, name, m, center, size);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
