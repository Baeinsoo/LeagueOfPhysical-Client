using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 맵 씬의 코스를 <b>전통 플래피</b>로 다시 굽는다 — 평평한 회랑에 파이프 쌍만.
    ///
    /// <para><b>손으로 놓지 않는 이유</b>: 창·간격은 물리와 전진 속도에서 나오는 값이라
    /// 그것들을 만지면 따라 움직여야 한다. 미터를 씬에 손으로 박아 두면 조용히 틀려진다
    /// (회랑 하한 4.912가 물리 변경 뒤에도 문서에 남아 있던 것이 그 사고였다).
    /// 그래서 <see cref="LOP.MapTools.GateRhythmRule"/>에서 목표를 읽어 매번 다시 굽는다.</para>
    ///
    /// <para><b>건드리지 않는 것</b>: <c>---Environment---</c>(구름·도시 실루엣 등 바탕)와
    /// 조명. 코스 지오메트리(<c>ComposedMap</c>의 자식)만 통째로 갈아 끼우고, 스폰·결승선은
    /// 자리를 옮긴다.</para>
    /// </summary>
    public static class FlappyClassicCourseBuilder
    {
        //  회랑 높이는 화면 세로와 같게 둔다 — 원본처럼 바닥과 천장이 늘 보여야 어디로 갈지
        //  판단할 수 있다. 카메라 20 · FOV 40에서 화면 세로가 14.56m다.
        private const float CameraDistance = 20f;
        private const float VerticalFov = 40f;

        //  한 판을 60초로 잡는다. 전진 6.8 m/s면 408m이고 관문 36개 — 원본에서 "꽤 잘한 한 판"이다.
        private const float RaceSeconds = 60f;

        //  이웃한 창의 높이차 상한. 1.67초에 충분히 갈 수 있는 폭이면서, 관문마다 고도를
        //  바꾸게 만들 만큼은 크다.
        private const float MaxGapStep = 6f;

        private const float TickSeconds = 0.02f;
        private const float PipeWidth = 1.6f;        // 기존 막대와 같은 두께
        private const float PipeDepth = 2.5f;        // 판정면 정렬 규약(오브젝트 z -1.25, 콜라이더 center.z +0.5)
        private const float PipeZ = -1.25f;
        private const float WallThickness = 20f;     // 바닥·천장 슬래브 두께 — 밑으로 빠지지 않게 두껍게
        private const float StartX = 0f;
        private const ulong Seed = 20260919UL;

        [MenuItem("LOP/Debug/Flappy 전통 코스 굽기")]
        public static void Build()
        {
            if (TryReadConfig(out LOP.MasterData.FlappyConfig config) == false)
            {
                EditorUtility.DisplayDialog("전통 코스 굽기",
                    "MasterData에서 FlappyConfig를 못 읽었다 — 패키지 StreamingAssets를 확인하라.", "확인");
                return;
            }

            var composed = GameObject.Find("ComposedMap");
            if (composed == null)
            {
                EditorUtility.DisplayDialog("전통 코스 굽기",
                    "씬에 ComposedMap이 없다 — 맵 씬(Assets/Art/Scenes/FlappyRaceMap.unity)을 먼저 열어라.", "확인");
                return;
            }

            float window = LOP.MapTools.GateRhythmRule.TargetWindow(
                config.FlapImpulse, config.Gravity, TickSeconds, config.BodyHeight);
            float spacing = LOP.MapTools.GateRhythmRule.TargetSpacing(config.ForwardSpeed);
            float corridor = LOP.MapTools.VisualHonesty.ScreenHalfHeight(CameraDistance, VerticalFov) * 2f;
            float floorY = -corridor * 0.5f;
            float ceilingY = corridor * 0.5f;
            float length = RaceSeconds * config.ForwardSpeed;

            var pipes = LOP.MapTools.ClassicCourseRule.Layout(
                StartX, length, spacing, floorY, ceilingY, window, MaxGapStep, Seed);
            string bad = LOP.MapTools.ClassicCourseRule.Validate(
                pipes, floorY, ceilingY, window, spacing, MaxGapStep);
            if (bad != null)
            {
                //  씬을 건드리기 <b>전에</b> 멈춘다 — 반쯤 구운 코스를 남기지 않는다.
                EditorUtility.DisplayDialog("전통 코스 굽기", "배치가 규칙을 어겼다:\n" + bad, "확인");
                return;
            }

            //  마커를 먼저 빼낸다 — FinishLine이 ComposedMap 자식이라, 그냥 지우면 결승선이
            //  같이 사라진다(실제로 그랬다: 검사기가 "FinishLine 0개"로 멎었다).
            RescueMarkers(composed.transform);

            Undo.RegisterFullObjectHierarchyUndo(composed, "Build classic course");
            for (int i = composed.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(composed.transform.GetChild(i).gameObject);
            }

            var material = FindCourseMaterial();
            Slab(composed.transform, "Floor", StartX, length, floorY - WallThickness * 0.5f,
                 length + spacing * 4f, WallThickness, material);
            Slab(composed.transform, "Ceiling", StartX, length, ceilingY + WallThickness * 0.5f,
                 length + spacing * 4f, WallThickness, material);

            foreach (LOP.MapTools.CoursePipe p in pipes)
            {
                float lowTop = p.GapCenter - window * 0.5f;
                float highBottom = p.GapCenter + window * 0.5f;
                Pipe(composed.transform, $"PipeLow_{p.X:F0}", p.X, floorY, lowTop, material);
                Pipe(composed.transform, $"PipeHigh_{p.X:F0}", p.X, highBottom, ceilingY, material);
            }

            PlaceSpawns(floorY, ceilingY, window, pipes.Count > 0 ? pipes[0].GapCenter : 0f);
            PlaceFinish(StartX + length + spacing);

            //  이 프로젝트는 결정론 때문에 물리 동기를 직접 관리한다(Physics.autoSyncTransforms를
            //  켜 두지 않는다). 부르지 않으면 콜라이더가 <b>만들 때의 자리</b>에 그대로 있어서,
            //  뒤따르는 질의가 전부 헛것을 본다 — 실제로 검사기가 코스 전체를 y[-0.5~0.5]로 읽고
            //  스폰이 지형 안에 있다고 보고했다(지오메트리는 멀쩡했다).
            Physics.SyncTransforms();

            EditorSceneManagerSave();
            Debug.Log($"[전통 코스] 파이프 {pipes.Count}쌍 · 창 {window:F2}m · 간격 {spacing:F1}m"
                    + $" · 회랑 {corridor:F1}m · 길이 {length:F0}m ({RaceSeconds:F0}초)");
        }

        //  코스 지오메트리 안에 섞여 있는 마커(FinishLine·SpawnPoint)를 <c>---Course---</c>
        //  아래로 옮긴다. 마커는 코스가 아니라 <b>규칙</b>이라 다시 구울 때 살아남아야 한다.
        private static void RescueMarkers(Transform composed)
        {
            var home = GameObject.Find("---Course---");
            if (home == null)
            {
                home = new GameObject("---Course---");
                Undo.RegisterCreatedObjectUndo(home, "Build classic course");
            }
            var markers = new System.Collections.Generic.List<Transform>();
            foreach (var f in composed.GetComponentsInChildren<LOP.FinishLine>(includeInactive: true))
            {
                markers.Add(f.transform);
            }
            foreach (var sp in composed.GetComponentsInChildren<LOP.SpawnPoint>(includeInactive: true))
            {
                markers.Add(sp.transform);
            }
            foreach (Transform t in markers)
            {
                Undo.SetTransformParent(t, home.transform, "Build classic course");
            }
        }

        private static void EditorSceneManagerSave()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        //  바닥·천장 슬래브. 코스보다 앞뒤로 넉넉히 뻗어 스폰과 결승선 바깥도 막는다.
        private static void Slab(Transform parent, string name, float startX, float length,
                                 float centerY, float width, float height, Material material)
        {
            var go = Box(parent, name, material);
            go.transform.localScale = new Vector3(width, height, PipeDepth);
            go.transform.position = new Vector3(startX + length * 0.5f, centerY, PipeZ);
        }

        private static void Pipe(Transform parent, string name, float x, float bottom, float top,
                                 Material material)
        {
            var go = Box(parent, name, material);
            float h = top - bottom;
            go.transform.localScale = new Vector3(PipeWidth, h, PipeDepth);
            go.transform.position = new Vector3(x, bottom + h * 0.5f, PipeZ);
        }

        private static GameObject Box(Transform parent, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.layer = LayerMask.NameToLayer("Default");
            //  판정면 정렬 — 그려지는 면은 z -1.25로 당기고 콜라이더는 z 0에 남긴다.
            //  이 보정이 없으면 원근 카메라가 판정보다 뒤에 있는 면을 좁게 그려 틈이 실제보다
            //  좁아 보인다(🎥 시각 정직성 검사가 잡는 바로 그 문제).
            go.GetComponent<BoxCollider>().center = new Vector3(0f, 0f, 0.5f);
            if (material != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            Undo.RegisterCreatedObjectUndo(go, "Build classic course");
            return go;
        }

        //  기존 코스가 쓰던 머티리얼을 그대로 쓴다 — 못 찾으면 기본값으로 두고 계속 간다
        //  (색이 다를 뿐 구조 검증에는 지장이 없다).
        private static Material FindCourseMaterial()
        {
            string[] guids = AssetDatabase.FindAssets("FloorNeutral t:Material");
            if (guids.Length == 0)
            {
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        //  네 자리를 첫 창 높이 언저리에 세로로 편다 — 출발선에서 스폰 높이가 통과를 정하지
        //  않도록 첫 파이프는 한 간격 뒤에 있다(ClassicCourseRule).
        private static void PlaceSpawns(float floorY, float ceilingY, float window, float firstGapCenter)
        {
            var spawns = Object.FindObjectsByType<LOP.SpawnPoint>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None);
            if (spawns.Length == 0)
            {
                Debug.LogWarning("[전통 코스] SpawnPoint 마커가 없다 — 자리를 못 옮겼다.");
                return;
            }
            float step = 1.6f;
            float top = firstGapCenter + (spawns.Length - 1) * step * 0.5f;
            for (int i = 0; i < spawns.Length; i++)
            {
                float y = Mathf.Clamp(top - i * step, floorY + window * 0.5f, ceilingY - window * 0.5f);
                Undo.RecordObject(spawns[i].transform, "Build classic course");
                spawns[i].transform.position = new Vector3(StartX, y, 0f);
            }
        }

        private static void PlaceFinish(float x)
        {
            var finish = Object.FindFirstObjectByType<LOP.FinishLine>(FindObjectsInactive.Include);
            if (finish == null)
            {
                Debug.LogWarning("[전통 코스] FinishLine 마커가 없다 — 자리를 못 옮겼다.");
                return;
            }
            Undo.RecordObject(finish.transform, "Build classic course");
            finish.transform.position = new Vector3(x, 0f, finish.transform.position.z);
        }

        //  코스를 굽는 데 필요한 것은 이 넷뿐이다 — FlappyConfig를 통째로 만들지 않는다
        //  (스턴·대시·추격자 값은 지오메트리와 무관한데 생성자가 전부 요구한다).
        private static bool TryReadConfig(out LOP.MasterData.FlappyConfig row)
        {
            row = null;
            string path = Path.GetFullPath(
                "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes");
            if (File.Exists(path) == false)
            {
                return false;
            }
            row = new LOP.MasterData.TbFlappyConfig(new Luban.ByteBuf(File.ReadAllBytes(path)))
                .GetOrDefault(1);
            return row != null;
        }
    }
}
