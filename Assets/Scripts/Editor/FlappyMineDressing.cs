using System.Collections.Generic;
using LOP.MapTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LOP.EditorTools
{
    /// <summary>
    /// 광산 옷 입히기(스펙 §6) — 회색 박스로 구운 코스 위에 블렌더 부품(<c>Assets/Art/Models/Mine</c>)을
    /// <see cref="MineDressingLayout"/>이 정한 자리에 놓고, 범위 안 회색 박스의 <b>렌더러만</b> 끈다(콜라이더는 손대지 않는다).
    ///
    /// <para><b>무엇을 끄나</b>는 이름이 아니라 위치 x로 정한다. 이름은 종류(관문·바닥·천장)를 가를 때만 쓴다.
    /// 관문 파이프는 범위에 조금이라도 걸치면 통째로 입히고 끈다. 바닥·천장 조각은 범위 안에 완전히 든 것만 끄고,
    /// 걸친 조각은 켠 채 두는 대신 바닥·천장 그림을 그 조각 끝까지 이어 덮는다(<see cref="MineDressingLayout.CoverRange"/>).</para>
    ///
    /// <para><b>보이는 면은 판정면(z = 0) 앞에</b> 둔다 — 원근 카메라가 판정면 뒤 면을 가운데로 당겨 그려 틈이 좁아 보이지
    /// 않게(<see cref="FlappyDepthAlignTool"/>와 같은 이유). 관문·바닥·천장 부품은 뒷면 z(Task 3 실측)만큼 앞으로 당긴다.
    /// 10-10 펼치기 1차 부품(갱 입구 문틀·갱도·계단 단·갈림길 바위)은 배치 규칙이 서로 앞뒤까지 맞춘 <b>최종 z</b>를 주므로 그대로 놓는다.</para>
    ///
    /// <para>구역(<see cref="MineDressingLayout.Zones"/>): 노을 바깥 → 갱 입구 문틀(천장은 들보, 문틀은 판정면 뒤) → 수직 갱(갱목이 바닥·천장) →
    /// 계단(바위 단이 바닥·천장) → 그 뒤 비계·바위 천장 + 고수 갈림길 바위·칸막이. 노을 하늘은 문틀 끝까지, 하늘·안개 그러데이션은 문틀 자리.</para>
    ///
    /// <para>전부 렌더 전용이다: 콜라이더 없음, 그림자 끔, 정적 배칭. 결과는 <c>ComposedMap/Dressing</c> 하나에 모이고
    /// 굽기마다 지우고 새로 만든다.</para>
    /// </summary>
    public static class FlappyMineDressing
    {
        private const string DressingName = "Dressing";
        private const string ModelDir = "Assets/Art/Models/Mine";
        private const string MaterialDir = "Assets/Art/Materials/Mine";

        //  배경 난수 시드 — 같은 시드면 같은 배경(굽기를 다시 해도 배경이 안 바뀐다).
        private const int BackgroundSeed = 7;

        //  ── 판정면 앞으로 당기기(부품 뒷면 z, Task 3 실측) ──
        //  관문 셋(GatePlank ±0.65, GateStrap ±0.69, GateCap −0.80~+0.73)은 한 틀에서 만든 것이라 같은 z로 옮긴다.
        private const float GateZ = -0.73f;
        //  TrestleBay ±0.65(RailSpan ±0.55).
        private const float FloorZ = -0.65f;
        //  CeilingRock −1.607~+1.35, Beam ±0.8.
        private const float CeilingRockZ = -1.35f, BeamZ = -0.8f;
        //  ── 카메라 시야(스펙 §2): z −23(10-08, 20에서 물림 — FlappyCameraDistance), 세로 시야 40°, 대시 때 +3 m.
        //  화면 비는 넓은 폰(20:9)보다 조금 넉넉히 ──
        private const float CameraZ = -23f, CameraDash = 3f, HalfFovDegrees = 20f, MaxAspect = 2.4f, ViewMargin = 2f;

        //  하늘 평면 깊이.
        private const float SkyZ = 30f;

        //  하늘·안개 띠의 칸 폭 — 칸 경계마다 그 자리 통로 가운데를 따라간다. 수직 갱(70°)에서도 칸 사이가 크게 꺾이지 않게 2 m.
        //  (10-10 펼치기 1차: 하늘·안개도 띠 메시 하나로 — 노을·굴 판은 원래 10 m 사각형이었다.)
        private const float StripSegment = 2f;
        //  띠 높이를 정하려고 통로 가운데를 훑는 간격.
        private const float CoverSample = 0.5f;

        //  랜턴 빛: 유리 가운데(사슬 위 끝 −1.85, Task 3)에서 조금 앞(랜턴 z ±0.3보다 앞). 가장자리로 갈수록 옅어지는
        //  방사형 텍스처(<c>lantern_glow.png</c>)를 붙인 사각형이라, 원판보다 크게 잡아도 테두리가 안 보인다.
        //  줄에 단 작은 랜턴(축척 0.45)은 셋 다 같은 비율로 줄인다.
        private const float LanternGlassDrop = 1.85f, GlowFront = 0.4f, GlowSize = 2.6f;

        //  10-10: 옛 굴 입구 아치(CaveMouth)는 놓지 않는다(스펙 §5 — 갱 입구 문틀이 대신한다). BoreFrame은 Task 5가 놓지 않기로 했지만
        //  (메시를 고친 뒤 다시 넣는다) 부품 묶음은 Task 3 FBX 14개를 다 싣는다.
        private static readonly string[] PartNames =
        {
            "GatePlank", "GateStrap", "GateCap", "TrestleBay", "RailSpan", "CeilingRock", "Beam",
            "BgFrame", "Ladder", "Walkway", "BgTrestleBay", "MineCart", "Lantern", "SilhouetteStrip",
            "EntranceFrame", "EntranceSign", "LanternString", "ShaftTimber", "CageRail", "Cage", "BenchFace", "ShortScaffold",
            "RockMass", "BoreFrame", "BoltSign", "PadGlow", "DividerWall", "MergeSlope",
        };

        private static readonly string[] MaterialNames =
        {
            "Silhouette_Cave1", "Silhouette_Cave2", "Silhouette_Canyon1", "Silhouette_Canyon2",
            "Sky_Sunset", "Sky_Cave", "Sky_Mouth", "Haze_Outside", "Haze_Cave", "Haze_Mouth",
            "Haze_Far_Outside", "Haze_Far_Cave", "Haze_Far_Mouth", "LanternGlow",
        };

        /// <summary>
        /// <paramref name="composedMap"/>(회색 박스가 이미 구워진 ComposedMap) 아래 <c>Dressing</c>을 다시 만든다.
        /// [<paramref name="dressFrom"/>, <paramref name="dressTo"/>]가 입히는 범위다(10-10 펼치기 1차: −20 ~ 첫 "레일 롤러코스터",
        /// <see cref="MineZones.DressEnd"/>). 배경은 앞뒤 15 m 더.
        /// 부품·재질이 하나라도 없으면 에러만 남기고 아무것도 안 바꾼다(회색 박스 그대로) — 그때 false를 돌려준다.
        /// 부르는 쪽은 false면 전통 바탕을 끄지 않는다(끄면 하늘도 옷도 없는 빈 배경이 남는다).
        /// </summary>
        public static bool Dress(Transform composedMap, MineCourse course, FlappyMineLook look, float dressFrom, float dressTo)
        {
            Transform old = composedMap.Find(DressingName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            if (TryLoadKit(out var models, out var materials) == false)
            {
                return false;
            }

            var dressing = new GameObject(DressingName).transform;
            dressing.SetParent(composedMap, worldPositionStays: false);
            var g = new Groups(dressing);

            //  구역은 코스에서 찾는다(3막 19.8 · 24.8 · 151.25). 옷 끝은 부르는 쪽 범위를 따른다.
            MineZones found = MineDressingLayout.Zones(course);
            var zones = new MineZones(found.OutsideEnd, found.EntranceEnd, dressTo);

            //  1. 회색 박스: 관문은 입히고 끄고, 바닥·천장·굴 조각·칸막이는 범위 안에 완전히 든 것만 끈다(콜라이더는 그대로).
            //     굴 조각(TubeRoof·TubeFloor)과 칸막이(ForkWall)는 갈림길 바위·칸막이 그림이 덮는다. 패드(BoostPad)는 회색이 아니라
            //     판정 사각형을 그대로 보여 주는 것이라 켜 둔다.
            var slabSpans = new List<(float x0, float x1)>();
            int gates = 0, hidden = 0;
            for (int i = 0; i < composedMap.childCount; i++)
            {
                Transform t = composedMap.GetChild(i);
                if (t == dressing) { continue; }
                string n = t.name;
                var r = t.GetComponent<MeshRenderer>();
                if (r == null) { continue; }

                bool low = n.StartsWith("PipeLow_");
                if (low || n.StartsWith("PipeHigh_"))
                {
                    //  Pipe()는 가운데 원점 상자(축척 = 폭·높이)라 트랜스폼이 곧 파이프 사각형이다.
                    Vector3 p = t.position, s = t.localScale;
                    float x0 = p.x - s.x * 0.5f, x1 = p.x + s.x * 0.5f;
                    if (MineDressingLayout.SpanOverlaps(x0, x1, dressFrom, dressTo) == false) { continue; }
                    DressGate(g.Gates, models, p.x, s.x, p.y - s.y * 0.5f, p.y + s.y * 0.5f, capAtTop: low);
                    Hide(r);
                    gates++;
                    hidden++;
                }
                else if (n.StartsWith("Floor_") || n.StartsWith("Ceiling_"))
                {
                    Bounds b = r.bounds;
                    slabSpans.Add((b.min.x, b.max.x));
                    if (MineDressingLayout.SpanInside(b.min.x, b.max.x, dressFrom, dressTo))
                    {
                        Hide(r);
                        hidden++;
                    }
                }
                else if (n.StartsWith("TubeRoof_") || n.StartsWith("TubeFloor_") || n.StartsWith("ForkWall_"))
                {
                    Bounds b = r.bounds;
                    if (MineDressingLayout.SpanInside(b.min.x, b.max.x, dressFrom, dressTo))
                    {
                        Hide(r);
                        hidden++;
                    }
                }
            }

            //  2. 구간 부품(Task 5 배치 규칙). z는 규칙이 준 최종 값 그대로 — 옛 부품처럼 더 당기지 않는다.
            Mesh glowQuad = GlowQuadMesh();
            int entrance = 0, shaft = 0, stairs = 0, forks = 0, lanterns = 0;
            if (zones.OutsideEnd >= dressFrom && zones.EntranceEnd > zones.OutsideEnd && zones.EntranceEnd <= dressTo)
            {
                foreach (MinePiece p in MineDressingLayout.Entrance(course, zones, look.lanternsPerString))
                {
                    PlacePiece(g.Entrance, models, glowQuad, materials, p, ref lanterns);
                    entrance++;
                }
            }
            List<(float from, float to)> shaftSpans = MineDressingLayout.ShaftSpans(course, zones);
            foreach (MinePiece p in MineDressingLayout.ShaftWalls(course, zones, look.shaftTimberSpacing, look.cagePositionRatio))
            {
                PlacePiece(g.Shaft, models, glowQuad, materials, p, ref lanterns);
                shaft++;
            }
            var stairSpans = new List<(float from, float to)>();
            foreach (MinePiece p in MineDressingLayout.StairBenches(course, zones))
            {
                PlacePiece(g.Stairs, models, glowQuad, materials, p, ref lanterns);
                //  바닥 쪽 단(뒤집지 않은 것)은 원점 = 칸 왼끝, 폭 = 칸 x 폭 — 그 칸은 단이 바닥·천장을 맡는다.
                if (p.Kind == MinePartKind.BenchFace && p.Flipped == false) { stairSpans.Add((p.X, p.X + p.Width)); }
                stairs++;
            }
            foreach (MineFork f in course.Forks)
            {
                if (f.X0 < dressFrom || f.X1 > dressTo) { continue; }
                foreach (MinePiece p in MineDressingLayout.ForkPieces(course, f))
                {
                    PlacePiece(g.Forks, models, glowQuad, materials, p, ref lanterns);
                    forks++;
                }
            }

            //  3. 바닥·천장 — 걸친 회색 조각 끝까지 넓힌 범위에서 갱목(수직 갱)·바위 단(계단)이 맡은 구간을 뺀 곳.
            //     비계 레일은 갱 앞(문틀 끝)에서 끝난다(스펙 §5).
            var (coverFrom, coverTo) = MineDressingLayout.CoverRange(slabSpans, dressFrom, dressTo);
            var structure = new List<(float from, float to)>(shaftSpans);
            structure.AddRange(stairSpans);
            List<(float from, float to)> plain = Subtract(coverFrom, coverTo, structure);

            int floor = 0, ceiling = 0;
            foreach (var (from, to) in plain)
            {
                foreach (MinePiece p in MineDressingLayout.TrestleBays(course, from, to))
                {
                    Place(g.Floor, models["TrestleBay"], p, p.Z + FloorZ);
                    Place(g.Floor, models["RailSpan"], p, p.Z + FloorZ);
                    floor++;
                }

                //  천장은 구역마다: 노을 바깥·갱 입구 문틀 = 들보, 굴 안 = 바위.
                //  문틀 구간(19.8~24.8)은 통로가 반 높이 7.28 → 2.8로 좁아져 문틀의 상인방·간판·랜턴 줄(문턱 + 14.3 위)이 판정 천장선보다
                //  4 m쯤 위, 판정 고체 쪽에 온다. 높이 4 m 바위(CeilingRock)를 깔면 그것들을 다 가린다 — 그래서 문틀 구간도 들보(높이 0.8)로
                //  천장선만 표시하고 그 위는 문틀(판정면 뒤 장식)이 보이게 둔다. 노을 바깥과 같은 약속이다(들보 위는 하늘이 보여도 판정 고체).
                if (from < zones.EntranceEnd)
                {
                    foreach (MinePiece p in MineDressingLayout.CeilingPieces(course, from, Mathf.Min(to, zones.EntranceEnd)))
                    {
                        Place(g.Ceiling, models["Beam"], AsBeam(p), p.Z + BeamZ);
                        ceiling++;
                    }
                }
                if (to > zones.EntranceEnd)
                {
                    foreach (MinePiece p in MineDressingLayout.CeilingPieces(course, Mathf.Max(from, zones.EntranceEnd), to))
                    {
                        Place(g.Ceiling, models["CeilingRock"], p, p.Z + CeilingRockZ);
                        ceiling++;
                    }
                }
            }

            //  4. 배경 — 실루엣(재질을 층·구역으로 갈아 끼운다), 먼 비계·광차, 가운데 층, 랜턴 + 빛 원판.
            var density = new MineDressingLayout.BackgroundDensity(look.frameSpacing, look.ladderChance, look.lanternSpacing, look.cartCount,
                                                                   look.farScale, look.midScale);
            int background = 0;
            foreach (MinePiece p in MineDressingLayout.Background(course, dressFrom, dressTo, BackgroundSeed, density))
            {
                switch (p.Kind)
                {
                    case MinePartKind.CaveSilhouette:
                    case MinePartKind.CanyonSilhouette:
                        var sil = Place(g.Silhouettes, models["SilhouetteStrip"], p, p.Z);
                        string mat = (p.Kind == MinePartKind.CaveSilhouette ? "Silhouette_Cave" : "Silhouette_Canyon") + (p.Layer + 1);
                        sil.GetComponent<MeshRenderer>().sharedMaterial = materials[mat];
                        break;
                    case MinePartKind.BgTrestleBay:
                    case MinePartKind.MineCart:
                        Place(g.Far, models[p.Kind.ToString()], p, p.Z);
                        break;
                    case MinePartKind.Lantern:
                        PlacePiece(g.Lights, models, glowQuad, materials, p, ref lanterns);
                        break;
                    default:
                        Place(g.Mid, models[p.Kind.ToString()], p, p.Z);
                        break;
                }
                background++;
            }

            //  5. 하늘(z 30)·안개 막(z 2.2·12) — 노을 바깥 | 갱 입구 문틀(그러데이션 띠 *_Mouth, 텍스처 u 0 = 바깥 → 1 = 굴) | 굴 안.
            //  노을 하늘은 문틀 끝(EntranceEnd)까지다(스펙 §6).
            int strips = 0;
            foreach (var (z, layer, cave, outside, mouth) in new[]
            {
                (SkyZ, g.Sky, "Sky_Cave", "Sky_Sunset", "Sky_Mouth"),
                (MineDressingLayout.HazeZ, g.Haze, "Haze_Cave", "Haze_Outside", "Haze_Mouth"),
                (MineDressingLayout.FarHazeZ, g.Haze, "Haze_Far_Cave", "Haze_Far_Outside", "Haze_Far_Mouth"),
            })
            {
                Vector2 view = ViewHalfSize(z);
                strips += Strip(layer, course, dressFrom - view.x, zones.OutsideEnd, z, view, materials[outside], blend: false);
                strips += Strip(layer, course, zones.OutsideEnd, zones.EntranceEnd, z, view, materials[mouth], blend: true);
                strips += Strip(layer, course, zones.EntranceEnd, dressTo + view.x, z, view, materials[cave], blend: false);
            }

            MakeRenderOnly(dressing.gameObject);
            Undo.RegisterCreatedObjectUndo(dressing.gameObject, "Dress mine course");

            Debug.Log($"[광산 옷] 범위 {dressFrom:F2}~{dressTo:F2} ({zones}) · 바닥·천장 그림 {coverFrom:F2}~{coverTo:F2}"
                    + $" (갱목·바위 단 구간 {structure.Count}개 뺌) · 관문 파이프 {gates}개 · 비계 {floor}칸 · 천장 {ceiling}조각"
                    + $" · 문틀 {entrance} · 갱도 {shaft} · 계단 {stairs} · 갈림길 {forks} · 배경 {background}개 · 랜턴 {lanterns}"
                    + $" · 하늘·안개 띠 {strips}개 · 끈 회색 렌더러 {hidden}개");
            return true;
        }

        //  [from, to]에서 구간들을 뺀 나머지(오름차순, 폭 1e-4 이하는 버린다).
        private static List<(float from, float to)> Subtract(float from, float to, List<(float from, float to)> cuts)
        {
            var sorted = new List<(float from, float to)>(cuts);
            sorted.Sort((a, b) => a.from.CompareTo(b.from));
            var result = new List<(float from, float to)>();
            float cursor = from;
            foreach (var (a, b) in sorted)
            {
                if (b <= cursor) { continue; }
                if (a >= to) { break; }
                if (a - cursor > 1e-4f) { result.Add((cursor, a)); }
                cursor = Mathf.Max(cursor, b);
            }
            if (to - cursor > 1e-4f) { result.Add((cursor, to)); }
            return result;
        }

        //  천장 조각을 들보로 — 같은 현(가운데 원점·아랫면 = 천장선), x 축척만 들보 단위(1 m)로.
        private static MinePiece AsBeam(MinePiece p)
        {
            if (p.Kind == MinePartKind.Beam) { return p; }
            return new MinePiece(MinePartKind.Beam, p.X, p.Y, p.Z, p.AngleDegrees, p.Width, p.Length,
                                 p.Length / MineDressingLayout.BeamUnit, p.ScaleY, p.Flipped, p.Layer);
        }

        //  새 부품(그리고 배경 랜턴)을 규칙이 준 자리·z 그대로. 랜턴은 빛 원판도 같은 축척으로.
        private static void PlacePiece(Transform parent, Dictionary<string, GameObject> models, Mesh glowQuad,
                                       Dictionary<string, Material> materials, MinePiece p, ref int lanterns)
        {
            Place(parent, models[p.Kind.ToString()], p, p.Z);
            if (p.Kind == MinePartKind.Lantern)
            {
                float s = p.ScaleY;
                Glow(parent, glowQuad, materials["LanternGlow"], new Vector3(p.X, p.Y - LanternGlassDrop * s, p.Z - GlowFront * s), GlowSize * s);
                lanterns++;
            }
        }

        // ---- 관문 ----

        //  파이프 사각형 하나를 판자 칸·쇠띠·쇠테로 정확히 채운다. 가로는 1.95 이하 칸으로 이어 깐다(긴 관문도 늘이지 않는다).
        private static void DressGate(Transform parent, Dictionary<string, GameObject> models,
                                      float x, float width, float bottom, float top, bool capAtTop)
        {
            GateStackLayout stack = MineDressingLayout.GateStack(bottom, top, capAtTop);
            foreach (GatePlankColumn col in MineDressingLayout.GateColumns(x, width))
            {
                float cx = col.X, sx = col.ScaleX;
                foreach (GateCell cell in stack.Cells)
                {
                    Instance(parent, models["GatePlank"], new Vector3(cx, cell.Y0, GateZ), 0f, new Vector3(sx, cell.Height, 1f));
                }
                foreach (float y in stack.StrapYs)
                {
                    Instance(parent, models["GateStrap"], new Vector3(cx, y, GateZ), 0f, new Vector3(sx, 1f, 1f));
                }
                if (stack.Cells.Count > 0)
                {
                    //  위 관문(틈이 아래)은 Z축 180° — 원점(윗면)이 아래 끝에 오고 두께가 위로 간다. 볼트(앞 −z)는 그대로 앞이다.
                    Instance(parent, models["GateCap"], new Vector3(cx, stack.CapY, GateZ), stack.CapFlipped ? 180f : 0f, new Vector3(sx, 1f, 1f));
                }
            }
        }

        // ---- 놓기 ----

        private static GameObject Place(Transform parent, GameObject model, MinePiece p, float z)
        {
            return Instance(parent, model, new Vector3(p.X, p.Y, z), p.AngleDegrees + (p.Flipped ? 180f : 0f),
                            new Vector3(p.ScaleX, p.ScaleY, 1f));
        }

        private static GameObject Instance(Transform parent, GameObject model, Vector3 position, float angleZ, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleZ));
            go.transform.localScale = scale;
            return go;
        }

        //  하늘·안개 띠: [x0, x1]을 StripSegment 근처 칸으로 나눈 메시 하나.
        //  칸 경계 x마다 "기준 띠" = 그 자리 통로 가운데 ± view.y(옛 판 높이)이고, 텍스처 v는 기준 띠에서 0(아래) → 1(위)로 잰다.
        //  띠는 그보다 위아래로 넓게 깐다 — 이 x가 화면에 들어오는 카메라(x ± view.x 안)의 통로 가운데 가장 낮은 곳 − view.y부터
        //  가장 높은 곳 + view.y까지. 수직 갱·계단에서 카메라가 22~49 m 내려가도 하늘·안개에 구멍이 나지 않는다.
        //  기준 띠 밖은 텍스처가 클램프라 노을 그러데이션의 위·아래 끝 색이 이어진다(단색 재질은 그대로).
        //  blend면 u = 0(x0) → 1(x1)(입구 그러데이션), 아니면 u = 0.5.
        private static int Strip(Transform parent, MineCourse course, float x0, float x1, float z, Vector2 view, Material material, bool blend)
        {
            if (!(x1 > x0)) { return 0; }
            int n = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / StripSegment));
            float seg = (x1 - x0) / n;
            var vertices = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[n * 6];
            float midX = (x0 + x1) / 2f, midY = course.CenterAt(midX);
            float bandH = view.y * 2f;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, x = Mathf.Lerp(x0, x1, t), cy = course.CenterAt(x);
                //  칸 사이에서 덮개가 모자라지 않게 한 칸 더 넓게 훑는다.
                float lo = cy, hi = cy;
                for (float sx = x - view.x - seg; sx <= x + view.x + seg; sx += CoverSample)
                {
                    float c = course.CenterAt(sx);
                    lo = Mathf.Min(lo, c);
                    hi = Mathf.Max(hi, c);
                }
                float bottom = lo - view.y, top = hi + view.y, bandBottom = cy - view.y;
                //  메시는 띠 가운데(midX, midY, z)를 원점으로 — 정점은 거기서 잰 상대 좌표.
                vertices[i * 2] = new Vector3(x - midX, bottom - midY, 0f);
                vertices[i * 2 + 1] = new Vector3(x - midX, top - midY, 0f);
                float u = blend ? t : 0.5f;
                uvs[i * 2] = new Vector2(u, (bottom - bandBottom) / bandH);
                uvs[i * 2 + 1] = new Vector2(u, (top - bandBottom) / bandH);
            }
            for (int i = 0; i < n; i++)
            {
                int bl = i * 2, tl = bl + 1, br = bl + 2, tr = bl + 3;
                //  카메라(−z)에서 보면 시계 방향 = 앞면(Unity Quad와 같은 감김).
                triangles[i * 6] = bl; triangles[i * 6 + 1] = tr; triangles[i * 6 + 2] = br;
                triangles[i * 6 + 3] = tr; triangles[i * 6 + 4] = bl; triangles[i * 6 + 5] = tl;
            }
            var mesh = new Mesh { name = material.name + "_Strip", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(material.name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = new Vector3(midX, midY, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return 1;
        }

        private static void Glow(Transform parent, Mesh quad, Material material, Vector3 position, float size)
        {
            var go = new GameObject("LanternGlow");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        //  1×1, −Z를 보는 UV 사각형(씬에 한 벌 — 빛 전부가 같이 쓴다). 둥근 모양·옅어짐은 텍스처가 맡는다.
        private static Mesh GlowQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "MineLanternGlowQuad",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                //  카메라(−z)에서 보면 시계 방향 = 앞면.
                triangles = new[] { 0, 3, 1, 3, 0, 2 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        //  깊이 z 평면에서 카메라가 보는 반 폭·반 높이(대시 거리까지, 여유 포함).
        private static Vector2 ViewHalfSize(float z)
        {
            float halfH = (z - CameraZ + CameraDash) * Mathf.Tan(HalfFovDegrees * Mathf.Deg2Rad);
            return new Vector2(halfH * MaxAspect + ViewMargin, halfH + ViewMargin);
        }

        // ---- 렌더 전용 ----

        private static void Hide(Renderer r)
        {
            Undo.RecordObject(r, "Dress mine course");
            r.enabled = false;
        }

        //  콜라이더 없음(FBX 임포트·Quad 기본 콜라이더 제거), 그림자 끔, 정적 배칭.
        private static void MakeRenderOnly(GameObject root)
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // ---- 부품·재질 ----

        private static bool TryLoadKit(out Dictionary<string, GameObject> models, out Dictionary<string, Material> materials)
        {
            models = new Dictionary<string, GameObject>();
            materials = new Dictionary<string, Material>();
            var missing = new List<string>();
            foreach (string name in PartNames)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
                if (m == null) { missing.Add($"{ModelDir}/{name}.fbx"); } else { models[name] = m; }
            }
            foreach (string name in MaterialNames)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");
                if (m == null) { missing.Add($"{MaterialDir}/{name}.mat"); } else { materials[name] = m; }
            }
            if (missing.Count > 0)
            {
                Debug.LogError("[광산 옷] 부품·재질이 없어 입히지 않았다(회색 박스 그대로): " + string.Join(", ", missing));
                return false;
            }
            return true;
        }

        //  Dressing 아래 층별 묶음.
        private sealed class Groups
        {
            public readonly Transform Gates, Floor, Ceiling, Entrance, Shaft, Stairs, Forks, Far, Mid, Silhouettes, Sky, Haze, Lights;

            public Groups(Transform dressing)
            {
                Gates = Child(dressing, "Gates");
                Floor = Child(dressing, "Floor");
                Ceiling = Child(dressing, "Ceiling");
                Entrance = Child(dressing, "Entrance");
                Shaft = Child(dressing, "Shaft");
                Stairs = Child(dressing, "Stairs");
                Forks = Child(dressing, "Forks");
                Transform background = Child(dressing, "Background");
                Far = Child(background, "Far");
                Mid = Child(background, "Mid");
                Silhouettes = Child(dressing, "Silhouettes");
                Sky = Child(dressing, "Sky");
                Haze = Child(dressing, "Haze");
                Lights = Child(dressing, "Lights");
            }

            private static Transform Child(Transform parent, string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, worldPositionStays: false);
                return t;
            }
        }
    }
}
