using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;
using S = LOP.EditorTools.SkydiveSpiralLayout;

namespace LOP.EditorTools
{
    /// <summary>
    /// 피라미드 개정안 회색 맵을 표(<see cref="SkydiveSpiralLayout"/>)에서 굽는다. 대화상자를 띄우지 않는다(CLI로 돌린다).
    /// 검사가 걸리면 씬을 건드리지 않고 콘솔에 남긴다.
    /// </summary>
    public static class SkydiveSpiralBuilder
    {
        public const string ScenePath = "Assets/Art/Scenes/SkydiveSpiralMap.unity";
        private const float Thickness = 3f, Wall = 4f;

        [MenuItem("LOP/Skydive/피라미드 개정안 굽기")]
        public static void Build()
        {
            string error = Verify();
            if (error != null)
            {
                Debug.LogError($"[SkydiveSpiral] 검사 실패 — {error}. 굽지 않는다.");
                return;
            }

            var scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(go);
            }

            var stone = SkydivePyramidDressing.Stone;
            var dark = SkydivePyramidDressing.StoneDark;
            var green = SkydivePyramidDressing.Jungle;
            var energy = EnergyWallMaterial();
            var root = new GameObject("Course").transform;

            //  ① 나선 테라스 — 판은 불투명(구멍 너머로 다음 층만 보인다), 둘레는 돌 벽(안쪽 면만 그림)
            var plate = new Plate("Terrace", -S.TerraceHalf, S.TerraceHalf, -S.TerraceHalf, S.TerraceHalf);
            foreach (var t in S.Terraces)
            {
                foreach (var p in Carve(plate, t.Holes)) { Slab(root, $"Terrace_{t.Y:0}_{p.Name}", stone, p, t.Y); }
            }
            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(root, false);
            for (int i = 0; i < 8; i++)
            {
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(spawns, false);
                sp.transform.localPosition = new Vector3(-28f + i * 8f, S.SpawnY + 2f, 40f);
                sp.AddComponent<LOP.SpawnPoint>().Order = i;
            }
            ShaftWalls(root, "Tower", dark, new Plate("Tower", -S.TerraceHalf, S.TerraceHalf, -S.TerraceHalf, S.TerraceHalf), S.BridgeTopY, S.SpawnY + 60f);

            //  ② 활공 다리 — 둘레는 옅은 빛 벽(보이지 않는 벽 금지), 아래는 레이저 바닥, 상승 기류 셋
            ShaftWalls(root, "Bridge", energy, new Plate("Bridge", S.BridgeXMin, S.BridgeXMax, -S.BridgeZHalf, S.BridgeZHalf), S.LaserFloorY - 10f, S.BridgeTopY);

            //  ③ 섬 — 동굴 입구는 지붕·세 벽으로 덮여 위에서 곧장 못 빠진다(섬에 내려 걸어 들어간다)
            var cave = S.Cave;
            var shaftHole = new Hole((cave.XMin + cave.XMax) * 0.5f, (cave.ZMin + cave.ZMax) * 0.5f, cave.Width, false);
            foreach (var p in Carve(S.Island, new[] { shaftHole })) { Slab(root, $"Island_{p.Name}", stone, p, S.IslandY); }
            var island = S.Island;
            Box(root, "IslandRock", dark, new Vector3((island.XMin + island.XMax) * 0.5f, S.IslandY - 31.5f, -20f), new Vector3(island.Width, 60f, 40f));   // 섬 아랫덩이(동굴 앞쪽만)
            float roofPad = Wall + 1f;
            Box(root, "Roof", stone, new Vector3(shaftHole.X, S.RoofY, shaftHole.Z), new Vector3(cave.Width + roofPad * 2f, Thickness, cave.Depth + roofPad * 2f));
            float entranceH = S.RoofY - S.IslandY, entranceY = (S.RoofY + S.IslandY) * 0.5f;
            Box(root, "Entrance_W", dark, new Vector3(cave.XMin - Wall * 0.5f, entranceY, shaftHole.Z), new Vector3(Wall, entranceH, cave.Depth + Wall * 2f));
            Box(root, "Entrance_E", dark, new Vector3(cave.XMax + Wall * 0.5f, entranceY, shaftHole.Z), new Vector3(Wall, entranceH, cave.Depth + Wall * 2f));
            Box(root, "Entrance_N", dark, new Vector3(shaftHole.X, entranceY, cave.ZMax + Wall * 0.5f), new Vector3(cave.Width + Wall * 2f, entranceH, Wall));

