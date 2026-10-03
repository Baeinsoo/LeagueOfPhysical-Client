using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;
using L = LOP.EditorTools.SkydivePyramidLayout;

namespace LOP.EditorTools
{
    /// <summary>
    /// 피라미드 맵을 표(<see cref="SkydivePyramidLayout"/>)에서 굽는다 — v2 나선. 씬은 굽기 결과라 손으로 고치지 않는다.
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

            //  판정 상자도 우리 툰 재질(꾸밈 1차) — 밟는 면은 사암 + 격자, 벽은 짙은 사암.
            var gray = SkydivePyramidDressing.Stone;
            var dark = SkydivePyramidDressing.StoneDark;
            var green = SkydivePyramidDressing.Jungle;

            var root = new GameObject("Course").transform;

            //  구간 1: 꼭대기 제단 + 스폰
            var altar = new Vector3(L.AltarXZ.x, L.SpawnY, L.AltarXZ.y);
            Box(root, "Altar", gray, altar, new Vector3(L.AltarHalf * 2f, Thickness, L.AltarHalf * 2f));
            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(root, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(spawns, false);
                sp.transform.localPosition = altar + new Vector3(Mathf.Cos(a) * 8f, 2f, Mathf.Sin(a) * 8f);
                sp.AddComponent<LOP.SpawnPoint>().Order = i;
            }

            //  구간 2: 나선 테라스 — 판 중심이 층마다 40° 돈다
            for (int k = 1; k <= L.Terraces.Length; k++)
            {
                var t = L.Terraces[k - 1];
                foreach (var p in Carve(L.PlateRect(k), t.Holes))
                {
                    Box(root, $"Terrace_{t.Y:0}_{p.Name}", gray, new Vector3((p.XMin + p.XMax) * 0.5f, t.Y, (p.ZMin + p.ZMax) * 0.5f),
                        new Vector3(p.Width, Thickness, p.Depth));
                }
            }

            //  테라스 계단 신전(부딪히는 물체) — 시안의 모서리 피라미드. 구멍·부활·레이저를 피해 고른 자리(L.SetPieces).
            foreach (var piece in L.SetPieces)
            {
                const int steps = 5;
                float stepH = piece.Height / steps;
                for (int k = 0; k < steps; k++)
                {
                    float w = piece.Half * 2f * (1f - k / (steps + 1.5f));
                    Box(root, $"Piece_{piece.Y:0}_{piece.X:0}_{piece.Z:0}_{k}", k % 2 == 0 ? gray : dark,
                        new Vector3(piece.X, piece.Y + 1.5f + stepH * (k + 0.5f), piece.Z), new Vector3(w, stepH, w));
                }
            }

            //  구간 3: 앞마당 + 갱도 입구 지붕·옆벽
            Box(root, "Porch", gray, Center(L.Porch, L.PorchY), new Vector3(L.Porch.Width, Thickness, L.Porch.Depth));
            foreach (var side in L.PorchSides)
            {
                Box(root, side.Name, gray, Center(side, L.PorchY), new Vector3(side.Width, Thickness, side.Depth));
            }
            var o = L.PorchOffset;   // 앞마당·갱도는 v1 배치를 나선 끝 자리로 옮긴 것
            Box(root, "Roof", gray, o + new Vector3(0f, L.RoofY, (L.ShaftZMin - L.ShaftWall + L.ShaftZMax + L.ShaftWall) * 0.5f),
                new Vector3((L.ShaftXHalf + L.ShaftWall) * 2f, Thickness, L.ShaftZMax - L.ShaftZMin + L.ShaftWall * 2f));
            foreach (float sx in new[] { -1f, 1f })
            {
                Box(root, $"EntranceWall_{sx:0}", dark, o + new Vector3(sx * (L.ShaftXHalf + L.ShaftWall * 0.5f), (L.PorchY + L.RoofY) * 0.5f, (L.ShaftZMin + L.ShaftZMax) * 0.5f),
                    new Vector3(L.ShaftWall, L.RoofY - L.PorchY, L.ShaftZMax - L.ShaftZMin));
            }

            //  구간 4: 갱도 벽 + 턱
            float shaftH = L.PorchY - L.ExitY, shaftCY = (L.PorchY + L.ExitY) * 0.5f, zc = (L.ShaftZMin + L.ShaftZMax) * 0.5f, zl = L.ShaftZMax - L.ShaftZMin;
            Box(root, "ShaftWall_W", dark, o + new Vector3(-L.ShaftXHalf - L.ShaftWall * 0.5f, shaftCY, zc), new Vector3(L.ShaftWall, shaftH, zl));
            Box(root, "ShaftWall_E", dark, o + new Vector3(L.ShaftXHalf + L.ShaftWall * 0.5f, shaftCY, zc), new Vector3(L.ShaftWall, shaftH, zl));
            Box(root, "ShaftWall_N", dark, o + new Vector3(0f, shaftCY, L.ShaftZMax + L.ShaftWall * 0.5f), new Vector3(L.ShaftXHalf * 2f + L.ShaftWall * 2f, shaftH, L.ShaftWall));
            Box(root, "ShaftWall_S", dark, o + new Vector3(0f, shaftCY, L.ShaftZMin - L.ShaftWall * 0.5f), new Vector3(L.ShaftXHalf * 2f + L.ShaftWall * 2f, shaftH, L.ShaftWall));
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

