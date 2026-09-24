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
        //  판단할 수 있다. 카메라 30 · FOV 40에서 화면 세로가 21.84m다.
        //
        //  <b>왜 30인가</b>: 09-18에 물리를 원본에 맞출 때 "한 탭 정점 ÷ 화면 세로 = 13.4%"로
        //  보정했는데, 그때 쓴 화면 세로 21.84m는 <c>FlappyCameraFollow.fixedZ = -30</c>에서
        //  나온 값이었다. 그 파일은 레거시고 실제 카메라는 20m라, 진짜 화면(14.56m) 기준으로는
        //  20.1% — 원본보다 세로 운동이 1.5배 컸다("중력이 강하다"는 체감의 정체).
        //  카메라를 30으로 올리면 그 보정이 전부 참이 되고, 회랑÷창 비율도 원본과 같은 5.0이 된다.
        private const float CameraDistance = 30f;
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
        private const float ShortcutStripStep = 0.25f;
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
        //  창이 둘인 <b>도전 구간</b>을 코스에 몇 군데 둘 것인가. 구간 하나는 연속 4관문이라
        //  6개면 53관문 중 24개(45%) — 약 100m(15초)마다 한 번이다. 기본 맵 느낌은 살아 있되
        //  "위험을 걸 자리"가 꾸준히 온다.
        private const int ChallengeRuns = 6;

        //  도전 구간을 <b>끝까지</b> 붙어 간 사람에게만 주는 부스트. 구간마다 하나뿐인 이유는
        //  경제다: 0.6초 부스트 = +4.1m이고 충돌 한 번이 −5.4m이니, 패드 하나가 충돌 0.76회를
        //  메운다. 관문마다 놓으면(24개) 코스 612m에서 +98m = 16%라 대시 경제가 통째로 무의미해진다.
        private const float BoostPadDuration = 0.6f;
        //  전진 6.8m/s로 0.22초(11틱) — 틱 사이로 빠질 일이 없다. <b>좁게 두는 이유가 둘</b>이다:
        //  ① 경사 — 패드는 가로로 눕힌 사각형이라 회랑이 기울면 폭이 넓을수록 양 끝이 벽에 가까워진다
        //  (5m로 뒀더니 왼쪽 끝이 바닥에 물렸다). ② <b>부스트가 어디서 시작될지가 폭만큼 흔들린다</b> —
        //  대시는 조종이 안 되는 직선이라, 시작점이 흔들리면 끝나는 자리도 그만큼 흔들린다.
        private const float BoostPadWidth = 1.5f;

        //  패드와 벽 사이에 남길 틈. 콜라이더 표면에 딱 붙으면 검사가 "겹쳤다"로 읽는다.
        private const float BoostPadClearance = 0.15f;

        //  이보다 얇아지면 아예 놓지 않는다. 경사가 급한 자리에서 차선이 벽에 붙어 있으면
        //  패드를 넣을 자리가 안 나오는데, 억지로 넣으면 <b>못 밟는 패드</b>가 된다 —
        //  "있는데 안 되는 것"이 "없는 것"보다 나쁘다.
        private const float BoostPadMinHeight = 1.6f;

        //  지름길 패드의 부스트가 출구보다 이만큼 먼저 끝나야 한다(spec §4).
        private const float ShortcutExitClear = 1.5f;

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

            //  코스는 뾰족한 U·A·계단 조각을 이어 붙인 꺾은선이다(spec 2026-09-24).
            //  앞뒤 여유는 스폰 뒤와 결승선 뒤를 덮는다 — 예전 경사 격자의 여유(앞 4칸·뒤 8칸)와 같다.
            var profile = ComposeProfile(config);
            System.Func<float, float> centerAt = profile.CenterAt;

            System.Func<float, bool> gateAllowed =
                x => profile.GateAllowedAt(x, LOP.MapTools.CourseProfileRule.GateMargin);

            var pipes = LOP.MapTools.ClassicCourseRule.Layout(
                StartX, length, spacing, floorY, ceilingY, window, MaxGapStep, Seed, centerAt,
                ChallengeRuns, gateAllowed);
            string bad = LOP.MapTools.ClassicCourseRule.Validate(
                pipes, floorY, ceilingY, window, spacing, MaxGapStep, centerAt, gateAllowed);
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
            //  바닥·천장은 <b>고저차를 따라간다</b>. 조각은 윗면(바닥) 또는 밑면(천장)이 기운
            //  사각형이고 <b>양 끝은 세로로 곧다</b> — 이웃 조각과 세로 변을 딱 맞대므로 급한
            //  꺾임에서도 틈(V자 홈)이나 겹침 턱이 안 생긴다. 바닥과 천장이 나란하니 회랑 높이는
            //  어디서나 같다.
            //  꺾은선의 꼭짓점마다, 그리고 구간 경계마다 끊는다 — 조각 하나가 곧은 경사 하나라
            //  바닥·천장이 꺾은선에 정확히 놓인다. 구간 경계에서 끊어야 색이 바뀐다.
            float sectionLength = length / FlappyRace.CourseSectionRule.Count;
            var splits = new System.Collections.Generic.List<float>();
            for (int s = 1; s < FlappyRace.CourseSectionRule.Count; s++) { splits.Add(StartX + sectionLength * s); }

            var floorPieces = LOP.MapTools.CourseProfileRule.FloorPieces(profile, splits);
            for (int i = 0; i < floorPieces.Count; i++)
            {
                LOP.MapTools.RampPiece q = floorPieces[i];
                Prism(composed.transform, $"Floor_{i}", new[]
                {
                    new Vector2(q.X0, floorY + q.Lift0 - WallThickness),
                    new Vector2(q.X1, floorY + q.Lift1 - WallThickness),
                    new Vector2(q.X1, floorY + q.Lift1),
                    new Vector2(q.X0, floorY + q.Lift0),
                }, SectionMaterial((q.X0 + q.X1) * 0.5f, length, fallback));
            }
            var ceilingPieces = LOP.MapTools.CourseProfileRule.CeilingPieces(profile, splits);
            for (int i = 0; i < ceilingPieces.Count; i++)
            {
                LOP.MapTools.RampPiece q = ceilingPieces[i];
                Prism(composed.transform, $"Ceiling_{i}", new[]
                {
                    new Vector2(q.X0, ceilingY + q.Lift0),
                    new Vector2(q.X1, ceilingY + q.Lift1),
                    new Vector2(q.X1, ceilingY + q.Lift1 + WallThickness),
                    new Vector2(q.X0, ceilingY + q.Lift0 + WallThickness),
                }, SectionMaterial((q.X0 + q.X1) * 0.5f, length, fallback));
            }

            //  지름길 구간의 천장은 경사 조각이 아니라 두 덩어리다: 지붕, 지름길과 계곡 사이의 혀.
            int shortcutPads = Shortcuts(composed.transform, profile, config, length, fallback);

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
                //  두 창의 <b>폭이 다르다</b> — 도전 창이 넓다(브레이크가 없어 도착이 빠르므로).
                //  그래서 어느 쪽이 도전 창인지 보고 반폭을 골라야 한다.
                bool challengeIsLower = p.ChallengeCenter < p.GapCenter;
                float lowerCenter = challengeIsLower ? p.ChallengeCenter : p.GapCenter;
                float upperCenter = challengeIsLower ? p.GapCenter : p.ChallengeCenter;
                float lowerHalf = (challengeIsLower ? LOP.MapTools.ClassicCourseRule.ChallengeWindowFor(window) : window) * 0.5f;
                float upperHalf = (challengeIsLower ? window : LOP.MapTools.ClassicCourseRule.ChallengeWindowFor(window)) * 0.5f;

                Pipe(composed.transform, $"PipeLow_{p.X:F0}", p.X,
                     bottom, lowerCenter - lowerHalf, skin);
                Pipe(composed.transform, $"PipeMid_{p.X:F0}", p.X,
                     lowerCenter + lowerHalf, upperCenter - upperHalf, skin);
                Pipe(composed.transform, $"PipeHigh_{p.X:F0}", p.X,
                     upperCenter + upperHalf, top, skin);
            }

            int boostPads = BoostPads(composed.transform, pipes, window, centerAt, spacing,
                                      floorY, ceilingY, fallback);

            Backdrop(composed.transform, "Midground",
                     LOP.MapTools.BackdropLayout.Midground(StartX, length, MidgroundSeed),
                     MidgroundZ, MidgroundDepth, FlappyCityMaterials.Midground, centerAt);

            RebuildSkyline(length);

            PlaceSpawns(floorY, ceilingY, window, pipes.Count > 0 ? pipes[0].GapCenter : 0f);
            PlaceFinish(StartX + length + spacing, centerAt);

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
                    + $" · 높낮이 {profile.MinY:F0}~{profile.MaxY:F0}m (조각 꼭짓점 {profile.VertexCount}개)"
                    + $" · 지름길 {profile.Shortcuts.Count}개 (패드 {shortcutPads}개)"
                    + $" · 도전 관문 {challengeGates}개"
                    + $" · 부스트 패드 {boostPads}개 ({BoostPadDuration:F1}초)");
        }

        /// <summary>굽기와 같은 코스 프로필. 에디터 측정(eval)이 씬과 같은 기하를 다시 얻을 때 쓴다.</summary>
        public static LOP.MapTools.CourseProfile ComposeProfile(LOP.MasterData.FlappyConfig config)
        {
            float window = LOP.MapTools.GateRhythmRule.TargetWindow(
                config.FlapImpulse, config.Gravity, TickSeconds, config.BodyHeight);
            float spacing = LOP.MapTools.GateRhythmRule.TargetSpacing(config.ForwardSpeed);
            float ceilingY = LOP.MapTools.VisualHonesty.ScreenHalfHeight(CameraDistance, VerticalFov);
            float length = RaceSeconds * config.ForwardSpeed;
            return LOP.MapTools.CourseProfileRule.Compose(
                StartX, length, spacing, ceilingY, window, Seed,
                leadIn: spacing * 4f, tail: spacing * 8f,
                arc: new LOP.MapTools.FlapArc(config.FlapImpulse, config.Gravity, config.ForwardSpeed, TickSeconds));
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

        //  지름길 하나 = 지붕 띠 + 혀 띠(굴을 판 덩어리를 세로로 자른 볼록 사각형) + 패드 + 검사기용 표시.
        //  표시는 <b>Transform만 있는</b> 빈 GameObject다 — 맵 씬은 서버도 읽으므로 클라 전용 컴포넌트를
        //  붙이면 서버에서 missing script가 되어 씬 주입이 끊긴다.
        private static int Shortcuts(Transform parent, LOP.MapTools.CourseProfile profile,
                                     LOP.MasterData.FlappyConfig config, float length, Material fallback)
        {
            int pads = 0;
            float span = BoostPadDuration * config.ForwardSpeed * config.DashMult;
            foreach (LOP.MapTools.ShortcutRect r in profile.Shortcuts)
            {
                float mid = (r.X0 + r.X1) * 0.5f;
                Material skin = SectionMaterial(mid, length, fallback);

                var roof = LOP.MapTools.CourseProfileRule.ShortcutRoof(r, WallThickness, ShortcutStripStep);
                for (int i = 0; i < roof.Count; i++)
                {
                    Prism(parent, $"ShortcutRoof_{r.X0:F0}_{i}", ToPolygon(roof[i]), skin);
                }
                var tongue = LOP.MapTools.CourseProfileRule.ShortcutTongue(r, ShortcutStripStep);
                for (int i = 0; i < tongue.Count; i++)
                {
                    Prism(parent, $"ShortcutTongue_{r.X0:F0}_{i}", ToPolygon(tongue[i]), skin);
                }
                Debug.Log($"[전통 코스] 지름길 x={r.X0:F0}: 호 {r.Entrance.Arcs}개 · 굴 {r.Entrance.Thickness:F1}m · 턱 {r.Entrance.Lip:F0}m · 굴 끝 {r.ChannelEnd:F1} · 출구 {r.X1:F1}");

                var marker = new GameObject($"Shortcut_{r.X0:F0}");
                marker.transform.SetParent(parent, worldPositionStays: false);
                marker.transform.position = new Vector3(mid, r.CenterY, 0f);
                marker.transform.localScale = new Vector3(r.X1 - r.X0, r.Y1 - r.Y0, 1f);
                Undo.RegisterCreatedObjectUndo(marker, "Build classic course");

                float? padX = LOP.MapTools.ShortcutRule.PadCenterX(r, span, BoostPadWidth, ShortcutExitClear);
                if (padX.HasValue == false)
                {
                    Debug.LogWarning($"[전통 코스] x={r.X0:F0} 지름길이 짧아 패드를 못 놓았다 ({r.Length:F1}m)");
                    continue;
                }
                float padY = r.CenterY;
                float padHeight = LOP.MapTools.BoostPadRule.Fit(
                    ref padY, r.Y1 - r.Y0, r.Y0 + BoostPadClearance, r.Y1 - BoostPadClearance);
                BoostPad(parent, $"BoostPad_{padX.Value:F0}", padX.Value, padY, padHeight, fallback);
                pads++;
            }
            return pads;
        }

        //  띠(8개 수)를 다각형으로. 혀 끝 띠는 위·아래가 만나 삼각형이라 겹친 점을 뺀다.
        private static Vector2[] ToPolygon(float[] strip)
        {
            var points = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < strip.Length; i += 2)
            {
                var p = new Vector2(strip[i], strip[i + 1]);
                if (points.Count > 0 && (points[points.Count - 1] - p).sqrMagnitude < 1e-8f) { continue; }
                points.Add(p);
            }
            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude < 1e-8f)
            {
                points.RemoveAt(points.Count - 1);
            }
            return points.ToArray();
        }

        //  z로 돌출한 볼록 다각형. 그려지는 면은 z [−2.5, 0], 콜라이더는 z [−1.25, +1.25] —
        //  Box()가 지키는 판정면 정렬 규약과 같다(원근 카메라가 틈을 좁게 그리지 않게).
        private static GameObject Prism(Transform parent, string name, Vector2[] polygon, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.layer = LayerMask.NameToLayer("Default");
            go.AddComponent<MeshFilter>().sharedMesh = PrismMesh(name, polygon, PipeZ - PipeDepth * 0.5f, PipeZ + PipeDepth * 0.5f);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = PrismMesh(name + "_Collider", polygon, -PipeDepth * 0.5f, PipeDepth * 0.5f);
            //  오목(비볼록) 메시 콜라이더는 속이 빈 껍데기라, 안쪽에 완전히 들어간 구체는 겹침
            //  검사(CheckSphere/OverlapSphere류)에 안 걸린다(표면을 스치는 CapsuleCast/Raycast는
            //  걸린다). 볼록으로 두면 속이 찬 덩어리가 된다 — 혀·바닥·천장 조각은 늘 볼록 사다리꼴이라 모양이
            //  바뀌지 않는다.
            collider.convex = true;
            Undo.RegisterCreatedObjectUndo(go, "Build classic course");
            return go;
        }

        //  면마다 꼭짓점을 따로 둔다 — 모서리가 각지게 빛받아야 벽으로 읽힌다(공유하면 뭉개진다).
        private static Mesh PrismMesh(string name, Vector2[] poly, float zNear, float zFar)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            int n = poly.Length;

            //  앞면(카메라 쪽, z가 작은 쪽). 다각형은 반시계인데 −z에서 봐도 그대로 반시계로 보인다 —
            //  그래서 인덱스 순서를 뒤집어야 시계 방향이 되어 유니티 앞면 규칙(카메라에서 봤을 때
            //  시계 방향인 면이 앞면)에 맞는다.
            int front = vertices.Count;
            for (int i = 0; i < n; i++) { vertices.Add(new Vector3(poly[i].x, poly[i].y, zNear)); }
            for (int i = 1; i < n - 1; i++) { triangles.Add(front); triangles.Add(front + i + 1); triangles.Add(front + i); }

            int back = vertices.Count;
            for (int i = 0; i < n; i++) { vertices.Add(new Vector3(poly[i].x, poly[i].y, zFar)); }
            for (int i = 1; i < n - 1; i++) { triangles.Add(back); triangles.Add(back + i); triangles.Add(back + i + 1); }

            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                int s = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, zNear));
                vertices.Add(new Vector3(b.x, b.y, zNear));
                vertices.Add(new Vector3(b.x, b.y, zFar));
                vertices.Add(new Vector3(a.x, a.y, zFar));
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 도전 구간마다 <b>마지막 관문 바로 뒤</b>에 부스트 패드를 하나 둔다 — 그 자리에 있으려면
        /// 위험한 쪽 창을 실제로 통과했어야 하므로, "위험을 고른 쪽이 거리로 보상받는다"가 성립한다.
        ///
        /// <para>구간 중간이 아니라 끝인 이유: 중간에 두면 마지막 한 관문만 도전 창으로 넘어도
        /// 받는다. 끝에 두면 그 구간을 붙어 간 사람이 받는다.</para>
        /// </summary>
        private static int BoostPads(Transform parent, System.Collections.Generic.IReadOnlyList<LOP.MapTools.CoursePipe> pipes,
                                     float window, System.Func<float, float> centerAt, float spacing,
                                     float floorY, float ceilingY, Material fallback)
        {
            int placed = 0;
            for (int i = 0; i < pipes.Count; i++)
            {
                if (pipes[i].HasChallenge == false)
                {
                    continue;
                }
                bool runEnd = i + 1 >= pipes.Count || pipes[i + 1].HasChallenge == false;
                if (runEnd == false)
                {
                    continue;
                }

                //  <b>구간 안</b>, 마지막 두 도전 관문 사이에 놓는다. 구간 <i>뒤</i>에 놓았더니
                //  전부 함정이 됐다 — 대시는 조종이 안 되는 수평 직선인데(중력·날갯짓 없음),
                //  그 앞에 오는 것이 차선이 다른 <b>평범한 관문</b>이라 8.2m를 날아가 그대로 박았다
                //  (6개 중 6개, 4.4m 앞에서 막힘. 2026-09-24 실측).
                //
                //  구간 안은 앞뒤가 <b>같은 차선</b>이라 높이 차가 작다. 그래서 패드를 <b>다음 도전
                //  관문의 창 높이</b>에 두면 부스트가 그 창을 통과시켜 준다 — 벌이 아니라 상이 된다.
                if (i < 1 || pipes[i - 1].HasChallenge == false)
                {
                    continue;   // 구간이 하나짜리라 안에 놓을 자리가 없다
                }
                float padX = (pipes[i - 1].X + pipes[i].X) * 0.5f;
                //  회랑 기울기를 따라가지 <b>않는다</b>. 이 패드의 존재 이유가 "다음 창에 정렬시키는
                //  것"이라, 창의 절대 높이를 그대로 써야 부스트가 그 창을 지나간다.
                float padY = pipes[i].ChallengeCenter;
                float padHeight = LOP.MapTools.ClassicCourseRule.ChallengeWindowFor(window);

                //  <b>패드 폭 전체</b>에서 회랑의 가장 좁은 곳에 맞춘다. 가운데 한 점만 보면
                //  기운 자리에서 양 끝이 벽을 파고든다 — 고정 여백으로는 못 막는다(경사가
                //  자리마다 다르므로). 도전 창은 회랑 벽에 붙어 있어 여유가 없는 쪽이다.
                float highestFloor = float.MinValue;
                float lowestCeiling = float.MaxValue;
                for (int step = 0; step <= 8; step++)
                {
                    float sampleX = padX + BoostPadWidth * (step / 8f - 0.5f);
                    //  바닥·천장이 꺾은선 꼭짓점에서 끊기므로 실제 면이 곧 centerAt이다.
                    float lift = centerAt(sampleX);
                    highestFloor = Mathf.Max(highestFloor, floorY + lift);
                    lowestCeiling = Mathf.Min(lowestCeiling, ceilingY + lift);
                }
                padHeight = LOP.MapTools.BoostPadRule.Fit(
                    ref padY, padHeight,
                    highestFloor + BoostPadClearance, lowestCeiling - BoostPadClearance);

                if (padHeight < BoostPadMinHeight)
                {
                    Debug.LogWarning($"[전통 코스] x={padX:F0} 부스트 패드를 걸렀다 — 회랑이"
                                   + $" {padHeight:F2}m밖에 안 남았다(최소 {BoostPadMinHeight:F1}m)."
                                   + " 그 자리의 도전 차선이 벽에 너무 붙어 있다.");
                    continue;
                }

                BoostPad(parent, $"BoostPad_{padX:F0}", padX, padY, padHeight, fallback);
                placed++;
            }
            return placed;
        }

        //  콜라이더가 없다 — 판정은 <c>FlappyBoostPadField</c>가 산술로 한다(트리거로 하면
        //  롤백 재생에서 물리를 안 돌려 아예 답이 없다). 그려지는 크기가 곧 판정 사각형이라
        //  🎥 시각 정직성이 유지된다.
        private static void BoostPad(Transform parent, string name, float x, float y, float height,
                                     Material fallback)
        {
            Material skin = FlappyCityMaterials.Boost != null ? FlappyCityMaterials.Boost : fallback;
            var go = Box(parent, name, skin);
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.transform.localScale = new Vector3(BoostPadWidth, height, PipeDepth);
            go.transform.position = new Vector3(x, y, PipeZ);

            var pad = go.AddComponent<LOP.FlappyBoostPad>();
            pad.Width = BoostPadWidth;
            pad.Height = height;
            pad.Duration = BoostPadDuration;
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
            //  스카이라인은 일부러 y=0에 둔다 — 코스 높이는 약 −40…+20m(60m)를 오르내리지만,
            //  스카이라인은 82m 뒤에 있고 안개에 묻혀 높이가 안 맞아도 눈에 안 띈다.
            Backdrop(city, "Skyline",
                     LOP.MapTools.BackdropLayout.Skyline(StartX, length, SkylineSeed),
                     SkylineZ, SkylineDepth, FlappyCityMaterials.Skyline, x => 0f);
        }

        //  게임 평면 뒤에 까는 실루엣. <b>콜라이더를 지운다</b> — 남으면 "안 보이는 벽"이 되고,
        //  그건 플레이어가 원인을 짚을 수 없는 종류의 버그다(🧱 층 규약 검사가 잡는 바로 그것).
        private static void Backdrop(Transform parent, string groupName,
                                     System.Collections.Generic.IReadOnlyList<LOP.MapTools.BackdropBox> boxes,
                                     float z, float depth, Material material,
                                     System.Func<float, float> liftAt)
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
                //  중간층은 코스 높이를 따라간다 — 40m 계곡에 내려가면 평지 기준 건물이 화면 위로 사라진다.
                go.transform.position = new Vector3(b.X, b.CenterY + liftAt(b.X), z);
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
        //  y=0을 그대로 쓴다 — 결승선과 달리 스폰은 늘 StartX(꺾은선의 시작점)에 있고, 프로필은
        //  거기서 항상 0으로 시작한다(CourseProfileRule.Compose가 y=0에서 출발).
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

        //  y를 0으로 고정하지 않는다 — 계단 때문에 코스가 0이 아닌 높이에서 끝날 수 있다.
        //  그 x의 실제 회랑 중심(centerAt)에 세워야 결승선이 바닥·천장 사이에 온전히 온다.
        private static void PlaceFinish(float x, System.Func<float, float> centerAt)
        {
            var finish = Object.FindFirstObjectByType<LOP.FinishLine>(FindObjectsInactive.Include);
            if (finish == null)
            {
                Debug.LogWarning("[전통 코스] FinishLine 마커가 없다 — 자리를 못 옮겼다.");
                return;
            }
            Undo.RecordObject(finish.transform, "Build classic course");
            finish.transform.position = new Vector3(x, centerAt(x), finish.transform.position.z);
        }

        //  코스를 굽는 데 필요한 것은 이 넷뿐이다 — FlappyConfig를 통째로 만들지 않는다
        //  (스턴·대시·추격자 값은 지오메트리와 무관한데 생성자가 전부 요구한다).
        public static bool TryReadConfig(out LOP.MasterData.FlappyConfig row)
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
