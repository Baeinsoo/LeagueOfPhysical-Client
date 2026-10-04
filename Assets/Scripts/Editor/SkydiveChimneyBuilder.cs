using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;
using C = LOP.EditorTools.SkydiveChimneyLayout;

namespace LOP.EditorTools
{
    /// <summary>
    /// 굴뚝 시제품 맵을 표(<see cref="SkydiveChimneyLayout"/>)에서 굽는다 — 회색 블록, 규칙 하나(촘촘한 그물 → 자세 선택).
    /// 대화상자를 띄우지 않는다(CLI로 돌린다). 검사가 걸리면 씬을 건드리지 않고 콘솔에 남긴다.
    /// </summary>
    public static class SkydiveChimneyBuilder
    {
        public const string ScenePath = "Assets/Art/Scenes/SkydiveChimneyMap.unity";
        private const float Thickness = 3f;

        [MenuItem("LOP/Skydive/굴뚝 시제품 굽기")]
        public static void Build()
        {
            string error = Verify();
            if (error != null)
            {
                Debug.LogError($"[SkydiveChimney] 검사 실패 — {error}. 굽지 않는다.");
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
            var root = new GameObject("Course").transform;
            var floor = new Plate("Floor", -C.Half, C.Half, -C.Half, C.Half);

            //  스폰 판 — 남쪽이 뚫려 있고 북쪽 줄에서 출발
            foreach (var p in Carve(floor, new[] { C.SpawnOpening }))
            {
                Slab(root, $"Spawn_{p.Name}", stone, p, C.SpawnY);
            }
            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(root, false);
            for (int i = 0; i < 8; i++)
            {
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(spawns, false);
                sp.transform.localPosition = new Vector3(-14f + i * 4f, C.SpawnY + 2f, 14f);
                sp.AddComponent<LOP.SpawnPoint>().Order = i;
            }

            //  굴뚝 벽 — 돌 쌓기 무늬가 지나가며 속도를 보여 준다. 그림자는 끈다(안이 까매진다).
            float top = C.SpawnY + 40f, h = top - C.ExitY, cy = (top + C.ExitY) * 0.5f, outer = C.Half + C.Wall * 0.5f, len = C.Half * 2f + C.Wall * 2f;
            Wall(root, "Wall_W", dark, new Vector3(-outer, cy, 0f), new Vector3(C.Wall, h, len), -90f);
            Wall(root, "Wall_E", dark, new Vector3(outer, cy, 0f), new Vector3(C.Wall, h, len), 90f);
            Wall(root, "Wall_S", dark, new Vector3(0f, cy, -outer), new Vector3(len, h, C.Wall), 180f);
            Wall(root, "Wall_N", dark, new Vector3(0f, cy, outer), new Vector3(len, h, C.Wall), 0f);

            //  중간 턱 — 구석 구멍 하나
            foreach (var p in Carve(floor, new[] { C.LedgeHole }))
            {
                Slab(root, $"Ledge_{p.Name}", stone, p, C.LedgeY);
            }

            //  착지 마당 + 결승
            Box(root, "Ground", green, Vector3.zero, new Vector3(C.GroundHalf * 2f, Thickness, C.GroundHalf * 2f));
            var finish = new GameObject("FinishLine");
            finish.transform.SetParent(root, false);
            finish.transform.localPosition = new Vector3(0f, Thickness * 0.5f, 0f);
            finish.AddComponent<LOP.FinishLine>();

            var lasers = new GameObject("Lasers").transform;
            lasers.SetParent(root, false);
            foreach (var l in C.Lasers()) { CreateLaserVolume(lasers, l); }

            //  창 테두리 — 그물은 촘촘한 빨간 선이라 구멍이 묻힌다. 노랗게 빛나는 틀로 "여기로"를 멀리서 보이게.
            //  틀은 창 바로 바깥 빔 자리(판정 경계)에 둔다 — 틀 안쪽이 곧 지나가는 칸이다. 충돌체 없음.
            var frameMat = WindowFrameMaterial();
            var frames = new GameObject("WindowFrames").transform;
            frames.SetParent(root, false);
            float e = C.WindowHalf + 0.4f, t = 0.25f;
            foreach (var n in C.Nets)
            {
                var c = new Vector3(n.X, n.Y + 0.3f, n.Z);
                Frame(frames, $"Frame_{n.Y:0}_N", frameMat, c + new Vector3(0f, 0f, e), new Vector3(e * 2f + t, t, t));
                Frame(frames, $"Frame_{n.Y:0}_S", frameMat, c + new Vector3(0f, 0f, -e), new Vector3(e * 2f + t, t, t));
                Frame(frames, $"Frame_{n.Y:0}_E", frameMat, c + new Vector3(e, 0f, 0f), new Vector3(t, t, e * 2f + t));
                Frame(frames, $"Frame_{n.Y:0}_W", frameMat, c + new Vector3(-e, 0f, 0f), new Vector3(t, t, e * 2f + t));
            }

            CreateCheckpointMarkers(root, C.SpawnY, C.RespawnPoints);
            //  CreateCheckpointMarkers는 (0, spawnY, 0)에 표식을 하나 더 둔다 — 여기는 스폰 판 구멍 위라 지운다.
            foreach (var m in root.GetComponentsInChildren<LOP.CheckpointMarker>())
            {
                var pos = m.transform.position;
                if (C.RespawnPoints.TryGetValue(pos.y, out var want) == false || (want - pos).sqrMagnitude > 0.01f)
                {
                    Object.DestroyImmediate(m.gameObject);
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SkydiveChimney] 구웠다 — {ScenePath} (빔 {C.Lasers().Length}개)");
        }

        internal static string Verify()
        {
            foreach (var n in C.Nets)
            {
                if (Mathf.Abs(n.X) + C.WindowHalf > C.Half - 0.5f || Mathf.Abs(n.Z) + C.WindowHalf > C.Half - 0.5f) { return $"{n.Y:0} 창이 굴뚝 밖"; }
                if (n.Y >= C.SpawnY || n.Y <= C.ExitY || Mathf.Abs(n.Y - C.LedgeY) < 5f) { return $"{n.Y:0} 그물 높이가 판과 겹친다"; }
            }
            foreach (var s in C.Transitions())
            {
                if (s.Kind == C.StepKind.Fall && C.SpreadReach(s.Drop) < s.Distance) { return $"{s.FromY:0}→{s.ToY:0} 낙하 칸인데 대자로 못 닿는다(애매한 칸)"; }
                if (s.Kind == C.StepKind.Glide && C.GlideReach(s.Drop) < s.Distance + C.WindowHalf * 2f) { return $"{s.FromY:0}→{s.ToY:0} 패러세일로도 못 닿는다"; }
            }
            foreach (var (fromY, seconds) in C.GlideSecondsPerSection())
            {
                if (seconds > C.GlideBudgetSeconds) { return $"{fromY:0} 구간 패러세일 {seconds:0.0}초 — 스태미나 {C.GlideBudgetSeconds:0}초를 넘는다"; }
            }
            foreach (var pair in C.RespawnPoints)
            {
                var hole = pair.Key == C.SpawnY ? C.SpawnOpening : C.LedgeHole;
                float r = hole.Half + LOP.SkydiveRespawn.SpreadRadius;
                if (Mathf.Abs(pair.Value.x - hole.X) <= r && Mathf.Abs(pair.Value.z - hole.Z) <= r) { return $"{pair.Key:0} 부활 지점이 구멍 위"; }
                if (Mathf.Abs(pair.Value.x) > C.Half - 2f || Mathf.Abs(pair.Value.z) > C.Half - 2f) { return $"{pair.Key:0} 부활 지점이 판 밖"; }
            }
            var lasers = C.Lasers();
            if (lasers.Select(l => l.Name).Distinct().Count() != lasers.Length) { return "빔 이름이 겹친다"; }
            return FindTooFastLaser(lasers);
        }

        /// <summary>창 테두리 — 레이저와 같은 셰이더(LOP/LaserGlow)라 깊이에 따라 같이 옅어진다(겹친 그물에서 어느 창이 바로 아래인지).</summary>
        private static Material WindowFrameMaterial()
        {
            const string path = "Assets/Art/Materials/Pyramid/ChimneyWindowFrame.mat";
            var shader = Shader.Find("LOP/LaserGlow");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_Color", new Color(1f, 0.8f, 0.2f) * 1.6f);   // HDR — 블룸에 빛난다(1을 넘되 옅어짐이 보일 만큼만)
            m.SetFloat("_Falloff", 0f);                                // 납작한 막대라 고르게
            LOP.SkydiveLaserView.ApplyFade(m);                          // 레이저와 같은 깊이 옅어짐
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Frame(Transform parent, string name, Material m, Vector3 center, Vector3 size)
        {
            var go = Box(parent, name, m, center, size);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 보이기만 — 부딪히면 그물 위에 선다
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Slab(Transform parent, string name, Material m, in Plate p, float y)
        {
            Box(parent, name, m, new Vector3((p.XMin + p.XMax) * 0.5f, y, (p.ZMin + p.ZMax) * 0.5f), new Vector3(p.Width, Thickness, p.Depth));
        }

        /// <summary>
        /// 판정은 상자, 그림은 안쪽 면 하나(Quad). 카메라가 최대 20m 뒤라 굴뚝(폭 40) 벽 가까이에선 밖으로 나간다 —
        /// 상자를 그리면 바깥 면이 화면을 막는다. 안쪽 면만 그리면 밖에서는 뒷면이라 안 보인다.
        /// </summary>
        private static void Wall(Transform parent, string name, Material m, Vector3 center, Vector3 size, float faceYaw)
        {
            var go = Box(parent, name, m, center, size);
            go.GetComponent<MeshRenderer>().enabled = false;

            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = name + "_Face";
            face.transform.SetParent(parent, false);
            //  Quad 앞면은 -Z를 본다 — yaw로 굴뚝 안쪽을 보게 돌리고, 벽 안쪽 면 자리에 둔다.
            var rot = Quaternion.Euler(0f, faceYaw, 0f);
            var inward = rot * Vector3.back;
            face.transform.localRotation = rot;
            face.transform.localPosition = center + inward * (C.Wall * 0.5f);
            face.transform.localScale = new Vector3(C.Half * 2f, size.y, 1f);
            var r = face.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