            //  나선 몸체 — 판 k 아래, 판 k+1 낙하 칸 밖(띠 벽 바깥)만. 위아래 판이 계단처럼 이어져 보인다.
            int bi = 0;
            foreach (var (rect, low, high) in L.BodyPieces)
            {
                var body = Box(root, $"Body_{bi++}_{rect.Name}", bi % 2 == 0 ? gray : dark, new Vector3((rect.XMin + rect.XMax) * 0.5f, (low + high) * 0.5f, (rect.ZMin + rect.ZMax) * 0.5f),
                    new Vector3(rect.Width, high - low, rect.Depth));
                //  몸체는 아래 테라스 바로 옆 400m 덩어리 — 그림자를 드리우면 아래 테라스가 통째로 어두워진다(10-04 캡처).
                body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            //  층별 경계 — 띠마다 그 층 판 둘레. 옆으로 흘러 한 층을 건너뛰지 못하게(충돌체 있음).
            var edge = EnsureMaterial("Assets/Art/Materials/SkydivePyramidBlockoutBoundary.mat", new Color(0.85f, 0.45f, 0.42f));
            var bands = L.BandWalls;
            for (int i = 0; i < bands.Length; i++)
            {
                var wall = Box(root, $"BandWall_{i}", edge, bands[i].center, bands[i].size);
                //  해가 남쪽에서 비춘다 — 2300m 벽이 그림자를 드리우면 코스 전체가 어두워진다.
                wall.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                //  벽 면은 그리지 않는다 — 꾸밈의 세로 레이저 울타리가 이 벽을 보여 준다(판정 자리와 같은 면).
                wall.GetComponent<MeshRenderer>().enabled = false;
            }

            //  레이저·문·바람·체크포인트
            var lasers = new GameObject("Lasers").transform;
            lasers.SetParent(root, false);
            foreach (var l in L.Lasers) { CreateLaserVolume(lasers, l); }
            var doors = new GameObject("Doors").transform;
            doors.SetParent(root, false);
            foreach (var d in L.TerraceDoors) { CreateDoorVolume(doors, d, dark); }
            var winds = new GameObject("Winds").transform;
            winds.SetParent(root, false);
            var windAssets = SkydiveWindAssets.EnsureAssets();
            foreach (var w in L.Winds) { CreateWindVolume(winds, w.Name, w.Center, w.Radius, w.Height, w.Wind, windAssets); }
            CreateCheckpointMarkers(root, L.SpawnY, L.RespawnPoints);

            //  CreateCheckpointMarkers는 스폰 표식을 (0,spawnY,0)에 하나 더 둔다 — 고도가 같은 표식이 둘이면 나중 것이 이겨,
            //  순서가 바뀌면 공중(원점)에서 부활한다. 표의 자리와 다른 표식은 지운다.
            foreach (var m in root.GetComponentsInChildren<LOP.CheckpointMarker>())
            {
                var pos = m.transform.position;
                if (L.RespawnPoints.TryGetValue(pos.y, out var want) == false || (want - pos).sqrMagnitude > 0.01f)
                {
                    Object.DestroyImmediate(m.gameObject);
                }
            }

            SkydivePyramidDressing.Dress(root);
            AssetDatabase.SaveAssets();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SkydivePyramid] 구웠다 — {ScenePath}");
        }

        internal static string Verify()
        {
            var spawn = L.AltarXZ;
            if (ReachableChain(false, L.Terraces, L.TerraceWinds, out string report, L.SpawnY, spawn) == false) { return "테라스 다이브 길 — " + report; }
            if (ReachableChain(true, L.Terraces, L.TerraceWinds, out report, L.SpawnY, spawn) == false) { return "테라스 안전한 길 — " + report; }
            //  첫 테라스(3200)는 익히기라 갈림길이 없다 — 갈림길 검사는 2800부터, 출발은 3200 구멍 한가운데(= 제단 아래).
            return FindRouteNotSplit(L.Terraces.Skip(1).ToArray(), L.TerraceWinds, L.Terraces[0].Y, spawn)
                ?? FindBlockedGate(L.Terraces, L.Lasers, L.SpawnY)
                ?? FindBlockedGate(L.ShaftLedges, L.Lasers, L.PorchY)
                ?? FindLaserOnSafeHole(L.Terraces, L.Lasers, L.SpawnY, L.TerracePlateCenters)
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
                    if (Mathf.Abs(p.x - L.AltarXZ.x) > L.AltarHalf || Mathf.Abs(p.z - L.AltarXZ.y) > L.AltarHalf) { return "제단 밖 스폰"; }
                    continue;
                }
                if (pair.Key == L.PorchY)
                {
                    var q = L.Porch;
                    if (p.x < q.XMin || p.x > q.XMax || p.z < q.ZMin || p.z > q.ZMax) { return "앞마당 밖 부활"; }
                    continue;
                }
                Shelf? shelf = null;
                Plate floor = default;
                for (int k = 1; k <= L.Terraces.Length; k++) { if (L.Terraces[k - 1].Y == pair.Key) { shelf = L.Terraces[k - 1]; floor = L.PlateRect(k); } }
                foreach (var t in L.ShaftLedges) { if (t.Y == pair.Key) { shelf = t; floor = L.ShaftFloor; } }
                if (shelf == null) { return $"{pair.Key:0}에 판이 없다"; }
                if (p.x < floor.XMin || p.x > floor.XMax || p.z < floor.ZMin || p.z > floor.ZMax) { return $"{pair.Key:0} 부활 지점이 판 밖"; }
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

    }
}
