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

        //  한 판을 90초로 잡는다. 전진 6.8 m/s면 612m이고 관문 약 53개다.
        //  <b>맵마다 다를 수 있는 값</b>이다 — 경기 길이는 씬의 결승선 x로 표현되고, 런타임에
        //  60초든 90초든 가정하는 곳은 없다(Archery의 MatchDurationTicks 같은 제한이 없다).
        private const float RaceSeconds = 90f;

        //  이웃한 창의 높이차 상한. 1.67초에 충분히 갈 수 있는 폭이면서, 관문마다 고도를
        //  바꾸게 만들 만큼은 크다.
        private const float MaxGapStep = 6f;

        private const float TickSeconds = 0.02f;
        private const float PipeWidth = 1.6f;        // 기존 막대와 같은 두께
        private const float PipeDepth = 2.5f;        // 판정면 정렬 규약(오브젝트 z -1.25, 콜라이더 center.z +0.5)
        private const float PipeZ = -1.25f;
        private const float WallThickness = 20f;     // 바닥·천장 슬래브 두께 — 밑으로 빠지지 않게 두껍게
        //  중간층 깊이. 34m 거리가 되어 화면 세로 24.8m를 담는다. 게임 평면(z=0)과 배경(z=62)
        //  사이가 통째로 비어 있던 자리다 — 2.5D가 안 읽히던 이유.
        private const float MidgroundZ = 14f;
        private const float MidgroundDepth = 6f;
        private const ulong MidgroundSeed = 20260920UL;

        //  배경 도시. 카메라에서 82m라 화면 세로가 59.7m다 — 기존 건물이 1~6.4m뿐이라
        //  화면의 10%만 채우고 있었다(스카이라인이 아니라 자갈이었다). x도 557m에서 끊겼다.
        private const float SkylineZ = 62f;
        private const float SkylineDepth = 8f;
        private const ulong SkylineSeed = 20260921UL;

        private const float StartX = 0f;
        //  창이 둘인 <b>도전 구간</b>을 코스에 몇 군데 둘 것인가. 구간 하나는 연속 3관문이라
        //  4개면 53관문 중 12개(23%) — 약 150m마다 한 번이다. 기본 맵 느낌은 그대로 두고
        //  "여기선 위험을 걸어 볼까"를 가끔 묻는 정도.
        private const int ChallengeRuns = 4;

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

            //  회랑 중심이 코스를 따라 오르내린다 — 긴 내리막이 다이브를 만든다.
            System.Func<float, float> centerAt =
                x => LOP.MapTools.CourseElevation.CenterY(x, StartX, length);

            var pipes = LOP.MapTools.ClassicCourseRule.Layout(
                StartX, length, spacing, floorY, ceilingY, window, MaxGapStep, Seed, centerAt,
                ChallengeRuns);
            string bad = LOP.MapTools.ClassicCourseRule.Validate(
                pipes, floorY, ceilingY, window, spacing, MaxGapStep, centerAt);
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

            var fallback = FindCourseMaterial();

            //  바닥·천장은 구간마다 끊는다 — 한 덩어리면 색이 안 바뀌어 구간 경계가 바닥에서만
            //  안 보인다. 앞뒤로는 코스 밖(스폰·결승선)까지 덮도록 여유를 준다.
            //  바닥·천장은 <b>고저차를 따라간다</b>. 수평 조각을 겹쳐 놓으면 안 된다 —
            //  높이가 다른 두 조각이 겹치는 구간에서 실효 바닥은 더 높은 쪽, 실효 천장은 더
            //  낮은 쪽이 되어 <b>회랑이 최대 11m까지 먹힌다</b>(창 4.37m보다 좁아져 통과 불가).
            //  실제로 x=203에서 회랑 높이가 0이 됐다.
            //
            //  그래서 조각을 <b>경사로 눕힌다</b>: 양 끝의 회랑 중심을 잇는 현(弦)을 윗면으로
            //  삼아 z축 둘레로 기울인다. 바닥과 천장이 나란한 현이므로 세로 간격(회랑 높이)이
            //  어디서나 일정하다. z축 회전이라 블록의 z 범위가 안 변해 층 규약·시각 정직성
            //  검사에도 영향이 없다.
            int rampCount = (int)System.Math.Ceiling(length / spacing) + 8;
            for (int i = 0; i < rampCount; i++)
            {
                float x0 = StartX - spacing * 4f + spacing * i;
                float x1 = x0 + spacing;
                Material rampSkin = SectionMaterial((x0 + x1) * 0.5f, length, fallback);
                Ramp(composed.transform, $"Floor_{i}", x0, x1, centerAt(x0), centerAt(x1),
                     floorY, below: true, material: rampSkin);
                Ramp(composed.transform, $"Ceiling_{i}", x0, x1, centerAt(x0), centerAt(x1),
                     ceilingY, below: false, material: rampSkin);
            }

            int challengeGates = 0;
            foreach (LOP.MapTools.CoursePipe p in pipes)
            {
                Material skin = SectionMaterial(p.X, length, fallback);
                float lift = centerAt(p.X);
                //  <b>실제</b> 바닥·천장까지 닿아야 한다. 평평한 floorY까지만 그리면 회랑이
                //  내려간 자리에서 파이프 아래에 틈이 생겨 새가 빠져나간다.
                float bottom = floorY + lift - 1f;
                float top = ceilingY + lift + 1f;

                if (p.HasChallenge == false)
                {
                    Pipe(composed.transform, $"PipeLow_{p.X:F0}", p.X,
                         bottom, p.GapCenter - window * 0.5f, skin);
                    Pipe(composed.transform, $"PipeHigh_{p.X:F0}", p.X,
                         p.GapCenter + window * 0.5f, top, skin);
                    continue;
                }

                //  창이 둘인 기둥 — 아래 창, <b>중간 기둥</b>, 위 창 순으로 셋을 세운다.
                //  중간 기둥이 두 창을 실제로 가르는 벽이라, 없으면 그냥 넓은 창 하나가 된다.
                challengeGates++;
                float lowerCenter = System.Math.Min(p.GapCenter, p.ChallengeCenter);
                float upperCenter = System.Math.Max(p.GapCenter, p.ChallengeCenter);
                Pipe(composed.transform, $"PipeLow_{p.X:F0}", p.X,
                     bottom, lowerCenter - window * 0.5f, skin);
                Pipe(composed.transform, $"PipeMid_{p.X:F0}", p.X,
                     lowerCenter + window * 0.5f, upperCenter - window * 0.5f, skin);
                Pipe(composed.transform, $"PipeHigh_{p.X:F0}", p.X,
                     upperCenter + window * 0.5f, top, skin);
            }

            Backdrop(composed.transform, "Midground",
                     LOP.MapTools.BackdropLayout.Midground(StartX, length, MidgroundSeed),
                     MidgroundZ, MidgroundDepth, FlappyCityMaterials.Midground);

            RebuildSkyline(length);

            PlaceSpawns(floorY, ceilingY, window, pipes.Count > 0 ? pipes[0].GapCenter : 0f);
            PlaceFinish(StartX + length + spacing);

            //  이 프로젝트는 결정론 때문에 물리 동기를 직접 관리한다(Physics.autoSyncTransforms를
            //  켜 두지 않는다). 부르지 않으면 콜라이더가 <b>만들 때의 자리</b>에 그대로 있어서,
            //  뒤따르는 질의가 전부 헛것을 본다 — 실제로 검사기가 코스 전체를 y[-0.5~0.5]로 읽고
            //  스폰이 지형 안에 있다고 보고했다(지오메트리는 멀쩡했다).
            Physics.SyncTransforms();

            EditorSceneManagerSave();
            Debug.Log($"[전통 코스] 파이프 {pipes.Count}쌍 · 창 {window:F2}m · 간격 {spacing:F1}m"
                    + $" · 회랑 {corridor:F1}m · 길이 {length:F0}m ({RaceSeconds:F0}초)"
                    + $" · 구간 {FlappyRace.CourseSectionRule.Count}개 ×"
                    + $" {length / FlappyRace.CourseSectionRule.Count:F0}m"
                    + $" · 고저차 ±{LOP.MapTools.CourseElevation.AmpStart:F0}~{LOP.MapTools.CourseElevation.AmpEnd:F0}m"
                    + $" (파장 {LOP.MapTools.CourseElevation.Wavelength:F0}m)"
                    + $" · 도전 관문 {challengeGates}개");
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

        //  두 점의 회랑 중심을 잇는 현을 윗면(바닥) 또는 밑면(천장)으로 삼는 경사 조각.
        //  z축 둘레로만 기울이므로 블록의 z 범위가 변하지 않는다.
        private static void Ramp(Transform parent, string name, float x0, float x1,
                                 float lift0, float lift1, float baseY, bool below, Material material)
        {
            float run = x1 - x0;
            float rise = lift1 - lift0;
            float angle = Mathf.Atan2(rise, run);
            //  현을 다 덮으려면 조각이 기운 만큼 길어야 한다. 1%는 이웃과 겹쳐 틈을 막는 여유.
            float chord = Mathf.Sqrt(run * run + rise * rise) * 1.01f;

            var go = Box(parent, name, material);
            go.transform.localScale = new Vector3(chord, WallThickness, PipeDepth);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);

            //  현의 한가운데에서 조각 두께의 절반만큼 <i>기운 방향의</i> 위/아래로 민다.
            float midX = (x0 + x1) * 0.5f;
            float midY = baseY + (lift0 + lift1) * 0.5f;
            float half = WallThickness * 0.5f * (below ? -1f : 1f);
            go.transform.position = new Vector3(midX - half * Mathf.Sin(angle),
                                                midY + half * Mathf.Cos(angle), PipeZ);
        }

        private static void Pipe(Transform parent, string name, float x, float bottom, float top,
                                 Material material)
        {
            var go = Box(parent, name, material);
            float h = top - bottom;
            go.transform.localScale = new Vector3(PipeWidth, h, PipeDepth);
            go.transform.position = new Vector3(x, bottom + h * 0.5f, PipeZ);
        }

        //  <c>---Environment---</c>의 <c>CitySilhouette</c>만 다시 굽는다. 구름·장식은 손대지 않는다.
        private static void RebuildSkyline(float length)
        {
            var env = GameObject.Find("---Environment---");
            if (env == null)
            {
                Debug.LogWarning("[전통 코스] ---Environment---가 없다 — 배경을 못 구웠다.");
                return;
            }
            Transform city = env.transform.Find("CitySilhouette");
            if (city == null)
            {
                var made = new GameObject("CitySilhouette");
                made.transform.SetParent(env.transform, worldPositionStays: false);
                Undo.RegisterCreatedObjectUndo(made, "Build classic course");
                city = made.transform;
            }
            Undo.RegisterFullObjectHierarchyUndo(city.gameObject, "Build classic course");
            for (int i = city.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(city.GetChild(i).gameObject);
            }
            Backdrop(city, "Skyline",
                     LOP.MapTools.BackdropLayout.Skyline(StartX, length, SkylineSeed),
                     SkylineZ, SkylineDepth, FlappyCityMaterials.Skyline);
        }

        //  게임 평면 뒤에 까는 실루엣. <b>콜라이더를 지운다</b> — 남으면 "안 보이는 벽"이 되고,
        //  그건 플레이어가 원인을 짚을 수 없는 종류의 버그다(🧱 층 규약 검사가 잡는 바로 그것).
        private static void Backdrop(Transform parent, string groupName,
                                     System.Collections.Generic.IReadOnlyList<LOP.MapTools.BackdropBox> boxes,
                                     float z, float depth, Material material)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(parent, worldPositionStays: false);
            Undo.RegisterCreatedObjectUndo(group, "Build classic course");

            foreach (LOP.MapTools.BackdropBox b in boxes)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"{groupName}_{b.X:F0}";
                go.transform.SetParent(group.transform, worldPositionStays: false);
                go.transform.localScale = new Vector3(b.Width, b.Height, depth);
                go.transform.position = new Vector3(b.X, b.CenterY, z);
                //  z축 둘레로만 기울인다 — 다른 축으로 돌리면 z 범위가 변해 층이 섞인다.
                go.transform.rotation = Quaternion.Euler(0f, 0f, b.TiltDegrees);
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
                if (material != null)
                {
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                Undo.RegisterCreatedObjectUndo(go, "Build classic course");
            }
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

        //  구간 재질이 아직 없으면(도시 재질을 안 만들었으면) 그레이박스 재질로 계속 간다 —
        //  색이 다를 뿐 구조 검증에는 지장이 없다.
        private static Material SectionMaterial(float x, float length, Material fallback)
        {
            Material m = FlappyCityMaterials.Of(FlappyRace.CourseSectionRule.Of(x, StartX, length));
            return m != null ? m : fallback;
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