            //  ④ 동굴 — 섬 밑으로 350까지, 턱 넷
            ShaftWalls(root, "Cave", dark, cave, S.CaveBottomY, S.IslandY - 1.5f);
            foreach (var ledge in S.CaveLedges)
            {
                foreach (var p in Carve(cave, ledge.Holes)) { Slab(root, $"Ledge_{ledge.Y:0}_{p.Name}", stone, p, ledge.Y); }
            }

            //  ⑤ 정글 + 제단(결승 판) + 경사로
            Box(root, "Jungle", green, Vector3.zero, new Vector3(S.GroundHalf * 2f, Thickness, S.GroundHalf * 2f));
            var a = S.AltarCenter;
            Box(root, "Altar_0", dark, a + new Vector3(0f, 4f, 0f), new Vector3(40f, 8f, 40f));
            Box(root, "Altar_1", stone, a + new Vector3(0f, 12f, 0f), new Vector3(28f, 8f, 28f));
            Box(root, "Altar_2", dark, a + new Vector3(0f, 19.5f, 0f), new Vector3(S.AltarTopHalf * 2f, 7f, S.AltarTopHalf * 2f));
            //  결승 판 = 이 상자의 렌더러 바운드(FinishLine.Shape). 크기가 있어 그 위에 내려야만 결승이다.
            var top = Box(root, "FinishAltar", EnsureGold(), a + new Vector3(0f, S.AltarTopY - 0.5f, 0f), new Vector3(S.AltarTopHalf * 2f, 1f, S.AltarTopHalf * 2f));
            top.AddComponent<LOP.FinishLine>();
            float rampLen = Mathf.Sqrt(S.RampRun * S.RampRun + S.AltarTopY * S.AltarTopY);
            var ramp = Box(root, "Ramp", stone, a + new Vector3(0f, S.AltarTopY * 0.5f - 0.5f, -S.AltarTopHalf - S.RampRun * 0.5f), new Vector3(S.RampWidth, 1f, rampLen));
            ramp.transform.localRotation = Quaternion.Euler(-S.RampSlopeDegrees, 0f, 0f);

            //  판정 장치
            var lasers = new GameObject("Lasers").transform;
            lasers.SetParent(root, false);
            foreach (var l in S.AllLasers()) { CreateLaserVolume(lasers, l); }
            var doors = new GameObject("Doors").transform;
            doors.SetParent(root, false);
            foreach (var d in S.Doors) { CreateDoorVolume(doors, d, dark); }
            var winds = new GameObject("Winds").transform;
            winds.SetParent(root, false);
            var windAssets = SkydiveWindAssets.EnsureAssets();
            foreach (var w in S.Updrafts) { CreateWindVolume(winds, w.Name, w.Center, w.Radius, w.Height, w.Wind, windAssets); }

            //  세이브 발판 — 보이는 판이 곧 발판(SavePad가 렌더러 바운드를 등록). 내려앉으면 저장, 내가 저장한 것은 빛난다(클라 뷰).
            var padMat = EnsurePadMaterial();
            var padRoot = new GameObject("SavePads").transform;
            padRoot.SetParent(root, false);
            foreach (var pad in S.SavePads)
            {
                var go = Box(padRoot, $"SavePad_{pad.Id}", padMat, new Vector3(pad.X, pad.TopY - S.PadRaise * 0.5f, pad.Z), new Vector3(pad.Half * 2f, S.PadRaise, pad.Half * 2f));
                var marker = go.AddComponent<LOP.SavePad>();
                marker.Id = pad.Id;
                marker.Label = pad.Label;
            }

            CreateCheckpointMarkers(root, S.SpawnY, S.RespawnPoints);
            foreach (var m in root.GetComponentsInChildren<LOP.CheckpointMarker>())
            {
                var pos = m.transform.position;
                if (S.RespawnPoints.TryGetValue(pos.y, out var want) == false || (want - pos).sqrMagnitude > 0.01f)
                {
                    Object.DestroyImmediate(m.gameObject);   // (0, spawnY, 0)에 하나 더 놓이는 표식 — 표의 자리와 다르면 지운다
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SkydiveSpiral] 구웠다 — {ScenePath} (빔 {S.AllLasers().Length}개)");
        }

