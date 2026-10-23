using System.Collections.Generic;
using LOP.MapTools;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 맵 씬의 코스를 <b>광산 코스</b>로 다시 굽는다 — 배치는 <see cref="MineCourseRule.Layout"/>(프로토타입 난수 생성기·
    /// 소비 순서 그대로, 구간 배치는 mode 10·시드 17)이 정하고, 여기서는 그 숫자를 전통 굽기의 부품
    /// (<c>Prism</c>·<c>Pipe</c>·<c>BoostPad</c>)으로 세우기만 한다.
    ///
    /// <para><b>회색 박스</b>다: 기믹·스카이라인·미드그라운드는 굽지 않고 재질도 하나뿐이다. 재미를 먼저 보고,
    /// 모양이 정해지면 그때 입힌다. <c>DressFrom</c>~코스 끝 + <see cref="EndMargin"/>(3막 406.15, 10-12 펼치기 2차 — 1차는 첫 "레일 롤러코스터" 151.25까지)를
    /// <see cref="FlappyMineDressing"/>이 광산 옷으로 입힌다(회색 박스의 렌더러만 끄고 판정은 그대로).</para>
    ///
    /// <para>전통 굽기와 같은 씬(<c>ComposedMap</c>)을 갈아 끼운다 — 두 메뉴는 서로를 덮어쓴다.
    /// 맵 룰(<see cref="LOP.FlappyMapRules"/>)도 같이 바꾼다: 광산은 추격자·수동 대시가 없다.</para>
    /// </summary>
    public static class FlappyMineCourseBuilder
    {
        private const float TickSeconds = 0.02f;

        //  바닥·천장을 자를 간격(좁은·낮은 구간의 전이 곡선). 꺾은선 꼭짓점은 이와 상관없이 다 들어간다.
        //  옷 칸(MineDressingLayout.BentTiles)이 같은 값으로 끊어야 그림과 회색 박스가 같은 x에서 꺾인다 — 값은 한곳에서 정한다.
        private const float BreakStep = MineDressingLayout.BreakStep;

        //  굴을 사다리꼴로 자르는 간격 — 전통 지름길 띠(ShortcutStripStep)와 같다.
        private const float TubeStripStep = 0.25f;

        //  바닥·천장을 스폰 뒤·결승선 뒤로 더 뻗는다. 옷 범위 끝(코스 끝 + 이 값)과 같은 값이라 배치 규칙 쪽 상수 하나를 쓴다.
        private const float EndMargin = MineDressingLayout.EndMargin;

        //  NaN 끝(통로 바닥·천장까지 막는 관문)은 선에 딱 맞추지 않고 이만큼 벽 안으로 묻는다 — 관문이 두꺼우면
        //  (긴 통로 12 m) 통로가 기운 자리에서 한쪽 아래에 틈이 생긴다. 전통 굽기의 파이프도 1 m 묻는다.
        private const float PipeBury = 1f;

        //  스폰 높이 범위를 정할 창 — 프로토타입 관문 틈(GAP)과 같다.
        private const float SpawnWindow = 3.75f;

        //  옷 입히는 범위 시작. 끝은 바닥·천장 조각 끝과 같은 코스 끝 + EndMargin(3막 406.15) — 범위 안 회색 렌더러가 전부 꺼진다.
        //  10-12 펼치기 2차: 1차는 첫 "레일 롤러코스터"(151.25)까지였다.
        private const float DressFrom = -EndMargin;

        [MenuItem("LOP/Debug/Flappy 광산 코스 굽기")]
        public static void Build()
        {
            if (FlappyClassicCourseBuilder.TryReadConfig(out LOP.MasterData.FlappyConfig config) == false)
            {
                EditorUtility.DisplayDialog("광산 코스 굽기",
                    "MasterData에서 FlappyConfig를 못 읽었다 — 패키지 StreamingAssets를 확인하라.", "확인");
                return;
            }

            var composed = GameObject.Find("ComposedMap");
            if (composed == null)
            {
                EditorUtility.DisplayDialog("광산 코스 굽기",
                    "씬에 ComposedMap이 없다 — 맵 씬(Assets/Art/Scenes/FlappyRaceMap.unity)을 먼저 열어라.", "확인");
                return;
            }

            var physics = new MinePhysics(config.ForwardSpeed, config.FlapImpulse, config.Gravity, config.MaxFallSpeed, TickSeconds);
            MineCourse course = MineCourseRule.Layout(physics);
            string bad = MineCourseRule.Validate(course);
            if (bad != null)
            {
                //  씬을 건드리기 <b>전에</b> 멈춘다 — 반쯤 구운 코스를 남기지 않는다.
                EditorUtility.DisplayDialog("광산 코스 굽기", "배치가 규칙을 어겼다:\n" + bad, "확인");
                return;
            }

            //  마커(FinishLine·SpawnPoint)를 먼저 빼낸다 — ComposedMap 자식이라 그냥 지우면 같이 사라진다.
            FlappyClassicCourseBuilder.RescueMarkers(composed.transform);

            Undo.RegisterFullObjectHierarchyUndo(composed, "Build mine course");
            for (int i = composed.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(composed.transform.GetChild(i).gameObject);
            }

            Transform root = composed.transform;
            Material skin = FlappyClassicCourseBuilder.FindCourseMaterial();
            float FloorAt(float x) => course.CenterAt(x) - course.HalfAt(x);
            float CeilingAt(float x) => course.CenterAt(x) + course.HalfAt(x);

            int slabs = BuildFloorAndCeiling(root, course, skin, FloorAt, CeilingAt);
            int strips = BuildTubes(root, course, skin, FloorAt, CeilingAt);
            int pipes = BuildGates(root, course, skin, FloorAt, CeilingAt);

            for (int i = 0; i < course.Walls.Count; i++)
            {
                MineRect w = course.Walls[i];
                FlappyClassicCourseBuilder.Prism(root, $"ForkWall_{w.X0:F0}",
                    FlappyClassicCourseBuilder.BoxPolygon(new Box2(w.X0, w.Y0, w.X1, w.Y1)), skin);
            }

            //  움직이는 장애물(승강기·진자 광차·낙석)과 무너진 갱도 받침목 — 값은 코스 규칙에서(spec §3).
            FlappyMineMoverBake.Counts movers = FlappyMineMoverBake.Build(root, course, skin, FloorAt, CeilingAt);

            //  패드 사각형이 곧 판정 사각형(FlappyBoostPad) — 크기는 배치 그대로(폭 1.2 · 높이 2.8).
            //  다만 판정은 발밑 점으로 하고 규칙의 사각형은 몸 중심 기준(프로토타입)이라, 몸 반지름만큼 내려 놓는다.
            foreach (MinePad pad in course.Pads)
            {
                MineRect r = pad.Rect;
                float centerY = (r.Y0 + r.Y1) * 0.5f - config.BodyRadius;
                FlappyClassicCourseBuilder.BoostPad(root, $"BoostPad_{r.X0:F0}", (r.X0 + r.X1) * 0.5f, centerY,
                                                    r.Y1 - r.Y0, pad.Duration, skin, r.X1 - r.X0);
            }

            //  갈림길 표시 — 검사기가 굴 쪽을 강제로 태워 볼 때 반대쪽을 막는다(굴이 위면 아래를 막는다).
            foreach (MineFork fork in course.Forks)
            {
                MineRect lane = fork.TunnelLane;
                var branch = new Branch(
                    ShortcutRect.FromCenterSize((lane.X0 + lane.X1) * 0.5f, (lane.Y0 + lane.Y1) * 0.5f,
                                                lane.X1 - lane.X0, lane.Y1 - lane.Y0),
                    fork.TunnelUp ? BranchSide.Below : BranchSide.Above, BranchKind.Mine);
                FlappyClassicCourseBuilder.AreaMarker(root, branch.MarkerName, new Box2(lane.X0, lane.Y0, lane.X1, lane.Y1));
            }

            //  빛 알갱이 무더기(spec 2026-10-23) — 판정은 FlappyMoteField가 산술로, 그림은 클라 FlappyMoteFx가 한다.
            //  움직이는 관문의 무더기는 그 승강기 케이지 자식으로 — 케이지와 같이 움직이고, 마커가 부모 승강기의 움직임을 읽어 판정에 쓴다.
            foreach (MineMoteCluster m in course.Motes)
            {
                Transform parent = root;
                if (m.Lift >= 0)
                {
                    parent = root.Find($"Lift_{course.Lifts[m.Lift].Gate.X:F1}/Cage");
                    if (parent == null)
                    {
                        //  고정으로 두면 관문이 오르내릴 때 알갱이가 파이프 속에 묻힌다 — 굽기를 실패시킨다.
                        throw new System.InvalidOperationException(
                            $"[광산 코스] 승강기 케이지(Lift_{course.Lifts[m.Lift].Gate.X:F1}/Cage)를 못 찾았다 — 무더기를 움직이게 세울 수 없다.");
                    }
                }
                FlappyClassicCourseBuilder.MoteCluster(parent, $"MoteCluster_{m.X:F0}", m.X, m.Y, m.GapHalf, m.Big, m.Points);
            }

            //  다이브 충전 맵: 날갯짓을 참고 빠르게 떨어지는 동안 게이지가 차고, 쓰는 때는 대시 버튼으로 고른다
            //  (spec 2026-10-23-flappy-dive-charge). 알갱이는 끈다.
            FlappyClassicCourseBuilder.ApplyMapRules(composed, chaser: false, manualDash: true, moteCharge: false, diveCharge: true);

            FlappyClassicCourseBuilder.PlaceSpawns(FloorAt(0f), CeilingAt(0f), SpawnWindow,
                                                   course.Gates.Count > 0 ? course.Gates[0].GapCenter : course.CenterAt(0f));
            FlappyClassicCourseBuilder.PlaceFinish(course.Length, course.CenterAt);

            //  카메라가 통로를 따라가려면(FlappyCorridorCamera) 그 중심선이 씬에 있어야 한다 — 없으면 붙이고,
            //  있으면 점만 갈아 끼운다. 스폰·결승선 뒤로도 EndMargin만큼 더 뻗는다(바닥·천장과 같은 범위).
            BuildCorridorLine(composed, course);

            //  광산 옷(스펙 §6) — 회색 박스 위에 부품을 놓고 범위 안 회색 렌더러를 끈다. 설정 에셋이 없으면 회색 박스로 둔다.
            var look = AssetDatabase.LoadAssetAtPath<FlappyMineLook>(FlappyMineMaterials.LookAssetPath);
            if (look == null)
            {
                Debug.LogError($"[광산 코스] {FlappyMineMaterials.LookAssetPath}가 없어 옷을 입히지 않았다 — 회색 박스로 남는다.");
                FlappyClassicCourseBuilder.SetFinishMarkerVisible(true, "Build mine course");
            }
            else
            {
                FlappyMineMaterials.Ensure(look);
                if (FlappyMineDressing.Dress(root, course, look, DressFrom, course.Length + EndMargin))
                {
                    //  전통 코스 바탕(구름·코인·덤불·도시 실루엣)은 끈다 — 지우지 않는다(전통 굽기가 다시 켠다).
                    List<string> hiddenBackdrop = FlappyClassicCourseBuilder.SetClassicBackdropActive(false, "Build mine course");
                    Debug.Log("[광산 코스] 끈 전통 바탕: " + (hiddenBackdrop.Count > 0 ? string.Join(", ", hiddenBackdrop) : "없음(이미 꺼져 있음)"));
                    //  결승선 마커 기둥(노랑·초록)은 그림만 숨긴다(렌더러는 켠 채 재질 목록만 비운다) — 마커·형상(bounds)·판정은 그대로, 결승선 그림은 옷의 깃발·체크무늬 띠가 맡는다(10-12 fix1).
                    FlappyClassicCourseBuilder.SetFinishMarkerVisible(false, "Build mine course");
                }
                else
                {
                    //  부품·재질이 없어 옷을 못 입혔다(Art 서브모듈을 안 받은 기계 등) — 바탕까지 끄면 빈 배경만 남는다.
                    //  옛 옷은 Dress가 이미 지웠으므로, 앞선 광산 굽기가 꺼 둔 바탕도 다시 켠다.
                    FlappyClassicCourseBuilder.SetClassicBackdropActive(true, "Build mine course");
                    FlappyClassicCourseBuilder.SetFinishMarkerVisible(true, "Build mine course");
                    Debug.LogError("[광산 코스] 옷을 못 입혀 전통 바탕을 켜 둔다 — 회색 박스로 남는다.");
                }
            }

            //  물리 동기를 직접 관리하는 프로젝트라, 부르지 않으면 콜라이더가 만들 때 자리에 남는다(전통 굽기 참고).
            Physics.SyncTransforms();

            FlappyClassicCourseBuilder.EditorSceneManagerSave();
            Debug.Log($"[광산 코스] 관문 {course.Gates.Count}개 (파이프 {pipes}개) · 굴 {course.Tubes.Count}개 (조각 {strips}개)"
                    + $" · 패드 {course.Pads.Count}개 · 갈림길 {course.Forks.Count}개 · 칸막이 {course.Walls.Count}개"
                    + $" · 바닥·천장 조각 {slabs}개 · 길이 {course.Length:F0}m"
                    + $" · 승강기 {movers.Lifts} · 광차 {movers.Carts} · 낙석 {movers.Rockfalls} · 받침목 {movers.Props}"
                    + $" (예상 {course.Length / config.ForwardSpeed:F0}초 @ {config.ForwardSpeed:F1}m/s)");
        }

        //  바닥·천장: Breaks의 이웃 두 x마다 사다리꼴 하나. 앞뒤로 EndMargin만큼 더 뻗는다(통로 선은 끝 밖에서 끝 값).
        private static int BuildFloorAndCeiling(Transform root, MineCourse course, Material skin,
                                                System.Func<float, float> floorAt, System.Func<float, float> ceilingAt)
        {
            List<float> xs = CorridorXs(course);

            int count = 0;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                Vector2[] floor = FlappyMineGeometry.FloorQuad(x0, floorAt(x0), x1, floorAt(x1), FlappyClassicCourseBuilder.WallThickness);
                Vector2[] ceiling = FlappyMineGeometry.CeilingQuad(x0, ceilingAt(x0), x1, ceilingAt(x1), FlappyClassicCourseBuilder.WallThickness);
                if (floor == null || ceiling == null) { continue; }
                FlappyClassicCourseBuilder.Prism(root, $"Floor_{i}", floor, skin);
                FlappyClassicCourseBuilder.Prism(root, $"Ceiling_{i}", ceiling, skin);
                count += 2;
            }
            return count;
        }

        //  바닥·천장 조각과 통로 중심선이 같이 쓰는 x 목록: 스폰 뒤 −EndMargin, Breaks, 결승선 뒤 +EndMargin.
        private static List<float> CorridorXs(MineCourse course)
        {
            var xs = new List<float> { -EndMargin };
            xs.AddRange(course.Breaks(BreakStep));
            xs.Add(course.Length + EndMargin);
            return xs;
        }

        //  굴: 0.25 m 조각마다 지붕(굴 위 가장자리 ~ High/천장선)과 바닥(Low/바닥선 ~ 굴 아래 가장자리) 사다리꼴.
        private static int BuildTubes(Transform root, MineCourse course, Material skin,
                                      System.Func<float, float> floorAt, System.Func<float, float> ceilingAt)
        {
            int count = 0;
            foreach (MineTube tube in course.Tubes)
            {
                int k = 0;
                foreach ((float x0, float x1) in FlappyMineGeometry.Slices(tube.X0, tube.X1, TubeStripStep))
                {
                    Vector2[] roof = FlappyMineGeometry.TubeRoofStrip(tube, x0, x1, ceilingAt);
                    if (roof != null)
                    {
                        FlappyClassicCourseBuilder.Prism(root, $"TubeRoof_{tube.X0:F0}_{k}", roof, skin);
                        count++;
                    }
                    Vector2[] floor = FlappyMineGeometry.TubeFloorStrip(tube, x0, x1, floorAt);
                    if (floor != null)
                    {
                        FlappyClassicCourseBuilder.Prism(root, $"TubeFloor_{tube.X0:F0}_{k}", floor, skin);
                        count++;
                    }
                    k++;
                }
            }
            return count;
        }

        //  통로 중심선 표시 — 없으면 붙이고 있으면 점만 갈아 끼운다(FlappyMapRules와 같은 요령).
        private static void BuildCorridorLine(GameObject composed, MineCourse course)
        {
            var line = composed.GetComponent<LOP.FlappyCorridorLine>();
            if (line == null)
            {
                line = Undo.AddComponent<LOP.FlappyCorridorLine>(composed);
            }
            Undo.RecordObject(line, "Build mine course corridor line");

            List<float> xs = CorridorXs(course);

            var points = new Vector2[xs.Count];
            for (int i = 0; i < xs.Count; i++)
            {
                points[i] = new Vector2(xs[i], course.CenterAt(xs[i]));
            }
            line.Points = points;
            EditorUtility.SetDirty(line);
        }

        //  관문 = 위·아래 파이프. Low/High가 NaN이면 통로 바닥·천장선까지(두께 전체에서 가장 먼 쪽 + 묻기),
        //  아니면 그 높이까지(갈림길 칸 안 관문은 칸막이·천장 사이에서만 막는다).
        private static int BuildGates(Transform root, MineCourse course, Material skin,
                                      System.Func<float, float> floorAt, System.Func<float, float> ceilingAt)
        {
            int count = 0;
            foreach (MineGate g in course.Gates)
            {
                float half = g.Width * 0.5f;
                float low = float.IsNaN(g.Low)
                    ? Mathf.Min(floorAt(g.X - half), floorAt(g.X), floorAt(g.X + half)) - PipeBury
                    : g.Low;
                float high = float.IsNaN(g.High)
                    ? Mathf.Max(ceilingAt(g.X - half), ceilingAt(g.X), ceilingAt(g.X + half)) + PipeBury
                    : g.High;
                float a = g.GapCenter - g.Gap * 0.5f;
                float b = g.GapCenter + g.Gap * 0.5f;

                //  높이가 없는 토막은 세우지 않는다(틈이 칸 끝에 붙은 관문).
                if (a - low > 0f)
                {
                    FlappyClassicCourseBuilder.Pipe(root, $"PipeLow_{g.X:F1}", g.X, low, a, skin, g.Width);
                    count++;
                }
                if (high - b > 0f)
                {
                    FlappyClassicCourseBuilder.Pipe(root, $"PipeHigh_{g.X:F1}", g.X, b, high, skin, g.Width);
                    count++;
                }
            }
            return count;
        }
    }
}
