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
    /// 관문 파이프는 범위에 조금이라도 걸치면 통째로 입히고 끈다. 바닥·천장 조각은 범위 안에 완전히 든 것은 끄고,
    /// 범위에 걸친 것은 렌더 메시만 범위 밖 부분으로 잘라 둔다(<see cref="MineDressingLayout.OutsideX"/> — 콜라이더 메시는 그대로).
    /// 그래서 옷 범위 안에는 회색이 없고, 바닥·천장 그림은 범위 [from, to] 그대로다(10-10 Task 7 Fix 1 — 예전엔 걸친 조각 끝까지 넓혔다).</para>
    ///
    /// <para><b>보이는 면은 판정면(z = 0) 앞에</b> 둔다 — 원근 카메라가 판정면 뒤 면을 가운데로 당겨 그려 틈이 좁아 보이지
    /// 않게(<see cref="FlappyDepthAlignTool"/>와 같은 이유). 관문·바닥·천장 부품은 뒷면 z(Task 3 실측)만큼 앞으로 당긴다.
    /// z는 전부 배치 규칙(<see cref="MineDressingLayout"/>)이 준 <b>최종 값</b>이다 — 관문만 여기서 당긴다.</para>
    ///
    /// <para>구역(<see cref="MineDressingLayout.Zones"/>): 노을 바깥 → 갱 입구 문틀(천장은 들보, 문틀은 판정면 뒤) → 수직 갱(갱목이 바닥·천장) →
    /// 계단(바위 단이 바닥·천장) → 그 뒤 비계·바위 천장 + 고수 갈림길 바위·칸막이 → (10-12 2차) 롤러코스터·급반전·깊은 갱·굴뚝(벽 사다리·랜턴) →
    /// 출구 문틀(입구의 거울, 문설주는 통로 안을 잘라 낸다) → 노을 바깥(절벽 끝·결승선 깃발).
    /// 안개 그러데이션은 입구·출구 문틀 자리, 하늘(z 30)은 시차 때문에 입구 문틀 끝 ± 20 m · 출구 문틀 끝 ± 20 m.</para>
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
        //  (바닥·천장의 FloorZ·CeilingRockZ·BeamZ는 10-10 Task 7 Fix 1부터 MineDressingLayout에 있다.)
        private const float GateZ = -0.73f;
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

        //  하늘(z 30) 노을 → 굴 섞임 반폭(m). 안개 막(z 2.2·12)은 판정면과 거의 같이 움직여 문틀 자리(OutsideEnd ~ EntranceEnd)에서 섞지만,
        //  하늘은 시차가 커서(판정면 x에 보이는 하늘 x = 카메라 x + (x − 카메라 x)·53/23) 고정된 x에서 바꾸면 문틀과 맞출 수 없고,
        //  5 m 띠는 화면을 쓸고 지나가는 세로 경계선이 된다(10-08 캡처). 그래서 문틀 끝(EntranceEnd) ± 20 m에 걸쳐 섞는다(10-08 고침과 같은 폭).
        private const float SkyBlendHalf = 20f;
        //  출구(10-12 2차): 굴 → 노을 섞임 반폭 — 같은 시차 이유로 출구 문틀 끝(ExitEnd) ± 이 값. 입구와 같은 값(스펙 §2).
        private const float ExitSkyBlendHalf = SkyBlendHalf;

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
            //  10-10 Fix 1b 바위 면 장식.
            "StrataLens", "RockSeam", "TimberSet", "Cribbing", "OreVein", "RailScrap",
            //  10-12 펼치기 2차 출구·결승선.
            "ExitSign", "CliffEdge", "FinishFlag",
        };

        private static readonly string[] MaterialNames =
        {
            "Silhouette_Cave1", "Silhouette_Cave2", "Silhouette_Canyon1", "Silhouette_Canyon2",
            "Sky_Sunset", "Sky_Cave", "Sky_Mouth", "Haze_Outside", "Haze_Cave", "Haze_Mouth",
            "Haze_Far_Outside", "Haze_Far_Cave", "Haze_Far_Mouth", "LanternGlow",
            //  10-10 Task 7 Fix 1: 얇은 밝은 테(EdgeRim = RockMass 메시 + 무늬 없는 RockEdge), 무늬 차분한 바위(Strata — MergeSlope 밝은 띠 덮기).
            "RockEdge", "Strata",
            //  10-10 Fix 1b: 지층 렌즈 두 밝기, 어두운 갱목(깊은 갱목·사다리 덮기).
            "StrataPatchDark", "StrataPatchLight", "TimberDim",
        };

        /// <summary>
        /// <paramref name="composedMap"/>(회색 박스가 이미 구워진 ComposedMap) 아래 <c>Dressing</c>을 다시 만든다.
        /// [<paramref name="dressFrom"/>, <paramref name="dressTo"/>]가 입히는 범위다(10-12 펼치기 2차: −20 ~ 코스 끝 + 20 —
        /// 1차는 첫 "레일 롤러코스터"까지였다). 배경은 앞뒤 15 m 더.
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

            //  구역은 코스에서 찾는다(3막 19.8 · 24.8 · 출구 362.5~367.5). 옷 끝은 부르는 쪽 범위를 따른다.
            MineZones found = MineDressingLayout.Zones(course);
            var zones = new MineZones(found.OutsideEnd, found.EntranceEnd, found.ExitStart, found.ExitEnd, dressTo);

            //  1. 회색 박스: 관문은 입히고 끄고, 바닥·천장·굴 조각·칸막이는 범위 안에 완전히 든 것만 끈다(콜라이더는 그대로).
            //     굴 조각(TubeRoof·TubeFloor)과 칸막이(ForkWall)는 갈림길 바위·칸막이 그림이 덮는다. 패드(BoostPad)는 청록 발광 상자라
            //     (따뜻한 색·LOP/Toon 규칙 밖, 열린 방 한가운데 판정면 앞이라 장애물로 읽힌다) 렌더러만 끄고 ⚡ 발판(PadGlow)·⚡ 판이 대신한다 —
            //     FlappyBoostPad 판정은 렌더러를 읽지 않는다(상자에 콜라이더도 없다).
            int gates = 0, hidden = 0, trimmed = 0;
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
                    if (MineDressingLayout.SpanInside(b.min.x, b.max.x, dressFrom, dressTo))
                    {
                        Hide(r);
                        hidden++;
                    }
                    else if (MineDressingLayout.SpanOverlaps(b.min.x, b.max.x, dressFrom, dressTo))
                    {
                        //  범위에 걸친 조각(3막 Floor_117·Ceiling_117, 146.8~178.8) — 범위 안 부분이 판정면 앞(z −2.5)에서 옷을 덮어 회색 상자로 보였다.
                        TrimToOutside(t, r, dressFrom, dressTo);
                        trimmed++;
                    }
                }
                else if (n.StartsWith("TubeRoof_") || n.StartsWith("TubeFloor_") || n.StartsWith("ForkWall_") || n.StartsWith("BoostPad_"))
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
                foreach (MinePiece p in MineDressingLayout.Entrance(course, zones))
                {
                    GameObject go = PlacePiece(g.Entrance, models, glowQuad, materials, p, ref lanterns);
                    //  문틀 기둥·가새는 열린 통로를 가로지른다 — 통로 안 부분을 잘라 낸다(상인방·문턱·통로 밖 기둥 끝만 남는다).
                    if (MineDressingLayout.ClipsToCorridor(p.Kind)) { ClipOutsideCorridor(go, course); }
                    entrance++;
                }
            }
            //  출구 문틀(입구의 거울) — 문설주·가새는 입구처럼 통로 안 부분을 잘라 낸다. 랜턴 줄은 안개 막 뒤(배치 규칙 z 그대로).
            int exit = 0;
            if (zones.HasExit && zones.ExitStart >= dressFrom && zones.ExitEnd <= dressTo)
            {
                foreach (MinePiece p in MineDressingLayout.Exit(course, zones, look.lanternsPerString))
                {
                    GameObject go = PlacePiece(g.Exit, models, glowQuad, materials, p, ref lanterns);
                    if (MineDressingLayout.ClipsToCorridor(p.Kind)) { ClipOutsideCorridor(go, course); }
                    exit++;
                }
            }
            //  결승선 깃발·절벽 끝 — 결승선이 범위 안일 때만.
            int finish = 0;
            if (course.Length >= dressFrom && course.Length <= dressTo)
            {
                foreach (MinePiece p in MineDressingLayout.Finish(course, zones))
                {
                    PlacePiece(g.Finish, models, glowQuad, materials, p, ref lanterns);
                    finish++;
                }
            }
            foreach (MinePiece p in MineDressingLayout.ShaftWalls(course, zones, look.shaftTimberSpacing))
            {
                PlacePiece(g.Shaft, models, glowQuad, materials, p, ref lanterns);
                shaft++;
            }
            foreach (MinePiece p in MineDressingLayout.StairBenches(course, zones))
            {
                PlacePiece(g.Stairs, models, glowQuad, materials, p, ref lanterns);
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

            //  3. 바닥·천장(배치 규칙이 갱목·바위 단 구간을 빼고, 곧고 완만한 곳은 비계·바위 천장, 굽은 곳은 바위 띠 + 밝은 테로 고른다).
            //     비계 칸마다 같은 자리에 레일(RailSpan). 레일 윗면이 바닥선 위 0.28인 것은 10-08부터 그대로다.
            int floor = 0, ceiling = 0;
            foreach (MinePiece p in MineDressingLayout.FloorAndCeiling(course, zones, dressFrom, dressTo))
            {
                Transform parent = p.Layer == 0 ? g.Floor : g.Ceiling;
                PlacePiece(parent, models, glowQuad, materials, p, ref lanterns);
                if (p.Kind == MinePartKind.TrestleBay) { Place(parent, models["RailSpan"], p, p.Z); }
                if (p.Layer == 0) { floor++; } else { ceiling++; }
            }

            //  3b. 바위 면 장식(Fix 1b) — 판정 고체 바위 면 위에만(규칙이 열린 통로·관문·칸막이에서 여유만큼 떨어뜨린다).
            var decor = new MineDressingLayout.RockDecorDensity(look.decorCell, look.decorLensChance, look.decorSeamChance, look.decorPropChance,
                look.decorCribChance, look.decorOreChance, look.decorRailChance, look.decorLanternChance, look.decorLanternMinGap, look.decorMargin,
                look.shaftDeepTimberOffset, look.shaftWallLadder);
            int decorCount = 0;
            foreach (MinePiece p in MineDressingLayout.RockDecor(course, zones, dressFrom, dressTo, BackgroundSeed, decor, look.shaftTimberSpacing))
            {
                PlacePiece(g.Decor, models, glowQuad, materials, p, ref lanterns);
                decorCount++;
            }
            //  3c. 굴뚝 벽 사다리·매단 랜턴 — RockDecor가 이 자리를 비워 두므로 <b>같은 시드</b>(BackgroundSeed)로 불러야 겹치지 않는다.
            int chimney = 0;
            foreach (MinePiece p in MineDressingLayout.ChimneyWalls(course, BackgroundSeed, zones))
            {
                if (p.X < dressFrom || p.X > dressTo) { continue; }
                PlacePiece(g.Chimney, models, glowQuad, materials, p, ref lanterns);
                chimney++;
            }

            //  4. 배경 — 실루엣(재질을 층·구역으로 갈아 끼운다), 먼 비계·광차, 가운데 층, 랜턴 + 빛 원판.
            var density = new MineDressingLayout.BackgroundDensity(look.frameSpacing, look.ladderChance, look.lanternSpacing, look.cartCount,
                                                                   look.farScale, look.midScale, look.farTrestleRun, look.farTrestleGap, look.midCartChance);
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
                        //  발판 위 광차(Fix 1b)는 가운데 층(z 6) — 먼 층 묶음과 가른다.
                        Place(p.Z < MineDressingLayout.FarHazeZ ? g.Mid : g.Far, models[p.Kind.ToString()], p, p.Z);
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

            //  5. 하늘(z 30)·안개 막(z 2.2·12) — 노을 바깥 | 그러데이션 띠(*_Mouth, 텍스처 u 0 = 바깥 → 1 = 굴) | 굴 안
            //     | (10-12 2차, 출구가 있으면) 거꾸로 된 그러데이션 띠(u 1 = 굴 → 0 = 바깥) | 노을 바깥.
            //  안개는 입구·출구 문틀 자리에서 섞고, 하늘은 문틀 끝을 가운데로 넓게 섞는다(스펙 §6 "노을은 문틀까지"를 시차 때문에 고친 것).
            //  하늘만 섞임 띠가 EntranceEnd ± SkyBlendHalf · ExitEnd ± ExitSkyBlendHalf다(시차 — 상수 옆 주석).
            int strips = 0;
            foreach (var (z, layer, cave, outside, mouth, inFrom, inTo, outFrom, outTo) in new[]
            {
                (SkyZ, g.Sky, "Sky_Cave", "Sky_Sunset", "Sky_Mouth",
                 zones.EntranceEnd - SkyBlendHalf, zones.EntranceEnd + SkyBlendHalf, zones.ExitEnd - ExitSkyBlendHalf, zones.ExitEnd + ExitSkyBlendHalf),
                (MineDressingLayout.HazeZ, g.Haze, "Haze_Cave", "Haze_Outside", "Haze_Mouth",
                 zones.OutsideEnd, zones.EntranceEnd, zones.ExitStart, zones.ExitEnd),
                (MineDressingLayout.FarHazeZ, g.Haze, "Haze_Far_Cave", "Haze_Far_Outside", "Haze_Far_Mouth",
                 zones.OutsideEnd, zones.EntranceEnd, zones.ExitStart, zones.ExitEnd),
            })
            {
                Vector2 view = ViewHalfSize(z);
                float end = dressTo + view.x;
                strips += Strip(layer, course, dressFrom - view.x, inFrom, z, view, materials[outside], BlendU.None);
                strips += Strip(layer, course, inFrom, inTo, z, view, materials[mouth], BlendU.OutsideToCave);
                if (zones.HasExit && outFrom >= inTo && outTo <= end)
                {
                    strips += Strip(layer, course, inTo, outFrom, z, view, materials[cave], BlendU.None);
                    strips += Strip(layer, course, outFrom, outTo, z, view, materials[mouth], BlendU.CaveToOutside);
                    strips += Strip(layer, course, outTo, end, z, view, materials[outside], BlendU.None);
                }
                else
                {
                    strips += Strip(layer, course, inTo, end, z, view, materials[cave], BlendU.None);
                }
            }

            //  6. 바위 덩어리(Strata 재질 — RockMass·MergeSlope·바위 단) UV를 월드 좌표 / rockPaintTile로 다시 쓴다(10-11 Fix 1c).
            //     부품 UV는 단위 상자 한 장이라 큰 판에서 칠한 바위가 늘어나고 조각마다 무늬가 끊겼다 — 월드 UV면 축척과 상관없이 같은 크기로 이어진다.
            int worldUv = ApplyWorldUv(dressing, materials["Strata"], look.rockPaintTile);

            MakeRenderOnly(dressing.gameObject);
            Undo.RegisterCreatedObjectUndo(dressing.gameObject, "Dress mine course");

            Debug.Log($"[광산 옷] 범위 {dressFrom:F2}~{dressTo:F2} ({zones}) · 걸친 회색 조각 {trimmed}개는 범위 밖만 남김"
                    + $" · 관문 파이프 {gates}개 · 바닥 {floor}조각 · 천장 {ceiling}조각"
                    + $" · 문틀 {entrance} · 출구 {exit} · 결승선 {finish} · 갱도 {shaft} · 굴뚝 {chimney} · 계단 {stairs} · 갈림길 {forks}"
                    + $" · 바위 면 장식 {decorCount} · 배경 {background}개 · 랜턴 {lanterns}"
                    + $" · 하늘·안개 띠 {strips}개 · 월드 UV 바위 {worldUv}개 · 끈 회색 렌더러 {hidden}개");
            return true;
        }

        //  범위에 걸친 회색 바닥·천장 조각의 렌더 메시를 범위 밖 부분만으로 바꾼다(콜라이더 메시는 따로라 그대로 — 판정 불변).
        //  PrismMesh는 앞면 n개 · 뒷면 n개 · 옆면 4n개 정점이라 앞 n개가 다각형이다.
        private static void TrimToOutside(Transform t, MeshRenderer r, float dressFrom, float dressTo)
        {
            var mf = t.GetComponent<MeshFilter>();
            Mesh m = mf.sharedMesh;
            Vector3[] v = m.vertices;
            int n = v.Length / 6;
            float zNear = v[0].z, zFar = v[n].z;
            var poly = new List<(double x, double y)>();
            for (int i = 0; i < n; i++)
            {
                Vector3 w = t.TransformPoint(v[i]);
                poly.Add((w.x, w.y));
            }
            List<(double x, double y)[]> parts = MineDressingLayout.OutsideX(poly, dressFrom, dressTo);
            if (parts.Count == 0)
            {
                Hide(r);
                return;
            }
            var combine = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                var local = new Vector2[parts[i].Length];
                for (int k = 0; k < local.Length; k++)
                {
                    Vector3 lp = t.InverseTransformPoint(new Vector3((float)parts[i][k].x, (float)parts[i][k].y, 0f));
                    local[k] = new Vector2(lp.x, lp.y);
                }
                combine[i] = new CombineInstance { mesh = FlappyClassicCourseBuilder.PrismMesh(m.name, local, zNear, zFar), transform = Matrix4x4.identity };
            }
            var trimmed = new Mesh { name = m.name + "_Outside" };
            trimmed.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: false);
            trimmed.RecalculateBounds();
            Undo.RecordObject(mf, "Dress mine course");
            mf.sharedMesh = trimmed;
        }

        //  material을 쓰는 렌더러마다 메시를 복사해 그 재질 서브메시 정점의 UV를 월드 (x, y) / tile로 바꾼다. 씬에 든 메시라 굽기마다 새로 만든다.
        private static int ApplyWorldUv(Transform root, Material material, float tile)
        {
            int count = 0;
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                var flags = new bool[mats.Length];
                bool any = false;
                for (int i = 0; i < mats.Length; i++) { flags[i] = mats[i] == material; any |= flags[i]; }
                if (any == false) { continue; }
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { continue; }
                mf.sharedMesh = WorldUvMesh(mf.sharedMesh, mf.transform.localToWorldMatrix, flags, tile);
                count++;
            }
            return count;
        }

        /// <summary>
        /// src의 복사본 — <paramref name="subMeshUsesWorld"/>가 참인 서브메시(모자라면 마지막 값)에 쓰인 정점의 UV를
        /// 월드 (x, y) / <paramref name="tile"/>로 바꾼다. 나머지 정점 UV는 그대로. 맞닿은 두 조각은 맞닿은 점에서 UV가 같다(이음매 없음).
        /// </summary>
        public static Mesh WorldUvMesh(Mesh src, Matrix4x4 toWorld, bool[] subMeshUsesWorld, float tile)
        {
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + "_WorldUv";
            Vector3[] v = mesh.vertices;
            var uv = new List<Vector2>();
            mesh.GetUVs(0, uv);
            while (uv.Count < v.Length) { uv.Add(Vector2.zero); }
            var hit = new bool[v.Length];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                bool use = subMeshUsesWorld.Length == 0 ? false : subMeshUsesWorld[Mathf.Min(s, subMeshUsesWorld.Length - 1)];
                if (use == false) { continue; }
                foreach (int i in mesh.GetTriangles(s)) { hit[i] = true; }
            }
            for (int i = 0; i < v.Length; i++)
            {
                if (hit[i] == false) { continue; }
                Vector3 w = toWorld.MultiplyPoint3x4(v[i]);
                uv[i] = new Vector2(w.x / tile, w.y / tile);
            }
            mesh.SetUVs(0, uv);
            return mesh;
        }

        //  부품 인스턴스의 메시를 열린 통로 밖 부분만으로 바꾼다(삼각형마다 MineDressingLayout.ClipOutsideCorridor — 위치·법선·UV는 같은 무게로 섞는다).
        //  씬에 든 메시라 굽기마다 새로 만든다(원본 FBX는 그대로).
        private static void ClipOutsideCorridor(GameObject go, MineCourse course)
        {
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh src = mf.sharedMesh;
                if (src == null) { continue; }
                Matrix4x4 toWorld = mf.transform.localToWorldMatrix;
                Vector3[] v = src.vertices;
                Vector3[] nrm = src.normals;
                Vector2[] uv = src.uv;
                bool hasN = nrm != null && nrm.Length == v.Length, hasUv = uv != null && uv.Length == v.Length;
                var outV = new List<Vector3>();
                var outN = new List<Vector3>();
                var outUv = new List<Vector2>();
                var subs = new List<List<int>>();
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    var tris = new List<int>();
                    int[] t = src.GetTriangles(s);
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        int a = t[k], b = t[k + 1], c = t[k + 2];
                        Vector3 wa = toWorld.MultiplyPoint3x4(v[a]), wb = toWorld.MultiplyPoint3x4(v[b]), wc = toWorld.MultiplyPoint3x4(v[c]);
                        foreach (var poly in MineDressingLayout.ClipOutsideCorridor(course, (wa.x, wa.y), (wb.x, wb.y), (wc.x, wc.y)))
                        {
                            int start = outV.Count;
                            foreach (var (w0, w1, w2) in poly)
                            {
                                float f0 = (float)w0, f1 = (float)w1, f2 = (float)w2;
                                outV.Add(v[a] * f0 + v[b] * f1 + v[c] * f2);
                                outN.Add(hasN ? (nrm[a] * f0 + nrm[b] * f1 + nrm[c] * f2).normalized : Vector3.back);
                                outUv.Add(hasUv ? uv[a] * f0 + uv[b] * f1 + uv[c] * f2 : Vector2.zero);
                            }
                            //  볼록 다각형 → 부채꼴, 감김은 원래 삼각형 그대로(무게 공간에서 자르기만 했다).
                            for (int i = 1; i + 1 < poly.Count; i++) { tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1); }
                        }
                    }
                    subs.Add(tris);
                }
                var mesh = new Mesh { name = src.name + "_OutsideCorridor" };
                mesh.indexFormat = outV.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(outV);
                mesh.SetNormals(outN);
                mesh.SetUVs(0, outUv);
                mesh.subMeshCount = subs.Count;
                for (int s = 0; s < subs.Count; s++) { mesh.SetTriangles(subs[s], s); }
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;
            }
        }

        //  새 부품(그리고 배경 랜턴)을 규칙이 준 자리·z 그대로. 랜턴은 빛 원판도 같은 축척으로.
        //  EdgeRim은 RockMass 메시에 RockEdge(무늬 없는 밝은 색) 재질, MergeSlope는 빗변 밝은 띠까지 바위 재질(Strata)로 —
        //  밝은 것은 판정선 위 얇은 테 하나만 남긴다(사용자 10-10 "디자인이 시끄럽다"; 갈림길 방 끝 합류 경사의 밝은 사선도 이것으로 사라진다).
        private static GameObject PlacePiece(Transform parent, Dictionary<string, GameObject> models, Mesh glowQuad,
                                             Dictionary<string, Material> materials, MinePiece p, ref int lanterns)
        {
            string model = ModelName(p.Kind);
            GameObject go = Place(parent, models[model], p, p.Z);
            string over = OverrideMaterial(p);
            if (over != null)
            {
                Material m = materials[over];
                foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) { mats[i] = m; }
                    r.sharedMaterials = mats;
                }
                go.name = p.Kind.ToString();
            }
            if (p.Kind == MinePartKind.Lantern)
            {
                float s = p.ScaleY;
                Glow(parent, glowQuad, materials["LanternGlow"], new Vector3(p.X, p.Y - LanternGlassDrop * s, p.Z - GlowFront * s), GlowSize * s);
                lanterns++;
            }
            return go;
        }

        //  부품 종류 → FBX 이름. 메시를 빌려 쓰는 종류(테·깊은 갱목·벽 사다리)만 다르다.
        private static string ModelName(MinePartKind kind) => kind switch
        {
            MinePartKind.EdgeRim => "RockMass",
            MinePartKind.DeepTimber => "ShaftTimber",
            MinePartKind.WallLadder => "Ladder",
            _ => kind.ToString(),
        };

        //  인스턴스 재질을 통째로 갈아 끼울 종류(없으면 null — FBX 리매핑 그대로).
        //  EdgeRim = 무늬 없는 밝은 테, MergeSlope = 빗변 밝은 띠까지 바위, 깊은 갱목·벽 사다리 = 어두운 갱목, 지층 렌즈 = 층(0 어둡게·1 밝게).
        private static string OverrideMaterial(MinePiece p) => p.Kind switch
        {
            MinePartKind.EdgeRim => "RockEdge",
            MinePartKind.MergeSlope => "Strata",
            MinePartKind.DeepTimber => "TimberDim",
            MinePartKind.WallLadder => "TimberDim",
            MinePartKind.StrataLens => p.Layer == 1 ? "StrataPatchLight" : "StrataPatchDark",
            _ => null,
        };

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

        //  z 축척은 배치 규칙(MineDressingLayout.DepthScale) — 바위 덩어리·테·단은 납작하게(0.1).
        private static GameObject Place(Transform parent, GameObject model, MinePiece p, float z)
        {
            return Instance(parent, model, new Vector3(p.X, p.Y, z), p.AngleDegrees + (p.Flipped ? 180f : 0f),
                            new Vector3(p.ScaleX, p.ScaleY, MineDressingLayout.DepthScale(p.Kind)));
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
        //  u: 입구 그러데이션은 0(x0) → 1(x1), 출구 그러데이션은 거꾸로 1(x0, 굴) → 0(x1, 바깥), 단색 띠는 0.5.
        private enum BlendU { None, OutsideToCave, CaveToOutside }

        private static int Strip(Transform parent, MineCourse course, float x0, float x1, float z, Vector2 view, Material material, BlendU blend)
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
                float u = blend == BlendU.OutsideToCave ? t : blend == BlendU.CaveToOutside ? 1f - t : 0.5f;
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
            public readonly Transform Gates, Floor, Ceiling, Entrance, Exit, Finish, Shaft, Chimney, Stairs, Forks, Decor, Far, Mid, Silhouettes, Sky, Haze, Lights;

            public Groups(Transform dressing)
            {
                Gates = Child(dressing, "Gates");
                Floor = Child(dressing, "Floor");
                Ceiling = Child(dressing, "Ceiling");
                Entrance = Child(dressing, "Entrance");
                Exit = Child(dressing, "Exit");
                Finish = Child(dressing, "Finish");
                Shaft = Child(dressing, "Shaft");
                Chimney = Child(dressing, "Chimney");
                Stairs = Child(dressing, "Stairs");
                Forks = Child(dressing, "Forks");
                Decor = Child(dressing, "Decor");
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