        internal static string Verify()
        {
            var t = S.Terraces;
            for (int k = 1; k < t.Length; k++)
            {
                float drop = t[k - 1].Y - t[k].Y;
                Hole fPrev = t[k - 1].Holes[0], sPrev = t[k - 1].Holes[1], f = t[k].Holes[0], s = t[k].Holes[1];
                if (Dist(fPrev, f) > S.DiveReach(drop) + f.Half) { return $"{t[k].Y:0} 빠른 구멍에 다이브로 못 닿는다"; }
                if (Dist(sPrev, s) > S.SpreadReach(drop) + s.Half) { return $"{t[k].Y:0} 안전한 구멍에 대자로 못 닿는다"; }
                if (Dist(fPrev, s) <= S.DiveReach(drop) + s.Half) { return $"{t[k].Y:0} 안전한 구멍에 다이브로 닿는다(갈림길 무너짐)"; }
            }
            var ledges = S.CaveLedges;
            for (int i = 1; i < ledges.Length; i++)
            {
                float drop = ledges[i - 1].Y - ledges[i].Y;
                foreach (var up in ledges[i - 1].Holes)
                {
                    foreach (var down in ledges[i].Holes)
                    {
                        if (Dist(up, down) > S.SpreadReach(drop) + down.Half) { return $"동굴 {ledges[i].Y:0} 구멍({down.X:0},{down.Z:0})에 대자로 못 닿는다"; }
                    }
                }
            }
            float finalDrop = ledges[ledges.Length - 1].Y - S.AltarTopY;
            if (S.FinalHorizontal <= S.SpreadReach(finalDrop) + S.AltarTopHalf) { return "제단이 대자로도 닿는다 — 마지막 구간에 패러세일이 필요 없다"; }
            if (S.FinalHorizontal > S.SpreadReach(finalDrop) + 14f * S.GlideBudgetSeconds) { return "제단이 스태미나를 다 써도 못 닿는다"; }
            if (S.RampSlopeDegrees >= 40f) { return $"경사로 {S.RampSlopeDegrees:0}° — 걸어 오를 수 없다(바닥 판정 45°)"; }
            foreach (var y in S.RespawnPoints.Keys)
            {
                if (y > S.LaserFloorY && y <= S.IslandY) { return $"{y:0} 체크포인트 — 섬을 놓쳐도 다리 위로 안 돌아간다"; }
            }
            foreach (var pair in S.RespawnPoints)
            {
                var shelves = t.Concat(ledges).Where(sh => sh.Y == pair.Key).ToArray();
                if (shelves.Length != 1) { return $"{pair.Key:0}에 판이 없다"; }
                foreach (var h in shelves[0].Holes)
                {
                    float r = h.Half + LOP.SkydiveRespawn.SpreadRadius;
                    if (Mathf.Abs(pair.Value.x - h.X) <= r && Mathf.Abs(pair.Value.z - h.Z) <= r) { return $"{pair.Key:0} 부활 지점이 구멍 위"; }
                }
            }
            var padError = FindBadPad();
            if (padError != null) { return padError; }
            return FindTooFastLaser(S.AllLasers());
        }

        /// <summary>발판이 판 위, 구멍 밖, 그리고 빠른 길(위층 빠른 구멍)에서 다이브로 공짜로 밟히지 않는 자리인지.</summary>
        internal static string FindBadPad()
        {
            if (S.SavePads.Select(p => p.Id).Distinct().Count() != S.SavePads.Length) { return "발판 번호가 겹친다"; }
            var t = S.Terraces;
            foreach (var pad in S.SavePads)
            {
                Plate floor;
                Hole[] holes;
                int k = System.Array.FindIndex(t, sh => sh.Y == pad.FloorY);
                var ledge = S.CaveLedges.FirstOrDefault(sh => sh.Y == pad.FloorY);
                if (k >= 0) { floor = new Plate("", -S.TerraceHalf, S.TerraceHalf, -S.TerraceHalf, S.TerraceHalf); holes = t[k].Holes; }
                else if (pad.FloorY == S.IslandY) { floor = S.Island; var c = S.Cave; holes = new[] { new Hole((c.XMin + c.XMax) * 0.5f, (c.ZMin + c.ZMax) * 0.5f, c.Width + 10f, false) }; }
                else if (ledge.Holes != null) { floor = S.Cave; holes = ledge.Holes; }
                else { return $"발판 {pad.Label}: {pad.FloorY:0}에 판이 없다"; }

                if (pad.X - pad.Half < floor.XMin || pad.X + pad.Half > floor.XMax || pad.Z - pad.Half < floor.ZMin || pad.Z + pad.Half > floor.ZMax) { return $"발판 {pad.Label}이 판 밖"; }
                foreach (var h in holes)
                {
                    if (Mathf.Abs(pad.X - h.X) < pad.Half + h.Half + 1f && Mathf.Abs(pad.Z - h.Z) < pad.Half + h.Half + 1f) { return $"발판 {pad.Label}이 구멍({h.X:0},{h.Z:0})에 붙었다"; }
                }
                if (k >= 1)
                {
                    var fast = t[k - 1].Holes[0];
                    float d = new Vector2(pad.X - fast.X, pad.Z - fast.Z).magnitude;
                    if (d <= S.DiveReach(t[k - 1].Y - t[k].Y) + pad.Half) { return $"발판 {pad.Label}이 빠른 길에서 다이브로 공짜로 닿는다"; }
                }
            }
            return null;
        }

