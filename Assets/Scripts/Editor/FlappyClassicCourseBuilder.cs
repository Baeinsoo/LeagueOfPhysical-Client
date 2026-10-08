using System.Collections.Generic;
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
    /// 자리를 옮긴다. 바탕은 도시 실루엣만 다시 굽고, 광산 굽기가 꺼 둔 바탕을 다시 켠다.</para>
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

        //  한 판을 110초로 잡는다. 전진 6.8 m/s면 748m다(2026-09-28 묶음 2 — 구간마다 새 지형 하나를
        //  얹을 자리). <b>맵마다 다를 수 있는 값</b>이다 — 경기 길이는 씬의 결승선 x로 표현되고, 런타임에
        //  몇 초를 가정하는 곳은 없다. 서버 판 상한은 150초(FlappyRaceRuleSystem).
        private const float RaceSeconds = 110f;

        //  이웃한 창의 높이차 상한. 1.67초에 충분히 갈 수 있는 폭이면서, 관문마다 고도를
        //  바꾸게 만들 만큼은 크다.
        private const float MaxGapStep = 6f;

        private const float TickSeconds = 0.02f;
        private const float PipeWidth = 1.6f;        // 기존 막대와 같은 두께
        private const float PipeDepth = 2.5f;        // 판정면 정렬 규약(오브젝트 z -1.25, 콜라이더 center.z +0.5)
        private const float PipeZ = -1.25f;
        private const float ShortcutStripStep = 0.25f;
        internal const float WallThickness = 20f;     // 바닥·천장 슬래브 두께 — 밑으로 빠지지 않게 두껍게
        //  절벽 면의 x 두께. WallThickness(20m)만큼 번지면 절벽 20m 앞에 계곡·샤프트 구멍이 오는
        //  시드·길이 조합에서 그 구멍을 조용히 메워 버린다 — 아래 Cliffs() 참고.
        private const float CliffFaceThickness = 1f;
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
        //  경제다: 부스트 0.4초 = +2.7m이고 충돌 한 번이 −8.2m(1.2초)이니, 패드 하나가 충돌 ⅓회를
        //  메운다. 관문마다 놓으면 대시 경제가 통째로 무의미해진다.
        //  부스트 길이는 따로 두지 않고 게이지 대시(<c>config.DashDuration</c>)와 같게 한다 —
        //  "패드 = 공짜 대시 한 번"으로 읽혀야 하고, 둘이 다르면 어느 쪽이 기준인지 헷갈린다(2026-09-25).
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

            //  샤프트 자리를 관문 배치보다 먼저 정한다 — gateAllowed가 그 자리를 피하게 하려면
            //  파이프를 놓기 전에 구멍이 어디인지 알아야 한다.
            var shafts = LOP.MapTools.FieldLayout.PlaceShafts(profile, StartX, length,
                                                             FlappyRace.CourseSectionRule.Count, ceilingY,
                                                             config.ForwardSpeed, config.AirflowUpAccel,
                                                             config.AirflowRiseCap);
            if (shafts.Count != FlappyRace.CourseSectionRule.Count)
            {
                Debug.LogWarning($"[전통 코스] 샤프트가 {shafts.Count}개 놓였다"
                               + $" (구간 {FlappyRace.CourseSectionRule.Count}개 중 자리를 못 찾은 구간이 있다).");
            }
            System.Func<float, bool> gateAllowed =
                x => profile.GateAllowedAt(x, LOP.MapTools.CourseProfileRule.GateMargin)
                  && LOP.MapTools.FieldLayout.GateBlocked(shafts, x, spacing) == false;

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

            var floorPieces = LOP.MapTools.CourseProfileRule.FloorPieces(profile, splits, shafts);
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
            Shafts(composed.transform, shafts, length, fallback);
            Cliffs(composed.transform, profile, floorY, ceilingY, length, fallback);
            HillTunnels(composed.transform, profile, length, fallback);
            Buildings(composed.transform, profile, ceilingY, length, fallback);
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
            Shortcuts(composed.transform, profile, config, length, fallback);
            int branchPads = Branches(composed.transform, profile, config, ceilingY, fallback);
            int guards = Guards(composed.transform, profile, ceilingY, length, fallback, out int guardNoHide, out int guardOverlaps);

            int challengeGates = 0;
            for (int pipeIndex = 0; pipeIndex < pipes.Count; pipeIndex++)
            {
                LOP.MapTools.CoursePipe p = pipes[pipeIndex];
                Material skin = SectionMaterial(p.X, length, fallback);
                float lift = centerAt(p.X);
                //  <b>실제</b> 바닥·천장까지 닿아야 한다. 평평한 floorY까지만 그리면 회랑이
                //  내려간 자리에서 파이프 아래에 틈이 생겨 새가 빠져나간다.
                float bottom = floorY + lift - 1f;
                float top = ceilingY + lift + 1f;

                if (p.HasChallenge == false)
                {
                    Pipe(composed.transform, $"PipeLow_{p.X:F0}", p.X, bottom, p.GapCenter - window * 0.5f, skin);
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
                                      floorY, ceilingY, config.DashDuration, fallback);

            //  샤프트마다 굴뚝 바로 뒤에 전용 홀로그램 관문 — 보통 관문 목록(pipes)에 넣지 않는다.
            //  Validate의 간격·높이차 규칙은 보통 관문의 리듬이고, 이 관문은 그 리듬 밖의 덤이다
            //  (통과 가능성은 검사기의 탐색이 실제 콜라이더로 확인한다).
            float finishX = StartX + length + spacing;
            var hologramGateXs = LOP.MapTools.FieldLayout.HologramGateXs(profile, shafts, finishX);
            if (hologramGateXs.Count != shafts.Count)
            {
                Debug.LogWarning($"[전통 코스] 전용 홀로그램 관문이 샤프트 {shafts.Count}개 중 {hologramGateXs.Count}개에만 섰다"
                               + " (굴뚝 뒤가 평지가 아니거나 결승선에 너무 가까운 샤프트가 있다).");
            }
            int holograms = HologramGates(composed.transform, hologramGateXs, window, centerAt,
                                          floorY, ceilingY, length, fallback);

            var airflowRects = LOP.MapTools.FieldLayout.Airflows(profile, shafts, ceilingY);
            Airflows(composed.transform, airflowRects);

            Backdrop(composed.transform, "Midground",
                     LOP.MapTools.BackdropLayout.Midground(StartX, length, MidgroundSeed),
                     MidgroundZ, MidgroundDepth, FlappyCityMaterials.Midground, centerAt);

            //  광산 굽기가 끈 바탕(구름·장식·도시 실루엣)을 다시 켠다 — 구름·장식은 여기서 다시 만들지 않으므로 지우지 않고 끄기만 했다.
            SetClassicBackdropActive(true, "Build classic course");
            RebuildSkyline(length);

            //  전통 코스는 추격자·수동 대시가 있는 맵이다 — 같은 씬을 광산 굽기가 끈 채로 남겼어도 다시 켠다.
            ApplyMapRules(composed, chaser: true, manualDash: true);
            //  광산 굽기가 남긴 통로 중심선도 뗀다 — 남겨 두면 카메라(FlappyCorridorCamera)가 이 코스에서도
            //  광산 높이를 따라간다. 전통 코스는 표시가 없는 맵이다.
            RemoveCorridorLine(composed);

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
                    + $" · 갈림길 {LOP.MapTools.CourseProfileRule.Branches(profile, ceilingY).Count}개 (패드 {branchPads}개)"
                    + $" · 빌딩 {profile.Buildings.Count} · 절벽 {profile.Cliffs.Count} · 언덕 굴 {profile.HillTunnels.Count}"
                    + $" · 도전 관문 {challengeGates}개"
                    + $" · 부스트 패드 {boostPads}개 ({config.DashDuration:F1}초)"
                    + $" · 샤프트 {shafts.Count}개 · 기류 {airflowRects.Count}개 · 홀로그램 {holograms}개"
                    + $" · 문지기 {guards}개 (셔터, 숨을 자리 부족 {guardNoHide})"
                    + (guardOverlaps > 0 ? $" · ⚠️ 셔터 지형 겹침 {guardOverlaps}" : ""));
        }

        /// <summary>굽기와 같은 코스 프로필. 에디터 측정(eval)이 씬과 같은 기하를 다시 얻을 때 쓴다.</summary>
        public static LOP.MapTools.CourseProfile ComposeProfile(LOP.MasterData.FlappyConfig config)
        {
            float spacing = LOP.MapTools.GateRhythmRule.TargetSpacing(config.ForwardSpeed);
            float ceilingY = LOP.MapTools.VisualHonesty.ScreenHalfHeight(CameraDistance, VerticalFov);
            float length = RaceSeconds * config.ForwardSpeed;
            return LOP.MapTools.CourseProfileRule.Compose(
                StartX, length, spacing, ceilingY, Seed,
                leadIn: spacing * 4f, tail: spacing * 8f,
                arc: new LOP.MapTools.FlapArc(config.FlapImpulse, config.Gravity, config.ForwardSpeed, TickSeconds));
        }

        //  코스 지오메트리 안에 섞여 있는 마커(FinishLine·SpawnPoint)를 <c>---Course---</c>
        //  아래로 옮긴다. 마커는 코스가 아니라 <b>규칙</b>이라 다시 구울 때 살아남아야 한다.
        internal static void RescueMarkers(Transform composed)
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

        //  맵 룰 마커(추격자·수동 대시)를 ComposedMap 자체에 둔다 — 자식은 굽기마다 지워지지만 이건 살아남는다.
        //  없으면 붙이고, 있으면 값만 바꾼다. 마커가 없는 맵은 둘 다 꺼진 것과 같지만, 굽기는 뜻을 씬에 남긴다.
        internal static void ApplyMapRules(GameObject composed, bool chaser, bool manualDash)
        {
            var rules = composed.GetComponent<LOP.FlappyMapRules>();
            if (rules == null)
            {
                rules = Undo.AddComponent<LOP.FlappyMapRules>(composed);
            }
            Undo.RecordObject(rules, "Build course map rules");
            rules.Chaser = chaser;
            rules.ManualDash = manualDash;
            EditorUtility.SetDirty(rules);
        }

        //  통로 중심선 표시(광산 굽기가 ComposedMap 자체에 붙인다 — 자식이 아니라 굽기마다 지워지지 않는다)를 뗀다.
        //  Undo로 떼므로 굽기를 되돌리면 같이 돌아온다. 없으면 아무것도 안 한다.
        internal static void RemoveCorridorLine(GameObject composed)
        {
            var line = composed.GetComponent<LOP.FlappyCorridorLine>();
            if (line != null)
            {
                Undo.DestroyObjectImmediate(line);
            }
        }

        internal static void EditorSceneManagerSave()
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

        //  지름길 하나 = 지붕 띠 + 혀 띠(굴을 판 덩어리를 세로로 자른 볼록 사각형). 표시·패드는
        //  <see cref="Branches"/>가 갈림길 전체(지름길·빌딩 위층·언덕 굴)를 한 곳에서 맡는다.
        private static void Shortcuts(Transform parent, LOP.MapTools.CourseProfile profile,
                                      LOP.MasterData.FlappyConfig config, float length, Material fallback)
        {
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
                //  호 틱 수·길이를 같이 찍어 둔다 — 굽을 때마다 사람이 눈으로 "32틱·4.35m"와 맞는지
                //  비교할 수 있게. 시험이 FlapArc를 자기가 만들어 쓰므로, 빌더가 엉뚱한 값을 넘겨도
                //  시험도 검사기도 못 잡는다(뮤테이션으로 실제 확인됨) — 이 로그가 유일한 안전망이다.
                Debug.Log($"[전통 코스] 지름길 x={r.X0:F0}: 호 {r.Entrance.Arcs}개(호 {r.Arc.TicksPerArc}틱·{r.Arc.Span:F2}m) · 굴 {r.Entrance.Thickness:F1}m · 턱 {r.Entrance.Lip:F0}m · 굴 끝 {r.ChannelEnd:F1} · 출구 {r.X1:F1}");
            }
        }

        //  갈림길(계곡 지름길·빌딩 위층·언덕 굴)마다 검사기용 표시 + 부스트 패드 하나. 갈림길이 빠른 이유는
        //  거리가 아니라 이 패드다 — 전진 속도는 늘 같다(spec 2026-09-28 §0).
        private static int Branches(Transform parent, LOP.MapTools.CourseProfile profile,
                                    LOP.MasterData.FlappyConfig config, float half, Material fallback)
        {
            int pads = 0;
            float span = LOP.FlappyDashCurve.Distance(config.ForwardSpeed, config.DashDuration,
                                                      config.DashDuration, config.DashMult, TickSeconds);
            foreach (LOP.MapTools.Branch branch in LOP.MapTools.CourseProfileRule.Branches(profile, half))
            {
                LOP.MapTools.ShortcutRect r = branch.Rect;
                AreaMarker(parent, branch.MarkerName, new LOP.MapTools.Box2(r.X0, r.Y0, r.X1, r.Y1));

                float? padX = LOP.MapTools.ShortcutRule.PadCenterX(r, span, BoostPadWidth, ShortcutExitClear);
                if (padX.HasValue == false)
                {
                    Debug.LogWarning($"[전통 코스] x={r.X0:F0} {branch.Label}이 짧아 패드를 못 놓았다 ({r.Length:F1}m)");
                    continue;
                }
                float padY = r.CenterY;
                float padHeight = LOP.MapTools.BoostPadRule.Fit(
                    ref padY, r.Y1 - r.Y0, r.Y0 + BoostPadClearance, r.Y1 - BoostPadClearance);
                BoostPad(parent, $"BoostPad_{padX.Value:F0}", padX.Value, padY, padHeight, config.DashDuration, fallback);
                pads++;
            }
            return pads;
        }

        //  갈림길 입구 문지기 = 주기 셔터(spec 2026-10-03 §2, 2026-10-04 셔터로 바꿈). 루트는 안 움직이고 자식 Door가
        //  오르내린다(FlappyShutterField). 문 콜라이더는 구간 재질 — 층 규약이 "장애물"로 읽는다.
        //  문이 다 열렸을 때 들어갈 천장 속이 꽉 차 있어야 한다 — 비어 있으면 열린 문이 허공에 떠 보이고 위로 지나는 길을 막는다.
        //  그래서 숨을 자리를 찾을 때까지 문을 굴 안쪽으로 민다(언덕 굴은 입구 천장이 얇다).
        private static int Guards(Transform parent, LOP.MapTools.CourseProfile profile, float half, float length,
                                  Material fallback, out int noHide, out int overlaps)
        {
            noHide = 0;
            overlaps = 0;
            Physics.SyncTransforms();
            int mask = LayerMask.GetMask("Default");
            int built = 0;
            foreach (LOP.MapTools.GuardSpot spot in LOP.MapTools.GuardLayout.ForCourse(LOP.MapTools.CourseProfileRule.Branches(profile, half)))
            {
                LOP.MapTools.GuardSpot g = spot;
                bool hidden = false;
                for (float x = spot.DoorX; x <= spot.BranchX0 + ShutterSearchDepth + 1e-3f; x += ShutterSearchStep)
                {
                    if (ShutterHidden(spot.AtDoorX(x), mask)) { g = spot.AtDoorX(x); hidden = true; break; }
                }
                if (hidden == false)
                {
                    noHide++;
                    Debug.LogWarning($"[전통 코스] {spot.MarkerName}: x {spot.DoorX:F2}~{spot.BranchX0 + ShutterSearchDepth:F2}에 열린 문이 숨을 천장이 없다 — 셔터를 안 놓는다");
                    continue;
                }
                if (Mathf.Abs(g.DoorX - spot.DoorX) > 1e-3f)
                {
                    Debug.Log($"[전통 코스] {g.MarkerName}: 숨을 자리 따라 문을 x {spot.DoorX:F2} → {g.DoorX:F2}로 민다");
                }

                ShutterGuard(parent, g, SectionMaterial(g.DoorX, length, fallback));
                built++;
                Physics.SyncTransforms();
                if (ClosedDoorTouchesTerrain(g, mask))
                {
                    overlaps++;
                    Debug.LogWarning($"[전통 코스] {g.MarkerName}: 닫힌 문이 칸 안에서 지형과 겹친다");
                }
            }
            return built;
        }

        private const float ShutterSearchStep = 0.25f;
        //  입구(X0)에서 이만큼 안쪽까지만 민다 — 더 들어가면 입구 문지기가 아니다.
        private const float ShutterSearchDepth = 8f;
        private const float ShutterStripe = 0.3f;
        //  문 앞면은 지형 앞면(z −2.5)보다 0.05 뒤 — 천장 속에 들어갔을 때 같은 면에서 깜빡이지 않는다.
        private const float ShutterDepth = PipeDepth - 0.05f;

        private static void ShutterGuard(Transform parent, LOP.MapTools.GuardSpot g, Material skin)
        {
            var root = new GameObject(g.MarkerName);
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.position = new Vector3(g.DoorX, 0f, 0f);
            root.isStatic = true;   // 루트는 안 움직인다. 움직이는 Door 이하는 static이 아니다
            Undo.RegisterCreatedObjectUndo(root, "Build classic course");

            var door = new GameObject("Door");
            door.transform.SetParent(root.transform, worldPositionStays: false);
            door.layer = LayerMask.NameToLayer("Default");

            var marker = root.AddComponent<LOP.FlappyShutter>();
            marker.Travel = g.Travel;
            marker.Period = LOP.MapTools.GuardLayout.PeriodSeconds;
            marker.OpenShare = LOP.MapTools.GuardLayout.ShutterOpenShare;
            marker.MoveShare = LOP.MapTools.GuardLayout.ShutterMoveShare;
            marker.Phase = 0f;
            marker.Door = door.transform;

            //  Box() 규약을 깊이만 바꿔 쓴다: 그려지는 면 z [−ShutterDepth, 0], 콜라이더는 z 0을 가운데로 걸친다(파이프와 같다).
            float w = LOP.MapTools.GuardLayout.ShutterWidth;
            var panel = Box(door.transform, "Panel", skin);
            panel.transform.localScale = new Vector3(w, g.DoorTop - g.DoorBottom, ShutterDepth);
            panel.transform.localPosition = new Vector3(0f, (g.DoorBottom + g.DoorTop) * 0.5f, -ShutterDepth * 0.5f);

            //  바닥 쪽 경고 띠 — 렌더 전용, 문 앞면 바로 앞. Door 자식이라 같이 오르내린다.
            float front = -ShutterDepth;
            const int stripes = 4;
            for (int i = 0; i < stripes; i++)
            {
                float x0 = -w * 0.5f + w * i / stripes, x1 = -w * 0.5f + w * (i + 1) / stripes;
                RenderOnly(door.transform, $"Stripe_{i}",
                    BoxPolygon(new LOP.MapTools.Box2(x0, g.DoorBottom, x1, g.DoorBottom + ShutterStripe)),
                    front - 0.03f, front, i % 2 == 0 ? WarningYellowMaterial() : WarningBlackMaterial());
            }
        }

        //  다 열린 문이 차지할 천장 속(칸 천장 위 0.1 ~ 문 꼭대기)이 문 양 끝·가운데에서 모두 지형 안인가.
        private static bool ShutterHidden(LOP.MapTools.GuardSpot g, int mask)
        {
            float w = LOP.MapTools.GuardLayout.ShutterWidth;
            float top = g.DoorTop + g.Travel;
            for (int c = -1; c <= 1; c++)
            {
                float x = g.DoorX + c * w * 0.5f;
                for (float y = g.Y1 + 0.1f; y <= top + 1e-3f; y += ShutterSearchStep)
                {
                    if (InsideTerrain(new Vector3(x, y, 0f), mask) == false) { return false; }
                }
                if (InsideTerrain(new Vector3(x, top, 0f), mask) == false) { return false; }
            }
            return true;
        }

        private static bool InsideTerrain(Vector3 p, int mask)
        {
            foreach (Collider c in Physics.OverlapSphere(p, 0.01f, mask, QueryTriggerInteraction.Ignore))
            {
                if (IsGuard(c.transform) == false) { return true; }
            }
            return false;
        }

        private static bool IsGuard(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.StartsWith(LOP.MapTools.GuardLayout.MarkerPrefix, System.StringComparison.Ordinal)) { return true; }
            }
            return false;
        }

        //  닫힌 문의 칸 안 부분(바닥~천장)이 지형을 0.01보다 깊이 파고드나. 바닥 묻힘·천장 속 부분은 일부러 겹치므로 뺀다.
        private static bool ClosedDoorTouchesTerrain(LOP.MapTools.GuardSpot g, int mask)
        {
            const float tolerance = 0.01f;   // 맞닿기만 한 것은 겹침이 아니다
            float w = LOP.MapTools.GuardLayout.ShutterWidth;
            var center = new Vector3(g.DoorX, (g.Y0 + g.Y1) * 0.5f, 0f);
            var halfExtents = new Vector3(w * 0.5f - tolerance, (g.Y1 - g.Y0) * 0.5f - tolerance, ShutterDepth * 0.5f - tolerance);
            foreach (Collider c in Physics.OverlapBox(center, halfExtents, Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
            {
                if (IsGuard(c.transform) == false) { return true; }
            }
            return false;
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
        internal static GameObject Prism(Transform parent, string name, Vector2[] polygon, Material material)
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
        internal static Mesh PrismMesh(string name, Vector2[] poly, float zNear, float zFar)
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
                                     float floorY, float ceilingY, float padDuration, Material fallback)
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

                BoostPad(parent, $"BoostPad_{padX:F0}", padX, padY, padHeight, padDuration, fallback);
                placed++;
            }
            return placed;
        }

        //  콜라이더가 없다 — 판정은 <c>FlappyBoostPadField</c>가 산술로 한다(트리거로 하면
        //  롤백 재생에서 물리를 안 돌려 아예 답이 없다). 그려지는 크기가 곧 판정 사각형이라
        //  🎥 시각 정직성이 유지된다.
        internal static void BoostPad(Transform parent, string name, float x, float y, float height,
                                      float duration, Material fallback, float width = BoostPadWidth)
        {
            Material skin = FlappyCityMaterials.Boost != null ? FlappyCityMaterials.Boost : fallback;
            var go = Box(parent, name, skin);
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.transform.localScale = new Vector3(width, height, PipeDepth);
            go.transform.position = new Vector3(x, y, PipeZ);

            var pad = go.AddComponent<LOP.FlappyBoostPad>();
            pad.Width = width;
            pad.Height = height;
            pad.Duration = duration;
        }

        internal static void Pipe(Transform parent, string name, float x, float bottom, float top,
                                  Material material, float width = PipeWidth)
        {
            var go = Box(parent, name, material);
            float h = top - bottom;
            go.transform.localScale = new Vector3(width, h, PipeDepth);
            go.transform.position = new Vector3(x, bottom + h * 0.5f, PipeZ);
        }

        //  샤프트 = 바닥에 뚫린 ∪자 주머니. 회랑 바닥은 구멍 두 개(샤프트·굴뚝)만 비고 가운데는 이어진다.
        private static void Shafts(Transform parent, IReadOnlyList<LOP.MapTools.ShaftPiece> shafts, float length,
                                   Material fallback)
        {
            foreach (var s in shafts)
            {
                Material skin = SectionMaterial((s.X0 + s.X1) * 0.5f, length, fallback);
                float bottom = s.PocketFloorY - WallThickness;
                Slab(parent, $"ShaftFloor_{s.X0:F0}", s.X0 - LOP.MapTools.FieldLayout.SideWall,
                     s.X1 + LOP.MapTools.FieldLayout.SideWall, bottom, s.PocketFloorY, skin);
                Slab(parent, $"ShaftWallL_{s.X0:F0}", s.X0 - LOP.MapTools.FieldLayout.SideWall, s.X0,
                     bottom, s.FloorY, skin);
                Slab(parent, $"ShaftWallR_{s.X0:F0}", s.X1, s.X1 + LOP.MapTools.FieldLayout.SideWall,
                     bottom, s.FloorY, skin);
                Slab(parent, $"ShaftMiddle_{s.X0:F0}", s.X0 + LOP.MapTools.FieldLayout.ShaftWidth, s.ChimneyX0,
                     s.PocketTop, s.FloorY, skin);
                ShaftDepthDecor(parent, s, skin);
            }
        }

        private static void Slab(Transform parent, string name, float x0, float x1, float y0, float y1, Material skin)
        {
            var go = Box(parent, name, skin);
            go.transform.localScale = new Vector3(x1 - x0, y1 - y0, PipeDepth);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, PipeZ);
        }

        //  절벽 = 바닥이 Edge에서 수직으로 떨어진다. 바닥 경사 조각은 Edge~SlopeEnd를 비워 두므로(FloorGaps)
        //  아래 바닥과 절벽 면을 여기서 채운다. 절벽 면은 윗바닥 슬래브(두께 20m)보다 낙차(25m)가 커서 생기는
        //  슬래브 밑 빈칸까지 막는다.
        //
        //  <b>면은 x로 얇게(CliffFaceThickness) 둔다</b> — y(낙차 쪽)는 WallThickness만큼 두꺼워도
        //  되지만, x까지 20m로 번지면 절벽 20m 이내에 계곡·샤프트 구멍이 오는 시드·길이 조합에서
        //  그 구멍을 이 슬래브가 조용히 메워 버린다. 얇아도 y 범위(lower-WallThickness~top)는 그대로라
        //  윗바닥 슬래브 밑 빈칸을 막는 역할은 그대로 한다.
        private static void Cliffs(Transform parent, LOP.MapTools.CourseProfile profile, float floorY, float half,
                                   float length, Material fallback)
        {
            foreach (LOP.MapTools.CliffPiece c in profile.Cliffs)
            {
                Material skin = SectionMaterial(c.Edge, length, fallback);
                float top = floorY + c.TopY;
                float lower = floorY + c.BottomY;
                Slab(parent, $"CliffFace_{c.Edge:F0}", c.Edge - CliffFaceThickness, c.Edge, lower - WallThickness, top, skin);
                Slab(parent, $"CliffFloor_{c.Edge:F0}", c.Edge, c.SlopeEnd, lower - WallThickness, lower, skin);

                //  예고·잔해는 판정면 뒤 렌더 전용 — 바닥이 뚝 끊긴다는 걸 멀리서 읽히게 한다.
                var decor = new GameObject($"CliffDecor_{c.Edge:F0}");
                decor.transform.SetParent(parent, worldPositionStays: false);
                Undo.RegisterCreatedObjectUndo(decor, "Build classic course");
                var stripes = LOP.MapTools.CliffDecor.Stripes(c, half);
                for (int i = 0; i < stripes.Count; i++)
                {
                    RenderOnly(decor.transform, $"Stripe_{i}", BoxPolygon(stripes[i]), 0.3f, 1.2f,
                               i % 2 == 0 ? WarningYellowMaterial() : WarningBlackMaterial());
                }
                RenderOnly(decor.transform, "SignPost", BoxPolygon(LOP.MapTools.CliffDecor.SignPost(c, half)), 0.5f, 0.7f,
                           WarningBlackMaterial());
                RenderOnly(decor.transform, "SignTriangle", TrianglePolygon(LOP.MapTools.CliffDecor.SignTriangle(c, half)),
                           0.45f, 0.55f, WarningYellowMaterial());
                var chunks = LOP.MapTools.CliffDecor.BrokenChunks(c, half);
                for (int i = 0; i < chunks.Count; i++)
                {
                    //  조각은 장애물 재질(skin)이라 게임 평면 대역(±0.45)에 걸치면 층 검사가 "보이는데 통과된다"로
                    //  잡는다 — 대역 밖(0.5~)으로 민다.
                    RenderOnly(decor.transform, $"Chunk_{i}", ToPolygon(chunks[i]), 0.5f, 1.2f, skin);
                }
                var rebars = LOP.MapTools.CliffDecor.Rebars(c, half);
                for (int i = 0; i < rebars.Count; i++)
                {
                    RenderOnly(decor.transform, $"Rebar_{i}", ToPolygon(rebars[i]), 0.6f, 0.7f, RebarMaterial());
                }
            }
        }

        //  언덕 굴 = 언덕 자리의 바닥 조각을 비우고(FloorGaps) 굴 바닥 + 굴 위 덩어리 띠로 채운다.
        private static void HillTunnels(Transform parent, LOP.MapTools.CourseProfile profile, float length,
                                        Material fallback)
        {
            foreach (LOP.MapTools.HillTunnelPiece t in profile.HillTunnels)
            {
                Material skin = SectionMaterial((t.HillX0 + t.HillX1) * 0.5f, length, fallback);
                Slab(parent, $"HillTunnelFloor_{t.HillX0:F0}", t.HillX0, t.HillX1, t.FloorY - WallThickness, t.FloorY, skin);
                var mass = LOP.MapTools.CourseProfileRule.HillTunnelMass(t);
                for (int i = 0; i < mass.Count; i++)
                {
                    Prism(parent, $"HillTunnelMass_{t.HillX0:F0}_{i}", ToPolygon(mass[i]), skin);
                }
                Debug.Log($"[전통 코스] 언덕 굴 x={t.HillX0:F0}: 굴 {t.Thickness:F1}m · 입구 {t.Mouth:F1} · 출구 {t.Exit:F1}");
            }
        }

        //  빌딩 = 층판·지붕(판정 있음) + 두 층 칸 표시 + 앞벽(연출만). 앞벽은 게임 평면 그림(z −2.5~0)보다
        //  카메라 쪽에 둬 🎥·🧱 검사의 "게임 평면" 밖이다 — 콜라이더도 없다.
        private const float FacadeZNear = -3.1f;
        private const float FacadeZFar = -2.8f;

        private static void Buildings(Transform parent, LOP.MapTools.CourseProfile profile, float half, float length,
                                      Material fallback)
        {
            Material facadeMaterial = BuildingConcreteMaterial();
            foreach (LOP.MapTools.BuildingPiece b in profile.Buildings)
            {
                Material skin = SectionMaterial((b.X0 + b.X1) * 0.5f, length, fallback);
                LOP.MapTools.Box2 slab = LOP.MapTools.BuildingLayout.SlabBox(b, half);
                LOP.MapTools.Box2 roof = LOP.MapTools.BuildingLayout.RoofBox(b, half);
                Slab(parent, $"BuildingSlab_{b.X0:F0}", slab.X0, slab.X1, slab.Y0, slab.Y1, skin);
                Slab(parent, $"BuildingRoof_{b.X0:F0}", roof.X0, roof.X1, roof.Y0, roof.Y1, skin);
                AreaMarker(parent, $"BuildingLane_{b.X0:F0}_Lower", LOP.MapTools.BuildingLayout.LowerLaneBox(b, half));
                AreaMarker(parent, $"BuildingLane_{b.X0:F0}_Upper", LOP.MapTools.BuildingLayout.UpperLaneBox(b, half));

                var facade = new GameObject($"BuildingFacade_{b.X0:F0}");
                facade.transform.SetParent(parent, worldPositionStays: false);
                var marker = facade.AddComponent<LOP.FlappyBuildingFacade>();
                marker.X0 = b.X0;
                marker.X1 = b.X1;
                Undo.RegisterCreatedObjectUndo(facade, "Build classic course");
                var strips = LOP.MapTools.BuildingLayout.Facade(b, half);
                for (int i = 0; i < strips.Count; i++)
                {
                    FacadeStrip(facade.transform, $"Facade_{i}", ToPolygon(strips[i]), facadeMaterial);
                }

                //  앞쪽 장식은 앞벽 표시 아래에 둔다 — 반투명 연출과 🏢 검사가 함께 본다. 외벽 띠보다 카메라 쪽.
                var lit = new System.Collections.Generic.List<bool>();
                var windows = LOP.MapTools.BuildingLayout.Windows(b, half, lit);
                for (int i = 0; i < windows.Count; i++)
                {
                    RenderOnly(facade.transform, $"Window_{i}", BoxPolygon(windows[i]), FacadeZNear - 0.1f, FacadeZNear,
                               lit[i] ? BuildingWindowLitMaterial() : BuildingWindowMaterial());
                }
                var trim = LOP.MapTools.BuildingLayout.EntranceTrim(b, half);
                for (int i = 0; i < trim.Count; i++)
                {
                    RenderOnly(facade.transform, $"Trim_{i}", BoxPolygon(trim[i]), FacadeZNear - 0.15f, FacadeZNear,
                               BuildingTrimMaterial());
                }
                LOP.MapTools.Box2 sign = LOP.MapTools.BuildingLayout.Sign(b, half);
                RenderOnly(facade.transform, "Sign", BoxPolygon(sign), FacadeZNear - 0.15f, FacadeZNear, BuildingTrimMaterial());
                var bolt = LOP.MapTools.BuildingLayout.SignBolt(sign);
                for (int i = 0; i < bolt.Count; i++)
                {
                    RenderOnly(facade.transform, $"Bolt_{i}", TrianglePolygon(bolt[i]), FacadeZNear - 0.2f, FacadeZNear - 0.15f,
                               BuildingBoltMaterial());
                }

                //  실내는 판정면 뒤 — 통로 뒤로 도시가 비치지 않게 막고, 기둥·조명으로 "건물 안"을 만든다.
                var interior = new GameObject($"BuildingInterior_{b.X0:F0}");
                interior.transform.SetParent(parent, worldPositionStays: false);
                Undo.RegisterCreatedObjectUndo(interior, "Build classic course");
                RenderOnly(interior.transform, "BackWall_Lower", BoxPolygon(LOP.MapTools.BuildingLayout.LowerLaneBox(b, half)),
                           1.5f, 2.5f, BuildingInteriorMaterial());
                RenderOnly(interior.transform, "BackWall_Upper", BoxPolygon(LOP.MapTools.BuildingLayout.UpperLaneBox(b, half)),
                           1.5f, 2.5f, BuildingInteriorMaterial());
                var pillars = LOP.MapTools.BuildingLayout.Pillars(b, half);
                for (int i = 0; i < pillars.Count; i++)
                {
                    RenderOnly(interior.transform, $"Pillar_{i}", BoxPolygon(pillars[i]), 1.0f, 1.5f, BuildingPillarMaterial());
                }
                var lamps = LOP.MapTools.BuildingLayout.Lamps(b, half);
                for (int i = 0; i < lamps.Count; i++)
                {
                    RenderOnly(interior.transform, $"Lamp_{i}", BoxPolygon(lamps[i]), 1.0f, 1.4f, BuildingLampMaterial());
                }
            }
        }

        //  렌더 전용 앞벽 조각 — Prism과 같은 메시인데 콜라이더가 없다.
        private static void FacadeStrip(Transform parent, string name, Vector2[] polygon, Material material)
        {
            RenderOnly(parent, name, polygon, FacadeZNear, FacadeZFar, material);
        }

        //  렌더 전용 조각 — Prism과 같은 메시인데 콜라이더가 없다. z 범위를 받는다(판정면 앞이면 음수, 뒤면 양수).
        private static GameObject RenderOnly(Transform parent, string name, Vector2[] polygon, float zNear, float zFar,
                                             Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.layer = LayerMask.NameToLayer("Default");
            go.AddComponent<MeshFilter>().sharedMesh = PrismMesh(name, polygon, zNear, zFar);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            //  그림자가 통로·바닥에 드리우면 "보이는 대로"가 흐려진다 — 장식은 그림자를 안 만든다.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        internal static Vector2[] BoxPolygon(LOP.MapTools.Box2 b)
            => new[] { new Vector2(b.X0, b.Y0), new Vector2(b.X1, b.Y0), new Vector2(b.X1, b.Y1), new Vector2(b.X0, b.Y1) };

        private static Vector2[] TrianglePolygon(float[] t)
            => new[] { new Vector2(t[0], t[1]), new Vector2(t[2], t[3]), new Vector2(t[4], t[5]) };

        //  검사기용 표시 — Transform만 있는 빈 GameObject(위치 = 가운데, 크기 = 폭·높이).
        internal static void AreaMarker(Transform parent, string name, LOP.MapTools.Box2 box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = new Vector3((box.X0 + box.X1) * 0.5f, (box.Y0 + box.Y1) * 0.5f, 0f);
            go.transform.localScale = new Vector3(box.X1 - box.X0, box.Y1 - box.Y0, 1f);
            Undo.RegisterCreatedObjectUndo(go, "Build classic course");
        }

        //  2.5D 연출 — 샤프트·굴뚝 양쪽 벽 가장자리에, 판정면보다 <b>뒤</b>(z 1.5~6)로 갈수록
        //  더 아래로 꺾여 들어간 층판을 세운다. "주머니가 화면 안쪽으로도 깊다"는 걸 보여 주는
        //  것일 뿐 판정이 없다 — 🎥 검사의 "렌더 전용"에 들어간다.
        private const int ShaftDecorLayers = 4;
        private const float ShaftDecorThickness = 0.6f;
        private const float ShaftDecorZNear = 1.5f;
        private const float ShaftDecorZFar = 6f;

        private static void ShaftDepthDecor(Transform parent, LOP.MapTools.ShaftPiece s, Material skin)
        {
            float zStep = (ShaftDecorZFar - ShaftDecorZNear) / (ShaftDecorLayers - 1);
            float yStep = s.Depth / ShaftDecorLayers;
            for (int i = 0; i < ShaftDecorLayers; i++)
            {
                float z = ShaftDecorZNear + zStep * i;
                float top = s.FloorY - yStep * i;
                float bottom = top - ShaftDecorThickness;
                DecorSlab(parent, $"ShaftDecorL_{s.X0:F0}_{i}",
                         s.X0 - LOP.MapTools.FieldLayout.SideWall, s.X0, bottom, top, z, skin);
                DecorSlab(parent, $"ShaftDecorR_{s.X0:F0}_{i}",
                         s.X1, s.X1 + LOP.MapTools.FieldLayout.SideWall, bottom, top, z, skin);
            }
        }

        //  렌더 전용 조각 — Box를 만든 뒤 콜라이더를 지운다(낌·벽 규약 검사가 "안 보이는 벽"으로
        //  잡지 않도록).
        private static void DecorSlab(Transform parent, string name, float x0, float x1, float y0, float y1,
                                      float z, Material skin)
        {
            var go = Box(parent, name, skin);
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.transform.localScale = new Vector3(x1 - x0, y1 - y0, ShaftDecorThickness);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z);
        }

        //  기류 사각형마다 판정 마커(LOP.FlappyAirflow, DI가 필드를 채운다) + 판정면 뒤 반투명
        //  기둥(연출뿐 — 콜라이더 없음)을 세운다.
        private static void Airflows(Transform parent, IReadOnlyList<LOP.FlappyAirflowRect> rects)
        {
            Material up = AirflowUpMaterial();
            Material down = AirflowDownMaterial();
            foreach (LOP.FlappyAirflowRect r in rects)
            {
                float cx = (r.X0 + r.X1) * 0.5f;
                float cy = (r.Y0 + r.Y1) * 0.5f;
                float width = r.X1 - r.X0;
                float height = r.Y1 - r.Y0;

                var marker = new GameObject($"Airflow_{r.Kind}_{r.X0:F0}");
                marker.transform.SetParent(parent, worldPositionStays: false);
                marker.transform.position = new Vector3(cx, cy, 0f);
                var flow = marker.AddComponent<LOP.FlappyAirflow>();
                flow.Width = width;
                flow.Height = height;
                flow.Kind = r.Kind;
                Undo.RegisterCreatedObjectUndo(marker, "Build classic course");

                Material fx = r.Kind == LOP.FlappyAirflowKind.Up ? up : down;
                var column = Box(parent, $"AirflowFx_{r.Kind}_{r.X0:F0}", fx);
                Object.DestroyImmediate(column.GetComponent<BoxCollider>());
                column.transform.localScale = new Vector3(width, height, PipeDepth);
                column.transform.position = new Vector3(cx, cy, 1.5f);
            }
        }

        //  전용 홀로그램 관문: 창은 회랑 가운데, 그 바로 아래 창 높이 하나만큼이 홀로그램이다 —
        //  대시로 뚫으면 창보다 한 칸 낮게 빠져나갈 수 있다. 나머지 아래는 바닥까지, 위는 천장까지 파이프.
        //  실제로 세운 홀로그램 수를 돌려준다 — 자리가 안 나서 통파이프로 막은 것은 세지 않는다.
        private static int HologramGates(Transform parent, IReadOnlyList<float> xs, float window,
                                         System.Func<float, float> centerAt, float floorY, float ceilingY,
                                         float length, Material fallback)
        {
            int built = 0;
            foreach (float x in xs)
            {
                Material skin = SectionMaterial(x, length, fallback);
                float lift = centerAt(x);
                float bottom = floorY + lift - 1f;
                float top = ceilingY + lift + 1f;
                float center = (floorY + ceilingY) * 0.5f + lift;
                float windowBottom = center - window * 0.5f;
                float hologramBottom = windowBottom - window;
                if (hologramBottom > bottom)
                {
                    Pipe(parent, $"HoloGateLow_{x:F0}", x, bottom, hologramBottom, skin);
                    Hologram(parent, $"HoloGate_{x:F0}", x, hologramBottom, windowBottom);
                    built++;
                }
                else
                {
                    Debug.LogWarning($"[전통 코스] x={x:F0} 전용 관문 창 아래에 홀로그램 자리가 없다 — 통파이프로 막았다.");
                    Pipe(parent, $"HoloGateLow_{x:F0}", x, bottom, windowBottom, skin);
                }
                Pipe(parent, $"HoloGateHigh_{x:F0}", x, windowBottom + window, top, skin);
            }
            return built;
        }

        //  대시 중이면 통과하는 벽. 콜라이더는 그대로 두고 층만 Hologram이다 — 판정은 이동이 마스크로 고른다.
        private static void Hologram(Transform parent, string name, float x, float bottom, float top)
        {
            var go = Box(parent, name, HologramMaterial());
            go.layer = LayerMask.NameToLayer(LOP.FlappyHologram.LayerName);
            float h = top - bottom;
            go.transform.localScale = new Vector3(PipeWidth, h, PipeDepth);
            go.transform.position = new Vector3(x, bottom + h * 0.5f, PipeZ);
            go.AddComponent<LOP.FlappyHologramMarker>();
        }

        private const string FieldMaterialFolder = "Assets/Art/Materials/Flappy";

        private static Material AirflowUpMaterial() => EnsureTransparent("AirflowUp", new Color(0.5f, 0.85f, 0.6f, 0.30f));
        private static Material AirflowDownMaterial() => EnsureTransparent("AirflowDown", new Color(0.55f, 0.85f, 0.9f, 0.30f));
        private static Material HologramMaterial() => EnsureTransparent("Hologram", new Color(0.62f, 0.42f, 0.92f, 0.35f));

        //  빌딩 외벽(앞벽 띠)·창문·입구 표시. 반투명 연출이 알파를 내려야 해서 반투명 재질로 둔다(알파 0.97).
        //  앞쪽 장식(창문·트림·볼트)은 외벽 콘크리트보다 카메라 쪽에 있는데 큐가 같으면 그리기 순서가
        //  안 정해져 튄다(z-fighting처럼 프레임마다 위아래가 뒤집힌다) — 큐를 한 칸씩 밀어 항상 외벽 다음에
        //  그리게 한다(2026-09-29).
        private static Material BuildingConcreteMaterial() => EnsureTransparent("BuildingConcrete", new Color(0.55f, 0.57f, 0.6f, 0.97f));
        private static Material BuildingWindowMaterial() => EnsureTransparent("BuildingWindow", new Color(0.17f, 0.23f, 0.29f, 0.97f), queueOffset: 1);
        private static Material BuildingWindowLitMaterial() => EnsureTransparent("BuildingWindowLit", new Color(1f, 0.84f, 0.42f, 0.97f), queueOffset: 1);
        private static Material BuildingTrimMaterial() => EnsureTransparent("BuildingTrim", new Color(1f, 0.8f, 0f, 0.97f), queueOffset: 1);
        private static Material BuildingBoltMaterial() => EnsureTransparent("BuildingBolt", new Color(0.1f, 0.1f, 0.1f, 0.97f), queueOffset: 2);
        //  실내(판정면 뒤)는 옅어지지 않는다 — 알파 1. 반투명일 이유가 없는데 반투명 큐(ZWrite off)로
        //  구우면 정렬이 그리는 순서에 기대게 돼 기둥·조명·경고 줄무늬가 서로 튄다 — 불투명으로 굽는다.
        private static Material BuildingInteriorMaterial() => EnsureOpaque("BuildingInterior", new Color(0.15f, 0.17f, 0.21f, 1f));
        private static Material BuildingPillarMaterial() => EnsureOpaque("BuildingPillar", new Color(0.23f, 0.25f, 0.3f, 1f));
        private static Material BuildingLampMaterial() => EnsureOpaque("BuildingLamp", new Color(1f, 0.81f, 0.35f, 1f));
        private static Material WarningYellowMaterial() => EnsureOpaque("WarningYellow", new Color(1f, 0.8f, 0f, 1f));
        private static Material WarningBlackMaterial() => EnsureOpaque("WarningBlack", new Color(0.12f, 0.12f, 0.12f, 1f));
        private static Material RebarMaterial() => EnsureOpaque("Rebar", new Color(0.7f, 0.35f, 0.16f, 1f));

        //  기류·홀로그램 반투명 재질. <b>있으면 그대로 쓴다</b>(FlappyCityMaterials.Ensure와 같은
        //  규칙 — 에디터에서 손으로 고친 색이 다시 구울 때마다 날아가면 아트를 만질 수 없다).
        //  URP Lit을 반투명 알파블렌드로 켜는 값은 이 프로젝트의 기존 반투명 재질
        //  (Assets/Art/Materials/SkydiveCloud.mat)과 같은 조합을 그대로 쓴다.
        //  <paramref name="queueOffset"/>: 같은 Transparent 칸에서 그리기 순서를 강제해야 할 때
        //  쓴다(카메라 쪽 장식을 외벽보다 항상 나중에) — 기본 0은 예전과 같다.
        private static Material EnsureTransparent(string name, Color color, int queueOffset = 0)
        {
            string path = $"{FieldMaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError($"[전통 코스] URP Lit 셰이더를 못 찾아 {name} 재질을 못 만들었다.");
                return null;
            }
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Surface", 1f);   // Transparent
            material.SetFloat("_Blend", 0f);     // Alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            //  URP는 불러올 때 큐를 Transparent + _QueueOffset으로 다시 맞춘다 — renderQueue만 바꾸면 3000으로 돌아간다.
            material.SetFloat("_QueueOffset", queueOffset);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
            if (AssetDatabase.IsValidFolder(FieldMaterialFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Flappy");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        //  알파가 늘 1이라 반투명일 이유가 없는 장식용 불투명 재질. EnsureTransparent와 같은
        //  "있으면 그대로 쓴다" 규칙 — Opaque·ZWrite on·Geometry 큐라 정렬이 그리기 순서에
        //  기대지 않는다(반투명 큐로 구우면 서로 튄다, 2026-09-29).
        private static Material EnsureOpaque(string name, Color color)
        {
            string path = $"{FieldMaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError($"[전통 코스] URP Lit 셰이더를 못 찾아 {name} 재질을 못 만들었다.");
                return null;
            }
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Surface", 0f);   // Opaque
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            if (AssetDatabase.IsValidFolder(FieldMaterialFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Flappy");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        //  <c>---Environment---</c> 아래 전통 코스 전용 바탕 — 광산 코스(FlappyMineCourseBuilder)는 이것들을 끈다
        //  (하늘·실루엣을 따로 입히는데 구름이 그 앞에, 코인·덤불이 통로 안·바닥 여유에 보였다 — 10-08 캡처).
        //  조명은 여기 없다(맵 씬엔 빛이 없고 게임 씬 FlappyRace에 있다). Ground는 원래부터 꺼져 있어 손대지 않는다.
        internal static readonly string[] ClassicBackdropNames = { "Clouds", "Decorations", "CitySilhouette" };

        /// <summary><c>---Environment---</c>의 <see cref="ClassicBackdropNames"/>를 켜거나 끈다. 상태가 바뀐 이름 목록을 돌려준다.</summary>
        internal static List<string> SetClassicBackdropActive(bool active, string undoName)
        {
            var changed = new List<string>();
            var env = GameObject.Find("---Environment---");
            if (env == null) { return changed; }
            foreach (string name in ClassicBackdropNames)
            {
                Transform t = env.transform.Find(name);
                if (t == null || t.gameObject.activeSelf == active) { continue; }
                Undo.RecordObject(t.gameObject, undoName);
                t.gameObject.SetActive(active);
                changed.Add(name);
            }
            return changed;
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

        internal static GameObject Box(Transform parent, string name, Material material)
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
        internal static Material FindCourseMaterial()
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
        internal static void PlaceSpawns(float floorY, float ceilingY, float window, float firstGapCenter)
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
        internal static void PlaceFinish(float x, System.Func<float, float> centerAt)
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
