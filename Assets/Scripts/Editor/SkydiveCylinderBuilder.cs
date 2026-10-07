using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;
using Y = LOP.EditorTools.SkydiveCylinderLayout;

namespace LOP.EditorTools
{
    /// <summary>
    /// 원통 시제품 맵을 표(<see cref="SkydiveCylinderLayout"/>)에서 굽는다. 대화상자를 띄우지 않는다(CLI로 돌린다).
    /// 원판·조리개 날개는 부채꼴 메시(메시 콜라이더), 풍차 날개는 상자 — 보이는 모양이 곧 판정이다.
    /// </summary>
    public static class SkydiveCylinderBuilder
    {
        public const string ScenePath = "Assets/Art/Scenes/SkydiveCylinderMap.unity";
        private const float MinPassage = 6f;   // 몸(0.8) + 넉넉한 조종 여유 — 이보다 좁은 틈은 틈이 아니다

        [MenuItem("LOP/Skydive/원통 시제품 굽기")]
        public static void Build()
        {
            string error = Verify();
            if (error != null)
            {
                Debug.LogError($"[SkydiveCylinder] 검사 실패 — {error}. 굽지 않는다.");
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
            var wallMat = SkydivePyramidDressing.StoneDark;
            var discMat = SkydivePyramidDressing.Toon("HazardDisc", "#C8553D", sideGrid: 3f);
            var irisMat = SkydivePyramidDressing.Toon("HazardIris", "#D98A2B", sideGrid: 3f);
            var millMat = SkydivePyramidDressing.Toon("HazardMill", "#8A5BB0", sideGrid: 3f);
            var root = new GameObject("Course").transform;

            //  원통 벽 — 판정은 상자, 그림은 안쪽 면만(카메라가 벽 밖으로 나가도 안 가린다)
            float wallTop = Y.SpawnY + 60f, wallH = wallTop + Y.Thickness;
            float segLen = 2f * Mathf.PI * (Y.Radius + Y.Wall) / Y.WallSegments + 0.5f;
            var walls = new GameObject("Wall").transform;
            walls.SetParent(root, false);
            for (int k = 0; k < Y.WallSegments; k++)
            {
                float deg = k * 360f / Y.WallSegments;
                var radial = Y.OnCircle(1f, deg, 0f);
                var box = Box(walls, $"Wall_{k}", wallMat, radial * (Y.Radius + Y.Wall * 0.5f) + Vector3.up * (wallH * 0.5f - Y.Thickness),
                              new Vector3(Y.Wall, wallH, segLen), Quaternion.Euler(0f, -deg, 0f));
                box.GetComponent<MeshRenderer>().enabled = false;
                var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(face.GetComponent<Collider>());
                face.name = $"Wall_{k}_Face";
                face.transform.SetParent(walls, false);
                face.transform.localPosition = radial * Y.Radius + Vector3.up * (wallH * 0.5f - Y.Thickness);
                face.transform.localRotation = Quaternion.LookRotation(radial);   // Quad 앞면(-Z)이 안쪽을 본다
                face.transform.localScale = new Vector3(2f * Mathf.PI * Y.Radius / Y.WallSegments + 0.3f, wallH, 1f);
                var r = face.GetComponent<MeshRenderer>();
                r.sharedMaterial = wallMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            //  출발 고리 + 스폰
            MeshBody(root, "Spawn", stone, Sector("CylSpawnRing", Y.SpawnHole, Y.Radius, 0f, 360f, Vector3.zero), new Vector3(0f, Y.SpawnY, 0f));
            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(root, false);
            for (int i = 0; i < 8; i++)
            {
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(spawns, false);
                sp.transform.localPosition = Y.OnCircle(Y.SpawnRingRadius, i * 45f, Y.SpawnY + Y.Thickness * 0.5f + 1f);
                sp.AddComponent<LOP.SpawnPoint>().Order = i;
            }

            //  도는 원판 — 틈 난 부채꼴 하나를 SpinnerVolume이 돌린다
            foreach (var d in Y.Discs)
            {
                var spinner = Hub(root, d.Name, d.Y).AddComponent<LOP.SpinnerVolume>();
                spinner.StartDegrees = d.StartDegrees;
                spinner.DegreesPerTick = d.DegreesPerTick;
                MeshBody(spinner.transform, "Plate", discMat, Sector($"Cyl{d.Name}", 0f, Y.Radius - 1f, d.GapDegrees, 360f, Vector3.zero), Vector3.zero);
            }

            //  조리개 — 날개(부채꼴)마다 닫힌 자리에서 바깥으로 물러난다
            foreach (var iris in Y.Irises)
            {
                var vol = Hub(root, iris.Name, iris.Y).AddComponent<LOP.IrisVolume>();
                vol.Travel = iris.Travel;
                vol.Period = iris.Period; vol.OpenTicks = iris.OpenTicks; vol.MoveTicks = iris.MoveTicks; vol.Phase = iris.Phase;
                var blades = new List<Transform>();
                float w = 360f / iris.Blades;
                for (int b = 0; b < iris.Blades; b++)
                {
                    //  날개 피벗 = 이등분선 방향 1m — 그 방향이 곧 물러나는 방향이다(IrisVolume.Pose).
                    var pivot = Y.OnCircle(1f, (b + 0.5f) * w, 0f);
                    var blade = MeshBody(vol.transform, $"Blade_{b}", irisMat, Sector($"Cyl{iris.Name}_{b}", 0f, Y.Radius - 1f, b * w, (b + 1) * w, pivot), pivot);
                    blades.Add(blade.transform);
                }
                vol.Blades = blades.ToArray();
                vol.Capture();
            }

            //  풍차 날개 — 상자 날개를 SpinnerVolume이 돌린다
            foreach (var m in Y.Windmills)
            {
                var spinner = Hub(root, m.Name, m.Y).AddComponent<LOP.SpinnerVolume>();
                spinner.StartDegrees = m.StartDegrees;
                spinner.DegreesPerTick = m.DegreesPerTick;
                float len = Y.Radius - 2f;
                for (int b = 0; b < m.Blades; b++)
                {
                    float deg = b * 360f / m.Blades;
                    Box(spinner.transform, $"Blade_{b}", millMat, Y.OnCircle(len * 0.5f, deg, 0f), new Vector3(len, Y.Thickness, m.Width), Quaternion.Euler(0f, -deg, 0f));
                }
            }

            //  닫히는 큰 판 — 원통을 막는 판(가운데 40×40 구멍) + 문
            var slab = new Plate("Slab", -Y.Radius - Y.Wall, Y.Radius + Y.Wall, -Y.Radius - Y.Wall, Y.Radius + Y.Wall);
            foreach (var p in Carve(slab, new[] { new Hole(0f, 0f, Y.DoorHole, true) }))
            {
                Box(root, $"DoorSlab_{p.Name}", stone, new Vector3((p.XMin + p.XMax) * 0.5f, Y.DoorSlabY, (p.ZMin + p.ZMax) * 0.5f), new Vector3(p.Width, Y.Thickness, p.Depth), Quaternion.identity);
            }
            var doors = new GameObject("Doors").transform;
            doors.SetParent(root, false);
            CreateDoorVolume(doors, Y.Door, irisMat);

            //  세이브 선반 — 벽의 좁은 턱 + 바닥 높이 발판(충돌 없음)
            var padMat = PadMaterial();
            foreach (var l in Y.Ledges)
            {
                MeshBody(root, $"Ledge_{l.Id}", stone, Sector($"CylLedge_{l.Id}", Y.Radius - l.Depth, Y.Radius + 0.5f, l.CenterDegrees - l.ArcDegrees * 0.5f, l.CenterDegrees + l.ArcDegrees * 0.5f, Vector3.zero),
                         new Vector3(0f, l.Y, 0f));
                var pad = Box(root, $"SavePad_{l.Id}", padMat, l.PadCenter, new Vector3(l.Depth - 1f, 0.06f, l.Depth - 1f), Quaternion.identity);
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                var marker = pad.AddComponent<LOP.SavePad>();
                marker.Id = l.Id;
                marker.Label = l.Label;
            }

            //  바닥 + 결승 판(충돌 없는 판 — 걸어 들어가도 결승)
            MeshBody(root, "Floor", SkydivePyramidDressing.Jungle, Sector("CylFloor", 0f, Y.Radius + Y.Wall, 0f, 360f, Vector3.zero), new Vector3(0f, -Y.Thickness * 0.5f, 0f));
            var finish = Box(root, "FinishPad", Gold(), Y.FinishCenter + Vector3.up * 0.03f, new Vector3(Y.FinishHalf * 2f, 0.06f, Y.FinishHalf * 2f), Quaternion.identity);
            Object.DestroyImmediate(finish.GetComponent<Collider>());
            finish.AddComponent<LOP.FinishLine>();

            //  레이저·바람·체크포인트
            var lasers = new GameObject("Lasers").transform;
            lasers.SetParent(root, false);
            foreach (var l in Y.Lasers) { CreateLaserVolume(lasers, l); }
            var winds = new GameObject("Winds").transform;
            winds.SetParent(root, false);
            var windAssets = SkydiveWindAssets.EnsureAssets();
            foreach (var w in Y.Winds) { CreateWindVolume(winds, w.Name, w.Center, w.Radius, w.Height, w.Wind, windAssets); }

            CreateCheckpointMarkers(root, Y.SpawnY, Y.RespawnPoints);
            foreach (var m in root.GetComponentsInChildren<LOP.CheckpointMarker>())
            {
                var pos = m.transform.position;
                if (Y.RespawnPoints.TryGetValue(pos.y, out var want) == false || (want - pos).sqrMagnitude > 0.01f)
                {
                    Object.DestroyImmediate(m.gameObject);   // (0, spawnY, 0)은 출발 고리 구멍 위라 지운다
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SkydiveCylinder] 구웠다 — {ScenePath}");
        }

        internal static string Verify()
        {
            //  틈 — 가운데 반지름(30)에서 잰 폭이 몸이 조종해 지날 만큼인가
            foreach (var d in Y.Discs)
            {
                float chord = 2f * 30f * Mathf.Sin(d.GapDegrees * 0.5f * Mathf.Deg2Rad);
                if (chord < MinPassage) { return $"{d.Name} 틈이 좁다({chord:0.0}m)"; }
            }
            foreach (var i in Y.Irises)
            {
                if (i.Travel < MinPassage) { return $"{i.Name}이 열려도 구멍이 좁다"; }
                if (i.OpenTicks < 30) { return $"{i.Name} 열린 시간이 너무 짧다"; }
            }
            foreach (var m in Y.Windmills)
            {
                float bladeDeg = 2f * Mathf.Atan2(m.Width * 0.5f, 30f) * Mathf.Rad2Deg;
                float gapDeg = 360f / m.Blades - bladeDeg;
                if (2f * 30f * Mathf.Sin(gapDeg * 0.5f * Mathf.Deg2Rad) < MinPassage) { return $"{m.Name} 날개 사이가 좁다"; }
            }
            //  선반은 도는 것과 높이가 겹치면 쓸린다
            foreach (var l in Y.Ledges)
            {
                foreach (float oy in Y.ObstacleYs())
                {
                    if (Mathf.Abs(l.Y - oy) < 30f) { return $"선반 {l.Label}이 장애물({oy:0})과 너무 가깝다"; }
                }
                foreach (var w in Y.Winds)
                {
                    float r = new Vector2(l.PadCenter.x - w.Center.x, l.PadCenter.z - w.Center.z).magnitude;
                    if (r < w.Radius && Mathf.Abs(l.Y - w.Center.y) < w.Height * 0.5f) { return $"선반 {l.Label}이 바람({w.Name}) 안"; }
                }
            }
            if (Y.FinishCenter.magnitude + Y.FinishHalf * 1.42f > Y.Radius) { return "결승 판이 원통 밖"; }
            return FindTooFastLaser(Y.Lasers);
        }

        // ---- 도우미 ----

        private static GameObject Hub(Transform parent, string name, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            return go;
        }

        /// <summary>메시 몸(그림 + 메시 콜라이더). 위치는 부모 기준.</summary>
        private static GameObject MeshBody(Transform parent, string name, Material m, Mesh mesh, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.layer = LayerMask.NameToLayer("Default");   // 낙하 sweep 마스크가 보는 레이어
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>
        /// 고리 부채꼴 판(두께 = 판 두께, 윗면 y = +두께/2). <paramref name="pivot"/>만큼 꼭짓점을 빼서 그 자리를 원점으로 삼는다.
        /// 각은 도, (cos, 0, sin) 방향. 면마다 꼭짓점을 따로 둬 모서리가 각지게(툰 음영).
        /// </summary>
        internal static Mesh Sector(string assetName, float r0, float r1, float a0, float a1, Vector3 pivot, bool save = true)
        {
            float h = Y.Thickness * 0.5f;
            int steps = Mathf.Max(2, Mathf.CeilToInt((a1 - a0) / 5f));
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = v.Count;
                v.Add(a - pivot); v.Add(b - pivot); v.Add(c - pivot); v.Add(d - pivot);
                for (int k = 0; k < 4; k++) { n.Add(normal); }
                //  유니티 앞면 = Cross(b-a, c-a)가 법선 쪽. 맞으면 그대로, 아니면 뒤집는다.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
                else { t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); }
            }

            Vector3 P(float r, float deg, float y) => Y.OnCircle(r, deg, y);
            for (int s = 0; s < steps; s++)
            {
                float d0 = Mathf.Lerp(a0, a1, (float)s / steps), d1 = Mathf.Lerp(a0, a1, (float)(s + 1) / steps);
                Quad(P(r0, d0, h), P(r1, d0, h), P(r1, d1, h), P(r0, d1, h), Vector3.up);
                Quad(P(r0, d0, -h), P(r0, d1, -h), P(r1, d1, -h), P(r1, d0, -h), Vector3.down);
                var outward = Y.OnCircle(1f, (d0 + d1) * 0.5f, 0f);
                Quad(P(r1, d0, -h), P(r1, d1, -h), P(r1, d1, h), P(r1, d0, h), outward);
                if (r0 > 0.01f) { Quad(P(r0, d0, -h), P(r0, d0, h), P(r0, d1, h), P(r0, d1, -h), -outward); }
            }
            if (a1 - a0 < 359.9f)
            {
                var c0 = Y.OnCircle(1f, a0 - 90f, 0f);   // 시작 끝면은 각이 줄어드는 쪽을 본다
                var c1 = Y.OnCircle(1f, a1 + 90f, 0f);
                Quad(P(r0, a0, -h), P(r1, a0, -h), P(r1, a0, h), P(r0, a0, h), c0);
                Quad(P(r0, a1, -h), P(r0, a1, h), P(r1, a1, h), P(r1, a1, -h), c1);
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(t, 0);
            mesh.SetUVs(0, v.Select(p => new Vector2(p.x, p.z)).ToList());
            mesh.RecalculateBounds();
            return save ? SkydivePyramidDressing.SaveMesh(mesh, assetName) : mesh;
        }

        private static GameObject Box(Transform parent, string name, Material m, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            go.layer = LayerMask.NameToLayer("Default");
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        private static Material PadMaterial()
        {
            const string path = "Assets/Art/Materials/Pyramid/SpiralSavePad.mat";   // 피라미드 개정안과 같은 하늘색 발판
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Material Gold()
        {
            const string path = "Assets/Art/Materials/Pyramid/SpiralFinishAltar.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }
}