        private static float Dist(in Hole a, in Hole b) => new Vector2(a.X - b.X, a.Z - b.Z).magnitude;

        /// <summary>네 벽 — 판정은 상자, 그림은 안쪽 면만(카메라가 벽 밖으로 나가도 안 가린다).</summary>
        private static void ShaftWalls(Transform parent, string name, Material m, in Plate inner, float low, float high)
        {
            float h = high - low, cy = (low + high) * 0.5f, cx = (inner.XMin + inner.XMax) * 0.5f, cz = (inner.ZMin + inner.ZMax) * 0.5f;
            WallFace(parent, $"{name}_W", m, new Vector3(inner.XMin - Wall * 0.5f, cy, cz), new Vector3(Wall, h, inner.Depth + Wall * 2f), -90f, inner.Depth);
            WallFace(parent, $"{name}_E", m, new Vector3(inner.XMax + Wall * 0.5f, cy, cz), new Vector3(Wall, h, inner.Depth + Wall * 2f), 90f, inner.Depth);
            WallFace(parent, $"{name}_S", m, new Vector3(cx, cy, inner.ZMin - Wall * 0.5f), new Vector3(inner.Width + Wall * 2f, h, Wall), 180f, inner.Width);
            WallFace(parent, $"{name}_N", m, new Vector3(cx, cy, inner.ZMax + Wall * 0.5f), new Vector3(inner.Width + Wall * 2f, h, Wall), 0f, inner.Width);
        }

        private static void WallFace(Transform parent, string name, Material m, Vector3 center, Vector3 size, float faceYaw, float faceWidth)
        {
            var go = Box(parent, name, m, center, size);
            go.GetComponent<MeshRenderer>().enabled = false;
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = name + "_Face";
            face.transform.SetParent(parent, false);
            var rot = Quaternion.Euler(0f, faceYaw, 0f);   // Quad 앞면은 -Z — 안쪽을 보게 돌린다
            face.transform.localRotation = rot;
            face.transform.localPosition = center + rot * Vector3.back * (Wall * 0.5f);
            face.transform.localScale = new Vector3(faceWidth, size.y, 1f);
            var r = face.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>다리 둘레 빛 벽 — 옅은 빨강 가산(LOP/LaserGlow). 하늘이 비쳐 보이면서 경계가 읽힌다.</summary>
        private static Material EnergyWallMaterial()
        {
            const string path = "Assets/Art/Materials/Pyramid/SpiralEnergyWall.mat";
            var shader = Shader.Find("LOP/LaserGlow");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_Color", new Color(1f, 0.25f, 0.3f) * 0.18f);
            m.SetFloat("_Falloff", 0f);
            m.SetFloat("_FadeNear", 10000f);   // 벽은 깊이로 옅어지지 않는다(늘 같은 옅기)
            m.SetFloat("_FadeFar", 10001f);
            m.SetFloat("_FadeAbove", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material EnsurePadMaterial()
        {
            const string path = "Assets/Art/Materials/Pyramid/SpiralSavePad.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { return m; }
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.95f));   // 옅은 하늘색 — 저장하면 클라가 민트로 바꿔 빛낸다
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material EnsureGold()
        {
            const string path = "Assets/Art/Materials/Pyramid/SpiralFinishAltar.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { return m; }
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(1f, 0.78f, 0.2f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void Slab(Transform parent, string name, Material m, in Plate p, float y)
        {
            Box(parent, name, m, new Vector3((p.XMin + p.XMax) * 0.5f, y, (p.ZMin + p.ZMax) * 0.5f), new Vector3(p.Width, Thickness, p.Depth));
        }

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
