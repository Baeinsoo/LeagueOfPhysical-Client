using System;
using System.Collections.Generic;
using System.Linq;

namespace LOP.MapTools
{
    /// <summary>
    /// 광산 옷 부품 종류. 이름은 Art/Models/Mine의 FBX 이름과 같다(실루엣 둘은 SilhouetteStrip 하나를 재질만 바꿔 쓴다).
    /// 10-10 펼치기 1차 부품 14종(Task 3 보고 표)과, 바위 가장자리 얇은 밝은 테 <see cref="EdgeRim"/>(RockMass 메시 + 무늬 없는 밝은 재질)을 뒤에 더했다.
    /// </summary>
    public enum MinePartKind
    {
        TrestleBay, CeilingRock, Beam,
        BgTrestleBay, MineCart, BgFrame, Ladder, Walkway, Lantern, CaveSilhouette, CanyonSilhouette,
        EntranceFrame, EntranceSign, LanternString, ShaftTimber, CageRail, Cage, BenchFace, ShortScaffold,
        RockMass, BoreFrame, BoltSign, PadGlow, DividerWall, MergeSlope,
        /// <summary>관문 쇠테(GateCap.fbx). 관문 기둥 끝에만 쓴다(Task 7 Fix 1부터 바위 가장자리 테는 <see cref="EdgeRim"/>).</summary>
        GateCap,
        /// <summary>
        /// 바위의 닿는 가장자리 얇은 밝은 테(Task 7 Fix 1, 사용자 10-10 "디자인이 시끄럽다") — RockMass 메시(왼끝 아래 원점, 1×1, z ±1.3)에
        /// 무늬 없는 밝은 재질(RockEdge)을 씌운 띠. 볼트·두꺼운 쇠테 없이 판정선 위에 <see cref="MineDressingLayout.RimThickness"/>만.
        /// </summary>
        EdgeRim,
    }

    /// <summary>
    /// 옷 구역의 x(스펙 §2·§5) — 전부 코스(<see cref="MineCourse.FlatEnd"/>·<see cref="MineCourse.Sections"/>)에서 찾는다.
    /// [−20, <see cref="OutsideEnd"/>] 노을 바깥, [<see cref="OutsideEnd"/>, <see cref="EntranceEnd"/>] 갱 입구 문틀, 그 뒤 굴 안,
    /// 옷은 <see cref="DressEnd"/>까지.
    /// </summary>
    public readonly struct MineZones
    {
        /// <summary>노을 바깥 끝 = 처음 통로가 꺾이거나 좁아지기 시작하는 x(3막 19.8).</summary>
        public readonly float OutsideEnd;
        /// <summary>갱 입구 문틀 끝 = 첫 "수직 갱 낙하" 구간 시작 x(3막 24.8). 노을 하늘·협곡은 여기까지, 굴 배경은 여기부터.</summary>
        public readonly float EntranceEnd;
        /// <summary>옷 범위 끝 = 첫 "레일 롤러코스터" 구간 시작 x(3막 151.25).</summary>
        public readonly float DressEnd;

        public MineZones(float outsideEnd, float entranceEnd, float dressEnd)
        {
            OutsideEnd = outsideEnd; EntranceEnd = entranceEnd; DressEnd = dressEnd;
        }

        public override string ToString() => $"바깥 ~{OutsideEnd:F2} · 문틀 ~{EntranceEnd:F2} · 옷 ~{DressEnd:F2}";
    }

    /// <summary>
    /// 부품 하나의 자리. (<see cref="X"/>, <see cref="Y"/>, <see cref="Z"/>) = 부품 <b>원점</b>이 갈 곳(원점 규칙은 Task 3 보고 표),
    /// <see cref="AngleDegrees"/> = 원점 둘레 Z축 회전(반시계 +). 축척은 부품 원래 크기에 곱한다.
    /// <see cref="Flipped"/>면 회전에 Z축 180°를 더한다(위아래 뒤집기 — 천장 쪽 실루엣).
    /// </summary>
    public readonly struct MinePiece : IEquatable<MinePiece>
    {
        public readonly MinePartKind Kind;
        public readonly float X, Y, Z, AngleDegrees;
        /// <summary>이어 까는 조각이 덮는 x 폭(수평). 배경 소품은 부품 폭.</summary>
        public readonly float Width;
        /// <summary>이어 까는 조각의 실제 길이 = 양끝을 잇는 현의 길이(기울면 <see cref="Width"/>보다 길다).</summary>
        public readonly float Length;
        public readonly float ScaleX, ScaleY;
        public readonly bool Flipped;
        /// <summary>같은 종류 안의 층(실루엣: 0 = 가까운 z 13, 1 = 먼 z 15). 그 밖엔 0.</summary>
        public readonly int Layer;

        public MinePiece(MinePartKind kind, float x, float y, float z, float angleDegrees, float width, float length,
                         float scaleX, float scaleY, bool flipped = false, int layer = 0)
        {
            Kind = kind; X = x; Y = y; Z = z; AngleDegrees = angleDegrees; Width = width; Length = length;
            ScaleX = scaleX; ScaleY = scaleY; Flipped = flipped; Layer = layer;
        }

        public bool Equals(MinePiece o) => Kind == o.Kind && X == o.X && Y == o.Y && Z == o.Z && AngleDegrees == o.AngleDegrees
            && Width == o.Width && Length == o.Length && ScaleX == o.ScaleX && ScaleY == o.ScaleY && Flipped == o.Flipped && Layer == o.Layer;

        public override bool Equals(object obj) => obj is MinePiece o && Equals(o);

        public override int GetHashCode() => HashCode.Combine(Kind, X, Y, Z, AngleDegrees, ScaleX, ScaleY, Layer);

        public override string ToString() => $"{Kind}({X:F2},{Y:F2},{Z:F2}) {AngleDegrees:F1}° s=({ScaleX:F2},{ScaleY:F2}){(Flipped ? " 뒤집힘" : "")} L{Layer}";
    }

    /// <summary>기둥 한 칸. GatePlank(아래 끝 원점, 1 m)를 <see cref="Y0"/>에 놓고 y 축척 = <see cref="Height"/>.</summary>
    public readonly struct GateCell
    {
        public readonly float Y0, Height;

        public GateCell(float y0, float height)
        {
            Y0 = y0; Height = height;
        }
    }

    /// <summary>관문 가로 칸 하나. GatePlank·GateStrap·GateCap(폭 1.95, 가운데 원점)을 <see cref="X"/>에 놓고 x 축척 = <see cref="ScaleX"/>.</summary>
    public readonly struct GatePlankColumn
    {
        public readonly float X, ScaleX;

        public GatePlankColumn(float x, float scaleX)
        {
            X = x; ScaleX = scaleX;
        }
    }

    /// <summary>관문 기둥 하나(파이프 한 개)를 채우는 판자 칸·쇠띠·쇠테 자리.</summary>
    public sealed class GateStackLayout
    {
        /// <summary>아래부터 위로. 마지막 칸만 1 m보다 짧을 수 있다.</summary>
        public readonly List<GateCell> Cells = new List<GateCell>();
        /// <summary>GateStrap(가운데 원점) y. 틈 쪽 끝에서 2 m마다, 기둥 안에 완전히 드는 것만.</summary>
        public readonly List<float> StrapYs = new List<float>();
        /// <summary>
        /// GateCap 원점 y = 틈 쪽 끝. GateCap은 윗면이 원점(두께 0.4, 아래로)이라 아래 관문(틈이 위)은 그대로 top에 두고,
        /// 위 관문(틈이 아래)은 <see cref="CapFlipped"/> — Z축 180°로 뒤집어 bottom에 둔다(그러면 위로 0.4).
        /// </summary>
        public float CapY;
        public bool CapFlipped;
    }

    /// <summary>
    /// 광산 옷(스펙 §6)의 <b>놓을 자리</b> — 순수 계산. 굽기(<c>FlappyMineDressing</c>)는 여기서 받은 자리에 부품을 놓기만 한다.
    ///
    /// <para><b>그림 경계 = 판정 경계</b>(스펙 §4): 데크 윗면은 바닥선(<c>CenterAt − HalfAt</c>), 천장 조각 아랫면은 천장선
    /// (<c>CenterAt + HalfAt</c>), 기둥 칸은 파이프 높이를 정확히 채운다. 이어 까는 조각(비계·천장)은 양끝을 그 선 위의 두 점에
    /// 맞춘 <b>현</b>이다 — 가운데 y = 양끝 평균, 각도 = 현의 기울기, 길이 = 현의 길이.</para>
    ///
    /// <para><b>칸은 꺾이는 곳에서 끊는다</b>(10-10): 회색 박스 바닥·천장은 <c>Breaks(</c><see cref="BreakStep"/><c>)</c>의 x마다 꺾이는
    /// 꺾은선이므로, 칸 끊는 점도 범위 양끝 + 그 x들이다. 바닥선·천장선이 둘 다 곧게 이어지는 구간은 합쳐 단위 폭 근처 같은 칸으로
    /// 나누고(<see cref="BentTiles"/>), 그래서 칸 양끝이 판정선 위에 있고 칸 안에서 판정선이 꺾이지 않는다 — 현이 곧 판정선이다.
    /// 전이·물결 구간은 0.5 m마다 꺾이므로 그곳 칸은 0.5 m로 짧다.</para>
    ///
    /// <para>부품 크기·원점은 Task 3 보고(<c>.superpowers/sdd/2026-10-07-flappy-mine-look-slice1/task-3-report.md</c>)의 유니티 실측값이다.</para>
    /// </summary>
    public static class MineDressingLayout
    {
        //  ── 구역 ──
        //  10-10: 옛 상수(바깥 끝 24 · 굴 안 34 · 굴 입구 아치 29)는 지웠다 — 구역은 코스에서 찾는다(<see cref="Zones"/>).
        /// <summary>구간 이름(<see cref="MineCourse.Sections"/>) — <c>MineCourseRule</c>의 이름과 같다.</summary>
        public const string ShaftSection = "수직 갱 낙하", StairSection = "계단 내리막", CoasterSection = "레일 롤러코스터";
        /// <summary>배경 층은 카메라 시야를 덮도록 범위 앞뒤로 이만큼 더 깐다.</summary>
        public const float BackgroundMargin = 15f;

        //  ── 깊이(Global Constraints, 10-08 고침) ──
        //  먼 비계는 z 11에서 관문과 다퉜다(캡처) — 18로 물렸고, 실루엣은 그보다 뒤(22·25)로 같이 물렸다.
        //  안개 막(2.2) 뒤 층은 전부 안개를 한 겹 쓰고, 먼 비계·실루엣은 가운데 층 뒤 안개 막(FarHazeZ 12)을 한 겹 더 쓴다 —
        //  한 겹(0.4)만으로는 먼 비계가 관문만큼 진했다(10-08 캡처). 두 겹이면 1 − 0.6² = 0.64.
        public const float HazeZ = 2.2f, LanternZ = 4.5f, MidZ = 6f, FarHazeZ = 12f, FarZ = 18f, SilhouetteNearZ = 22f, SilhouetteFarZ = 25f;

        //  ── 부품 단위(Task 3 실측) ──
        /// <summary>TrestleBay·RailSpan x ±1.2(가운데 원점).</summary>
        public const float TrestleBayWidth = 2.4f;
        /// <summary>천장 조각 간격. CeilingRock x ±1.0(가운데, 아랫면 원점) — 앞면 굴곡 주기 2라 이음매가 맞물린다.</summary>
        public const float CeilingPieceWidth = 2f, CeilingRockUnit = 2f;
        /// <summary>Beam x ±0.5(가운데, 아랫면 원점) — 1 m 단위라 x 축척으로 늘인다.</summary>
        public const float BeamUnit = 1f;
        /// <summary>
        /// 판정면 앞으로 당긴 z(부품 뒷면이 판정면): TrestleBay ±0.65(RailSpan ±0.55), CeilingRock −1.607~+1.35, Beam ±0.8.
        /// (10-10 Fix 1: 굽기에 있던 값을 옮겨 왔다 — <see cref="FloorAndCeiling"/>가 최종 z를 준다.)
        /// </summary>
        public const float FloorZ = -0.65f, CeilingRockZ = -1.35f, BeamZ = -0.8f;
        /// <summary>
        /// 비계(다리 12 m)·바위 천장(높이 4 m)을 놓는 곧은 구간 조건(10-10 Task 7 Fix 1): 기울기 이하·길이 이상. 그 밖(굽은·짧은·가파른 칸)은
        /// 다리 없는 바위 띠 + 밝은 테 — 기운 상자의 다리·옆면이 이웃 골짜기의 열린 통로로 빠져나오기 때문이다(캡처 1.90 m·1.20 m).
        /// </summary>
        public const float TrestleMaxSlopeDegrees = 15f, StraightMinRun = 1.2f;
        /// <summary>
        /// 바위 띠 두께(판정선에서 바깥으로 수직) — 화면 위·아래 끝까지 차게(카메라 반 시야 8.4 − 낮은 천장 반 높이 2.2 + 물결 높이차·대시 여유).
        /// 사용자 10-10: "판정 바깥은 차분한 어두운 바위 덩어리로 채운다".
        /// </summary>
        public const float LineRockDepth = 10f;
        /// <summary>GatePlank 한 칸 높이(아래 끝 원점, y 0~1).</summary>
        public const float PlankCell = 1f;
        /// <summary>GateStrap 반 두께(y ±0.08), GateCap 두께(윗면 원점, 아래로 0.4).</summary>
        public const float StrapHalf = 0.08f, CapThickness = 0.4f;
        /// <summary>쇠띠 간격.</summary>
        public const float StrapSpacing = 2f;
        /// <summary>BgTrestleBay x ±1.5, 레일 윗면 +0.28(RailSpan도 같다) — 광차는 여기에 선다.</summary>
        public const float BgTrestleBayWidth = 3f, RailTop = 0.28f;
        /// <summary>
        /// SilhouetteStrip x ±10 — 양끝 높이가 같아 이어 깔 수 있다. z 13·15에서 22·25로 물리며 화면에서 같은 크기로 보이게
        /// 카메라 거리 비(45/33 ≈ 1.36)만큼 키운다 — 띠 하나가 덮는 폭 = 20 × 1.36.
        /// </summary>
        public const float SilhouetteScaleX = 1.36f, SilhouetteWidth = 20f * SilhouetteScaleX;
        /// <summary>GatePlank·GateStrap·GateCap x ±0.975 — 보통 관문 폭.</summary>
        public const float GateUnitWidth = 1.95f;
        /// <summary>BgFrame x ±3.3(윗면 원점), Walkway x ±3.0(윗면 원점), Ladder 위 끝 원점.</summary>
        public const float BgFrameWidth = 6.6f, WalkwayWidth = 6f, LadderWidth = 0.82f, LanternWidth = 0.6f, MineCartWidth = 1.95f;

        //  ── 펼치기 1차 부품(10-10, Task 3 보고 표의 실측 원점·크기) ──
        //  z는 <b>최종 값</b>이다(굽기가 더 당기지 않는다). 닿는 부품(바위·테·칸막이·단·갱목·발판)은 뒷면이 판정면(z 0)에 오게 —
        //  원근 카메라가 판정면 뒤 면을 틈 쪽으로 당겨 그려 틈이 좁아 보이지 않게(⚡ 판은 판정 고체인 칸막이 앞면에 붙는다).
        //  장식(문틀·간판)은 앞면이 판정면 뒤(z &gt; 0). 바위 덩어리·테·단은 깊이 축척 0.1로 납작하다(<see cref="DepthScale"/>).
        /// <summary>
        /// EntranceFrame x ±2.5(문턱 가운데 원점), 기둥 안쪽 면 x ±1.9, 상인방 y 14.62~15.42(문턱 기준), z 0.03~0.68.
        /// 간판(높이 1) 가운데는 아랫변이 상인방 아랫변(14.62)에 오게 — 문틀 안 통로(14.56) 위.
        /// </summary>
        public const float EntranceHalfWidth = 2.5f, EntrancePostInner = 1.9f, EntranceSignCenter = 15.12f;
        /// <summary>문틀(z 0.03~)은 그대로 판정면 뒤. 간판(z −0.085~0.07)은 상인방 앞면에 붙여 앞면이 판정면 바로 뒤.</summary>
        public const float EntranceFrameZ = 0f, EntranceSignZ = 0.09f;
        //  랜턴 줄(LanternString + 작은 Lantern 4개)은 Task 7 Fix 1에서 뺐다(사용자 10-10 "3D 모델이 투머치" — 입구는 상인방 + 간판만).

        /// <summary>
        /// ShaftTimber 높이(왼끝 아래 원점, y 0~0.4, z ±0.5), 한 조각 길이 상한(실제 갱목 크기 — 3 m로 늘이면 거대한 판자로 읽혔다),
        /// 갱 벽 두께(판정선에서 바깥으로 — 카메라 반 시야 + 대시 여유).
        /// </summary>
        public const float ShaftTimberHeight = 0.4f, ShaftTimberUnit = 1.5f, ShaftWallDepth = 12f, ShaftTimberZ = -0.5f;
        /// <summary>벽 갱목 줄 수(Task 7 Fix 1: 7 m 16줄 → 두 줄. 그 뒤는 바위).</summary>
        public const int ShaftTimberRows = 2;
        /// <summary>
        /// 갱목 뒤 바위(RockMass + 삼각형): 판정선에서 수직으로 이만큼 바깥(두 줄 갱목 뒤에 숨는 깊이)의 선 바깥, 가장 바깥 꼭짓점 너머 <see cref="ShaftWallDepth"/>까지.
        /// 앞면이 판정면 바로 뒤(z 0.02)라 갱목(z −1~0)을 가리지 않는다.
        /// </summary>
        public const float ShaftRockInset = 0.65f, ShaftRockZ = 0.15f;
        //  승강 케이지·레일(Cage·CageRail)은 Task 7 Fix 1에서 뺐다 — 2 m 통로 안 상자가 장애물로 읽혔고(캡처), 사용자 10-10 "투머치".

        /// <summary>BenchFace(윗면 앞 모서리 왼끝, 메시 z −1.35~1.3) — 깊이 축척 0.1(<see cref="DepthScale"/>)이라 z −0.265~0.</summary>
        public const float BenchZ = -0.13f;
        //  짧은 비계(ShortScaffold)는 Task 7 Fix 1에서 뺐다 — 계단 통로 안 탁자·발판처럼 장애물로 읽혔고(캡처), 사용자 10-10 "투머치".

        /// <summary>
        /// 부품의 z 축척(굽기가 localScale.z로 쓴다). 바위 덩어리·삼각형·얇은 테·바위 단은 0.1 — 사용자 10-10("디자인이 시끄럽다"):
        /// 깊이 2.6 m 상자는 원근으로 윗면이 밝게 드러나 두꺼운 밝은 띠로 보였다. 납작하게 판정면 바로 앞에 붙이면 차분한 면이 되고
        /// 그림 경계도 판정 경계에 더 붙는다. 나무 구조물(관문·비계·갱목·칸막이)과 빛 판은 그대로 1.
        /// </summary>
        public static float DepthScale(MinePartKind kind) =>
            kind == MinePartKind.RockMass || kind == MinePartKind.MergeSlope || kind == MinePartKind.EdgeRim || kind == MinePartKind.BenchFace ? FlatDepth : 1f;

        /// <summary><see cref="DepthScale"/>의 납작한 값.</summary>
        public const float FlatDepth = 0.1f;

        /// <summary>
        /// 굴 회색 박스 조각 폭 — <c>FlappyMineCourseBuilder.TubeStripStep</c>과 같아야 한다(바위 테가 같은 현을 따라간다).
        /// </summary>
        public const float TubeStripStep = 0.25f;
        /// <summary>
        /// RockMass(왼끝 아래, 메시 z ±1.3, 깊이 축척 0.1 → z −0.26~0) — 뒷면 0. 밝은 테(<see cref="MinePartKind.EdgeRim"/> = RockMass 메시, z −0.28~−0.02,
        /// 두께 0.12)는 바위 앞면(−0.26)·MergeSlope 앞면(−0.267)보다 앞.
        /// 테가 틈 쪽으로 삐져나올 수 있는 곳(바위가 90° 넘게 볼록하게 꺾이는 V, 굴 x 폭 끝에서 기운 테)은 끊지 않고 y 축척으로 얇게 해
        /// 삐져나온 깊이를 <see cref="RimSlack"/> 이하로 둔다(그림 경계 = 판정 경계 ± 0.05).
        /// </summary>
        public const float RockZ = -0.13f, RimZ = -0.15f, RimThickness = 0.12f, RimSlack = 0.04f;
        /// <summary>MergeSlope(왼끝 아래, 메시 z −1.35~1.3, 깊이 축척 0.1 → −0.267~0) — 바위 앞면과 겹치지 않게 조금 앞. 방 끝에서 덮는 폭.</summary>
        public const float MergeSlopeZ = -0.132f, MergeSlopeWidth = 2.4f;
        /// <summary>DividerWall(왼끝 가운데, y ±0.6 = 1.2, z ±0.75).</summary>
        public const float DividerZ = -0.75f, DividerHeight = 1.2f;
        //  BoreFrame(굴 입구 문틀)은 Fix round 1부터 놓지 않는다: 상인방·문턱이 x −0.3~1.0 곧은 각재라 굴 앞에 두면 열린 칸 위,
        //  굴 안에 두면 휘는 틈 위에 판정면 앞(앞면 −1.84)으로 걸치고, 틈을 가로지르는 문설주(z 0.2~0.5)를 판정면 뒤에 두려면
        //  바위(앞면 −2.6)에 묻힌다. 메시를 고친 뒤(문설주를 뒤로, 상인방·문턱을 짧게·앞으로) 다시 넣는다. 굴 시작 세로 면은 밝은 테가 맡는다.
        /// <summary>
        /// BoltSign(가운데, x ±0.95, y ±0.547, z −0.11~0.06) — 칸막이(판정 고체) 앞머리 앞면에 붙인다: 칸막이 앞면(−1.5)보다 0.02 앞,
        /// 높이는 칸막이 위·아래에서 <see cref="BoltSignInset"/> 안쪽까지 줄인다(Task 7 Fix 1: 굴 입구 앞 열린 칸 판정면 뒤에 떠 있어 장애물로 읽혔다).
        /// </summary>
        public const float BoltSignZ = -1.58f, BoltSignHalfWidth = 0.95f, BoltSignHalfHeight = 0.547f, BoltSignNose = 0.3f, BoltSignInset = 0.05f;
        /// <summary>PadGlow(가운데, y ±0.15, z −0.67~0.65) — 윗면이 방 바닥선, 밝은 테보다 앞(z −1.62~−0.30, 납작한 바위 바로 앞).</summary>
        public const float PadGlowZ = -0.95f, PadGlowHalfHeight = 0.15f;

        //  ── 배경 모양(블렌더 시안 mine_variants5.py의 값을 통로 가운데 기준으로) ──
        //  먼 비계 높이: −1.0 + 2.5·sin(i·0.45), i = 3 m 칸 번호 → x로 이으면 sin(0.15·x).
        const double FarBase = -1.0, FarAmp = 2.5, FarPhasePerBay = 0.45;
        //  가운데 층: 틀 윗면 +6.5, 사다리 위 끝 +3.0(틀 기둥 옆 +1.2, 조금 앞 z −0.4), 발판 윗면 −2.5~−0.5.
        const float FrameTop = 6.5f, LadderTop = 3.0f, LadderOffsetX = 1.2f, LadderDz = -0.4f, WalkLow = -2.5f, WalkRange = 2f;
        //  랜턴: 유리 가운데 +2.8(Lantern 원점이 사슬 위 끝이고 유리는 원점 −1.85), 자리 흔들림 ±1 m.
        const float LanternGlass = 2.8f, LanternGlassBelowHang = 1.85f, LanternJitter = 1f;

        /// <summary>배경 밀도(<c>FlappyMineLook</c>의 같은 이름 값 — 그 클래스는 에디터 어셈블리라 여기서 못 본다).</summary>
        public readonly struct BackgroundDensity
        {
            /// <summary>배경 갱목 틀(BgFrame) 간격(m).</summary>
            public readonly float FrameSpacing;
            /// <summary>틀마다 사다리가 걸릴 확률(0~1).</summary>
            public readonly float LadderChance;
            /// <summary>랜턴 간격(m).</summary>
            public readonly float LanternSpacing;
            /// <summary>[from, to] 안 먼 비계 위 광차 수.</summary>
            public readonly int CartCount;
            /// <summary>먼 비계·광차 축척 — 칸 폭도 3 m × 이 값으로 줄어 멀리 보인다.</summary>
            public readonly float FarScale;
            /// <summary>가운데 층(틀·발판·사다리) 축척.</summary>
            public readonly float MidScale;

            /// <summary>먼 비계를 이만큼 칸 이어 놓고 <see cref="FarGap"/>칸 비운다(0 = 끊지 않고 이어 깐다).</summary>
            public readonly int FarRun;
            /// <summary>먼 비계 무리 사이 빈 칸 수.</summary>
            public readonly int FarGap;

            public BackgroundDensity(float frameSpacing, float ladderChance, float lanternSpacing, int cartCount, float farScale, float midScale)
                : this(frameSpacing, ladderChance, lanternSpacing, cartCount, farScale, midScale, 0, 0)
            {
            }

            public BackgroundDensity(float frameSpacing, float ladderChance, float lanternSpacing, int cartCount, float farScale, float midScale,
                                     int farRun, int farGap)
            {
                FrameSpacing = frameSpacing; LadderChance = ladderChance; LanternSpacing = lanternSpacing; CartCount = cartCount;
                FarScale = farScale; MidScale = midScale; FarRun = farRun; FarGap = farGap;
            }
        }

        // ── 관문 ──

        /// <summary>
        /// 파이프 하나 [<paramref name="bottom"/>, <paramref name="top"/>]를 채우는 기둥. <paramref name="capAtTop"/> = 틈이 위
        /// (아래 관문). 칸은 아래부터 1 m씩, 마지막 칸만 남은 높이(y 축척). 높이가 0 이하면 칸이 없다.
        /// </summary>
        public static GateStackLayout GateStack(float bottom, float top, bool capAtTop)
        {
            var s = new GateStackLayout { CapY = capAtTop ? top : bottom, CapFlipped = !capAtTop };
            double h = (double)top - bottom;
            if (h <= 1e-4) { return s; }

            //  1e-3 여유로 올림 — 딱 3 m가 float 오차로 3.0000002가 되어 0에 가까운 빈 칸이 생기지 않게.
            int n = Math.Max(1, (int)Math.Ceiling(h / PlankCell - 1e-3));
            for (int i = 0; i < n; i++)
            {
                double y0 = bottom + i * (double)PlankCell;
                double height = i < n - 1 ? PlankCell : h - (n - 1) * (double)PlankCell;
                s.Cells.Add(new GateCell((float)y0, (float)height));
            }

            //  쇠띠: 틈 쪽 끝(쇠테)에서 2 m마다 반대쪽으로 — 보이는 끝에서 고르게. 기둥 끝에 걸치는 것은 뺀다.
            double end = capAtTop ? top : bottom, dir = capAtTop ? -1 : 1;
            for (int k = 1; ; k++)
            {
                double y = end + dir * k * StrapSpacing;
                if (y - StrapHalf < bottom + 0.05 || y + StrapHalf > top - 0.05) { break; }
                s.StrapYs.Add((float)y);
            }
            return s;
        }

        /// <summary>
        /// 관문 가로 칸. 폭 <paramref name="width"/>를 1.95 이하의 같은 폭 칸 ceil(width / 1.95)개로 나눈다 — 긴 관문(폭 12)을 한 칸으로
        /// 6.15배 늘이면 나무결·볼트가 찌그러진다. 칸은 모두 같은 폭이라 가는 자투리 칸이 없다(7칸이면 축척 0.88).
        /// 양끝은 파이프 양끝과 정확히 같다.
        /// </summary>
        public static List<GatePlankColumn> GateColumns(float x, float width)
        {
            var list = new List<GatePlankColumn>();
            if (!(width > 1e-4f)) { return list; }
            //  1e-3 여유로 올림 — 3.9가 float 오차로 3.9000002가 되어 0에 가까운 자투리 칸이 생기지 않게.
            int n = Math.Max(1, (int)Math.Ceiling(width / (double)GateUnitWidth - 1e-3));
            double w = (double)width / n, left = x - width / 2.0;
            for (int i = 0; i < n; i++)
            {
                list.Add(new GatePlankColumn((float)(left + (i + 0.5) * w), (float)(w / GateUnitWidth)));
            }
            return list;
        }

        // ── 바닥·천장 ──

        /// <summary>
        /// 바닥·천장을 자를 간격(좁은·낮은 구간의 전이 곡선) — <c>MineCourse.Breaks</c>에 넘긴다. 회색 박스
        /// (<c>FlappyMineCourseBuilder</c>)도 이 값을 써서 옷 칸과 회색 박스 조각이 같은 x에서 꺾인다.
        /// </summary>
        public const float BreakStep = 0.5f;

        /// <summary>
        /// 바닥 비계(TrestleBay + 같은 자리 RailSpan). [from, to]를 꺾이는 곳에서 끊고(<see cref="BentTiles"/>) 곧은 구간마다
        /// 2.4 m에 가장 가까운 같은 폭으로 나눠 빈틈없이 깐다 — 칸 폭이 2.4에서 어긋나므로 x 축척 = <see cref="MinePiece.ScaleX"/>(현 길이 / 2.4). Y = 데크 윗면(원점).
        /// </summary>
        public static List<MinePiece> TrestleBays(MineCourse c, float from, float to)
        {
            var list = new List<MinePiece>();
            foreach (var (x0, x1) in BentTiles(c, from, to, TrestleBayWidth))
            {
                list.Add(Chord(MinePartKind.TrestleBay, x0, x1, x => c.CenterAtD(x) - c.HalfAtD(x), 0f, TrestleBayWidth));
            }
            return list;
        }

        /// <summary>
        /// 천장 조각. [from, to]를 꺾이는 곳에서 끊고(<see cref="BentTiles"/>) 곧은 구간마다 2 m 근처 같은 폭으로 나눈다. 가운데가 노을 바깥(x &lt; <see cref="MineZones.OutsideEnd"/>)이면 <see cref="MinePartKind.Beam"/>
        /// (1 m 단위), 아니면 <see cref="MinePartKind.CeilingRock"/>(2 m 단위). Y = 아랫면 = 천장선.
        /// </summary>
        public static List<MinePiece> CeilingPieces(MineCourse c, float from, float to)
        {
            var list = new List<MinePiece>();
            float outsideEnd = Zones(c).OutsideEnd;
            foreach (var (x0, x1) in BentTiles(c, from, to, CeilingPieceWidth))
            {
                bool beam = (x0 + x1) / 2.0 < outsideEnd;
                list.Add(Chord(beam ? MinePartKind.Beam : MinePartKind.CeilingRock, x0, x1, x => c.CenterAtD(x) + c.HalfAtD(x), 0f,
                               beam ? BeamUnit : CeilingRockUnit));
            }
            return list;
        }

        // ── 바닥·천장 전체(10-10 Task 7 Fix 1 — 굽기에서 옮겨 왔다) ──

        /// <summary>
        /// 범위 [<paramref name="from"/>, <paramref name="to"/>]의 바닥·천장 그림 전부(갱목 구간 <see cref="ShaftSpans"/>·바위 단 구간은 뺀다 —
        /// 거기는 갱목·단이 바닥·천장이다). z는 최종 값, <see cref="MinePiece.Layer"/> 0 = 바닥, 1 = 천장.
        /// 범위는 넓히지 않는다 — 범위에 걸친 회색 조각은 굽기가 범위 밖 부분만 남기고 자른다(<see cref="OutsideX"/>).
        /// </summary>
        public static List<MinePiece> FloorAndCeiling(MineCourse c, MineZones z, float from, float to)
        {
            var list = new List<MinePiece>();
            foreach (var (a, b) in PlainSpans(c, z, from, to))
            {
                AddLine(c, a, b, -1, list);
                //  천장: 노을 바깥·갱 입구 문틀 = 들보(높이 0.8 — 문틀 상인방·간판이 들보 위 판정 고체 쪽에 보이게), 굴 안 = 바위.
                if (a < z.EntranceEnd)
                {
                    foreach (var (x0, x1) in BentTiles(c, (float)a, (float)Math.Min(b, z.EntranceEnd), CeilingPieceWidth))
                    {
                        list.Add(At(Chord(MinePartKind.Beam, x0, x1, x => c.CenterAtD(x) + c.HalfAtD(x), 0f, BeamUnit), BeamZ, 1));
                    }
                }
                if (b > z.EntranceEnd) { AddLine(c, Math.Max(a, z.EntranceEnd), b, 1, list); }
            }
            return list;
        }

        //  바닥(side −1)·굴 안 천장(+1) 한 구간: 판정선을 곧은 구간(run)으로 끊는다. 바닥은 완만하고(≤ 15°) 긴(≥ 1.2 m) run만 비계,
        //  나머지(굽은 물결·전이·가파른 칸)와 굴 안 천장 전부는 다리 없는 바위 띠 + 얇은 밝은 테(사용자 10-10: 천장 바위 부품의
        //  울퉁불퉁한 구멍 무늬와 두꺼운 테가 시끄럽다 — 천장은 차분한 바위 덩어리로). 그림 경계가 판정 현 그대로다.
        static void AddLine(MineCourse c, double a, double b, int side, List<MinePiece> list)
        {
            int layer = side < 0 ? 0 : 1;
            var band = new List<(double x0, double x1, double e0, double e1)>();
            foreach (var r in LineRuns(c, a, b, side))
            {
                if (side > 0 || !Straight(r))
                {
                    band.Add(r);
                    continue;
                }
                AddBand(band, side, layer, list);
                band.Clear();
                Func<double, double> line = x => c.CenterAtD(x) + side * c.HalfAtD(x);
                foreach (var (x0, x1) in Tiles((float)r.x0, (float)r.x1, TrestleBayWidth)) { list.Add(At(Chord(MinePartKind.TrestleBay, x0, x1, line, 0f, TrestleBayWidth), FloorZ, layer)); }
            }
            AddBand(band, side, layer, list);
        }

        static bool Straight((double x0, double x1, double e0, double e1) r)
        {
            double deg = Math.Abs(Math.Atan2(r.e1 - r.e0, r.x1 - r.x0)) * 180.0 / Math.PI;
            return deg <= TrestleMaxSlopeDegrees + 1e-6 && r.x1 - r.x0 >= StraightMinRun - 1e-6;
        }

        //  [a, b]의 판정선(바닥 −1·천장 +1)을 기울기가 같은 곳끼리 이은 곧은 구간 — 양끝은 판정선 위(회색 박스 현과 같은 x).
        static List<(double x0, double x1, double e0, double e1)> LineRuns(MineCourse c, double a, double b, int side)
        {
            var runs = new List<(double x0, double x1, double e0, double e1)>();
            var xs = StraightBreaks(c, (float)a, (float)b);
            double E(double x) => c.CenterAtD(x) + side * c.HalfAtD(x);
            for (int i = 1; i < xs.Count; i++)
            {
                double x0 = xs[i - 1], x1 = xs[i], e0 = E(x0), e1 = E(x1);
                if (runs.Count > 0)
                {
                    var r = runs[runs.Count - 1];
                    if (Math.Abs((r.e1 - r.e0) / (r.x1 - r.x0) - (e1 - e0) / (x1 - x0)) < 1e-4)
                    {
                        runs[runs.Count - 1] = (r.x0, x1, r.e0, e1);
                        continue;
                    }
                }
                runs.Add((x0, x1, e0, e1));
            }
            return runs;
        }

        //  다리 없는 바위 띠 + 얇은 밝은 테. 바위는 AddRockBeyondLine(바깥 끝이 곧은 덩어리), 테는 현마다.
        static void AddBand(List<(double x0, double x1, double e0, double e1)> band, int side, int layer, List<MinePiece> list)
        {
            if (band.Count == 0) { return; }
            var tmp = new List<MinePiece>();
            AddRockBeyondLine(band, side, LineRockDepth, RockZ, MergeSlopeZ, tmp);
            for (int i = 0; i < band.Count; i++)
            {
                var r = band[i];
                double maxT = Math.Min(i > 0 ? ConvexThickness(band[i - 1], r, side) : RimThickness,
                                       i + 1 < band.Count ? ConvexThickness(r, band[i + 1], side) : RimThickness);
                //  바위 쪽으로 오목하게 꺾이는 곳(물결 골)은 두 테 끝이 부채꼴로 벌어져 홈이 난다 — 테를 현 방향으로 두께·tan(꺾임/2)만큼
                //  늘여 겹친다. 늘인 부분은 이웃 현의 바위 쪽이라 통로로 나가지 않는다.
                double ext0 = i > 0 ? ConcaveOverlap(band[i - 1], r, side) : 0, ext1 = i + 1 < band.Count ? ConcaveOverlap(r, band[i + 1], side) : 0;
                double len = Math.Sqrt((r.x1 - r.x0) * (r.x1 - r.x0) + (r.e1 - r.e0) * (r.e1 - r.e0));
                double ux = (r.x1 - r.x0) / len, uy = (r.e1 - r.e0) / len;
                double ax = r.x0 - ux * ext0, ay = r.e0 - uy * ext0, bx = r.x1 + ux * ext1, by = r.e1 + uy * ext1;
                //  바위가 진행 방향 오른쪽: 바닥 바위(아래)는 왼→오, 천장 바위(위)는 오→왼.
                if (side < 0) { AddRim(ax, ay, bx, by, double.NegativeInfinity, double.PositiveInfinity, maxT, tmp); }
                else { AddRim(bx, by, ax, ay, double.NegativeInfinity, double.PositiveInfinity, maxT, tmp); }
            }
            foreach (MinePiece p in tmp) { list.Add(At(p, p.Z, layer)); }
        }

        /// <summary>
        /// 꺾은선(현 runs, x 증가) 바깥(side −1 아래, +1 위) 판정 고체를 바위로 채운다: 현마다 그 x 폭의 세로 기둥(RockMass)을 가장 바깥 쪽 꼭짓점
        /// 너머 <paramref name="depth"/>까지 + 기둥 끝과 현 사이 삼각형(MergeSlope, 굽기가 무늬 없는 바위 재질로 덮는다). 그래서 안쪽 경계는 현 그대로,
        /// 바깥 끝은 한 높이로 곧다(현마다 같은 두께로 깔면 바깥 끝이 계단이 되고, 현을 따라 기울이면 가파른 현에서 옆 통로로 새어 나왔다 — 캡처 2차·테스트).
        /// </summary>
        static void AddRockBeyondLine(List<(double x0, double x1, double e0, double e1)> runs, int side, double depth, float rockZ, float wedgeZ, List<MinePiece> list)
        {
            double extreme = side < 0 ? runs.Min(r => Math.Min(r.e0, r.e1)) : runs.Max(r => Math.Max(r.e0, r.e1));
            double outer = extreme + side * depth;
            foreach (var r in runs)
            {
                double near = side < 0 ? Math.Min(r.e0, r.e1) : Math.Max(r.e0, r.e1);
                AddRock(r.x0, r.x1, outer, near, list, rockZ);
                AddEdgeWedge(r.x0, r.e0, r.x1, r.e1, side, list, wedgeZ);
            }
        }

        /// <summary>바닥·천장 그림이 맡는 구간 = [from, to]에서 갱목 구간·바위 단(바닥 쪽 단의 [X, X + Width]) 구간을 뺀 나머지.</summary>
        public static List<(double from, double to)> PlainSpans(MineCourse c, MineZones z, float from, float to)
        {
            var cuts = new List<(double from, double to)>();
            foreach (var (a, b) in ShaftSpans(c, z)) { cuts.Add((a, b)); }
            foreach (MinePiece p in StairBenches(c, z))
            {
                if (p.Kind == MinePartKind.BenchFace && !p.Flipped) { cuts.Add((p.X, p.X + p.Width)); }
            }
            cuts.Sort((p, q) => p.from.CompareTo(q.from));
            var result = new List<(double from, double to)>();
            double cursor = from;
            foreach (var (a, b) in cuts)
            {
                if (b <= cursor) { continue; }
                if (a >= to) { break; }
                if (a - cursor > 1e-4) { result.Add((cursor, a)); }
                cursor = Math.Max(cursor, b);
            }
            if (to - cursor > 1e-4) { result.Add((cursor, to)); }
            return result;
        }

        static MinePiece At(MinePiece p, float z, int layer) =>
            new MinePiece(p.Kind, p.X, p.Y, z, p.AngleDegrees, p.Width, p.Length, p.ScaleX, p.ScaleY, p.Flipped, layer);

        /// <summary>
        /// 볼록 다각형에서 x &lt; <paramref name="from"/> 부분과 x &gt; <paramref name="to"/> 부분(폭 1e-4 넘는 것만).
        /// 범위에 걸친 회색 바닥·천장 조각은 이 부분만 그린다 — 범위 안은 옷이 그린다.
        /// </summary>
        public static List<(double x, double y)[]> OutsideX(IReadOnlyList<(double x, double y)> poly, double from, double to)
        {
            var parts = new List<(double x, double y)[]>();
            var pts = poly.ToList();
            foreach (var part in new[] { ClipPoly(pts, p => from - p.x, Lerp), ClipPoly(pts, p => p.x - to, Lerp) })
            {
                if (part.Count >= 3 && part.Max(p => p.x) - part.Min(p => p.x) > 1e-4) { parts.Add(part.ToArray()); }
            }
            return parts;
        }

        static (double x, double y) Lerp((double x, double y) a, (double x, double y) b, double t) => (a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        //  볼록 다각형을 f(p) ≥ 0 쪽만 남긴다(Sutherland–Hodgman). 꼭짓점 사이는 lerp로 잇는다.
        static List<T> ClipPoly<T>(List<T> poly, Func<T, double> f, Func<T, T, double, T> lerp)
        {
            var result = new List<T>();
            for (int i = 0; i < poly.Count; i++)
            {
                T cur = poly[i], next = poly[(i + 1) % poly.Count];
                double fc = f(cur), fn = f(next);
                if (fc >= 0) { result.Add(cur); }
                if ((fc >= 0) != (fn >= 0)) { result.Add(lerp(cur, next, fc / (fc - fn))); }
            }
            return result;
        }

        /// <summary>
        /// 판정면 뒤 장식 중 통로를 가로지르는 모양(갱 입구 문틀의 기둥·가새)이라 굽기가 열린 통로 안 부분을 잘라 내는 부품.
        /// </summary>
        public static bool ClipsToCorridor(MinePartKind kind) => kind == MinePartKind.EntranceFrame;

        /// <summary>
        /// 삼각형 하나에서 열린 통로(바닥선과 천장선 사이) 밖 부분만 남긴다 — 결과는 다각형들, 꼭짓점은 원래 세 꼭짓점의 무게(합 1)라
        /// 위치·법선·UV를 같은 무게로 섞으면 된다. 바닥·천장선은 회색 박스와 같이 <c>Breaks</c> 사이 현이다.
        /// </summary>
        public static List<List<(double w0, double w1, double w2)>> ClipOutsideCorridor(MineCourse c, (double x, double y) p0, (double x, double y) p1, (double x, double y) p2)
        {
            var result = new List<List<(double w0, double w1, double w2)>>();
            (double x, double y) Pos((double w0, double w1, double w2) w) =>
                (w.w0 * p0.x + w.w1 * p1.x + w.w2 * p2.x, w.w0 * p0.y + w.w1 * p1.y + w.w2 * p2.y);
            (double, double, double) WLerp((double w0, double w1, double w2) a, (double w0, double w1, double w2) b, double t) =>
                (a.w0 + (b.w0 - a.w0) * t, a.w1 + (b.w1 - a.w1) * t, a.w2 + (b.w2 - a.w2) * t);

            //  x를 꺾이는 곳(Breaks)에서 끊은 칸마다 바닥·천장선이 곧다 — 그 칸 안에서 천장선 위·바닥선 아래만 남긴다.
            double xmin = Math.Min(p0.x, Math.Min(p1.x, p2.x)), xmax = Math.Max(p0.x, Math.Max(p1.x, p2.x));
            var xs = new List<double> { xmin };
            foreach (float bx in c.Breaks(BreakStep)) { if (bx > xmin + 1e-9 && bx < xmax - 1e-9) { xs.Add(bx); } }
            xs.Add(xmax);
            var tri = new List<(double w0, double w1, double w2)> { (1, 0, 0), (0, 1, 0), (0, 0, 1) };
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                double xa = xs[i], xb = xs[i + 1];
                var poly = tri;
                bool flat = xb - xa <= 1e-9;   // 화면에 수직인 면(옆면) — x 한 점
                if (!flat)
                {
                    poly = ClipPoly(poly, w => Pos(w).x - xa, WLerp);
                    poly = ClipPoly(poly, w => xb - Pos(w).x, WLerp);
                }
                if (poly.Count < 3) { continue; }
                double fa = c.CenterAtD(xa) - c.HalfAtD(xa), fb = c.CenterAtD(xb) - c.HalfAtD(xb);
                double ca = c.CenterAtD(xa) + c.HalfAtD(xa), cb = c.CenterAtD(xb) + c.HalfAtD(xb);
                double Line(double ya, double yb, double x) => flat ? ya : ya + (yb - ya) * (x - xa) / (xb - xa);
                var above = ClipPoly(poly, w => { var p = Pos(w); return p.y - Line(ca, cb, p.x); }, WLerp);
                var below = ClipPoly(poly, w => { var p = Pos(w); return Line(fa, fb, p.x) - p.y; }, WLerp);
                if (above.Count >= 3) { result.Add(above); }
                if (below.Count >= 3) { result.Add(below); }
            }
            return result;
        }

        // ── 구역(10-10 펼치기 1차) ──

        /// <summary>
        /// 구역 x를 코스에서 찾는다: 노을 바깥 끝 = <see cref="MineCourse.FlatEnd"/>, 갱 입구 문틀 끝 = 첫 "수직 갱 낙하",
        /// 옷 끝 = 첫 "레일 롤러코스터"(3막 19.8 · 24.8 · 151.25). 구간이 없으면 문틀 끝 = 바깥 끝, 옷 끝 = 코스 길이.
        /// </summary>
        public static MineZones Zones(MineCourse c)
        {
            float outside = c.FlatEnd();
            float entrance = FirstSection(c, ShaftSection) ?? outside;
            float dressEnd = FirstSection(c, CoasterSection) ?? c.Length;
            return new MineZones(outside, Math.Max(outside, entrance), dressEnd);
        }

        static float? FirstSection(MineCourse c, string name)
        {
            foreach (MineSection s in c.Sections)
            {
                if (s.Name == name) { return s.X; }
            }
            return null;
        }

        // ── 갱 입구 문틀 ──

        /// <summary>
        /// 갱 입구 문틀(스펙 §5): 문틀은 오른끝이 <see cref="MineZones.EntranceEnd"/>에 닿게(3막은 폭 5가 문틀 구간 19.8~24.8을 꼭 채운다),
        /// 문턱 = 그 자리 통로 중심 − 7.28(문틀 안 통로 14.56이 넓은 통로와 겹친다). 간판은 상인방 앞(통로 위). 둘 다 판정면 뒤 장식이다.
        /// 문틀 기둥·가새는 열린 통로를 가로지르므로 굽기가 통로 안 부분을 잘라 낸다(<see cref="ClipsToCorridor"/>) — 상인방·문턱·기둥 끝만 남는다.
        /// </summary>
        public static List<MinePiece> Entrance(MineCourse c, MineZones z)
        {
            var list = new List<MinePiece>();
            float fx = z.EntranceEnd - EntranceHalfWidth;
            float threshold = c.CenterAt(fx) - MineCourse.BaseHalf;
            float w = EntranceHalfWidth * 2f;
            list.Add(new MinePiece(MinePartKind.EntranceFrame, fx, threshold, EntranceFrameZ, 0f, w, w, 1f, 1f));
            list.Add(new MinePiece(MinePartKind.EntranceSign, fx, threshold + EntranceSignCenter, EntranceSignZ, 0f, 3f, 3f, 1f, 1f));
            return list;
        }

        // ── 수직 갱 ──

        /// <summary>
        /// 수직 갱 갱도(스펙 §5): 좁은 구간(<c>Narrows</c>) [a − 3, b + 3]마다(앞은 문틀 끝, 뒤는 옷 끝에서 자른다) 바닥·천장 쪽 벽에
        /// <list type="bullet">
        /// <item><see cref="MinePartKind.ShaftTimber"/> <see cref="ShaftTimberRows"/>줄 — 판정선이 꺾이는 곳(<see cref="StraightBreaks"/>)에서 끊은
        /// 꺾은선을 바깥(바닥은 아래, 천장은 위)으로 <paramref name="timberSpacing"/>씩 평행 이동(꺾이는 곳은 맞댐)한 줄마다 1.5 m 이하 조각,
        /// 둘째 줄은 반 칸 어긋나게. 첫 줄의 통로 쪽 변이 판정선이고 나머지는 전부 판정선 바깥이다.</item>
        /// <item>그 뒤 바위(<see cref="MinePartKind.RockMass"/>·삼각형, 판정면 뒤): 판정선에서 <see cref="ShaftRockInset"/> 바깥 선 너머 전부(바깥 끝은 곧다).</item>
        /// </list>
        /// 통로 안에는 아무것도 없다(케이지는 뺐다).
        /// </summary>
        public static List<MinePiece> ShaftWalls(MineCourse c, MineZones z, float timberSpacing)
        {
            var list = new List<MinePiece>();
            if (!(timberSpacing > 0f)) { throw new ArgumentOutOfRangeException(nameof(timberSpacing)); }
            foreach (var (a, b, _) in c.Narrows)
            {
                var (from, to) = ShaftSpan(a, b, z);
                if (to - from <= 1e-6) { continue; }
                var xs = StraightBreaks(c, (float)from, (float)to);
                foreach (int side in new[] { -1, 1 })
                {
                    var verts = MergeStraight(xs.Select(x => (x, c.CenterAtD(x) + side * c.HalfAtD(x))).ToList());
                    for (int k = 0; k < ShaftTimberRows; k++) { AddTimberLayer(verts, side, k * (double)timberSpacing, from, to, k % 2 == 1, list); }
                    AddShaftRock(verts, side, from, to, list);
                }
            }
            return list;
        }

        /// <summary>
        /// <see cref="ShaftWalls"/>가 갱목으로 덮는 x 구간들(좁은 구간마다 [a − 3, b + 3]을 문틀 끝·옷 끝으로 자른 것, 빈 구간은 뺀다) —
        /// 3막은 [24.8, 39.8] 하나. 굽기가 이 구간에서는 바닥 비계·천장 조각을 놓지 않는다(갱목이 바닥·천장을 맡는다).
        /// </summary>
        public static List<(float from, float to)> ShaftSpans(MineCourse c, MineZones z)
        {
            var list = new List<(float from, float to)>();
            foreach (var (a, b, _) in c.Narrows)
            {
                var (from, to) = ShaftSpan(a, b, z);
                if (to - from > 1e-6) { list.Add(((float)from, (float)to)); }
            }
            return list;
        }

        static (double from, double to) ShaftSpan(double a, double b, MineZones z)
        {
            return (Math.Max(a - MineCourse.EdgeBlend, z.EntranceEnd), Math.Min(b + MineCourse.EdgeBlend, z.DressEnd));
        }

        //  꺾은선 verts를 바깥으로 d만큼 평행 이동한 점들(꺾이는 곳은 두 선이 만나는 점으로 맞댐)과 마디마다 바깥 법선.
        //  side −1 = 바닥 쪽(바깥 = 진행 방향 오른쪽 = 아래), +1 = 천장 쪽(왼쪽 = 위).
        static ((double x, double y)[] off, (double x, double y)[] normals) Offset(List<(double x, double y)> verts, int side, double d)
        {
            int m = verts.Count - 1;
            var normals = new (double x, double y)[m];
            for (int j = 0; j < m; j++)
            {
                double dx = verts[j + 1].x - verts[j].x, dy = verts[j + 1].y - verts[j].y, len = Math.Sqrt(dx * dx + dy * dy);
                //  바깥 법선: 천장 쪽은 왼쪽 (−dy, dx), 바닥 쪽은 오른쪽 (dy, −dx).
                normals[j] = side > 0 ? (-dy / len, dx / len) : (dy / len, -dx / len);
            }
            var off = new (double x, double y)[m + 1];
            for (int j = 0; j <= m; j++)
            {
                (double x, double y) n;
                if (j == 0) { n = normals[0]; }
                else if (j == m) { n = normals[m - 1]; }
                else
                {
                    var (ax, ay) = normals[j - 1];
                    var (bx, by) = normals[j];
                    double k = 1.0 + ax * bx + ay * by;
                    n = ((ax + bx) / k, (ay + by) / k);
                }
                off[j] = (verts[j].x + n.x * d, verts[j].y + n.y * d);
            }
            return (off, normals);
        }

        //  평행 이동한 줄 하나에 갱목을 깐다 — 줄은 [x0, x1] 안으로 자른다(가파른 갱에서 바깥 줄은 법선 방향으로 옆으로 밀려 범위 밖으로 나간다).
        //  stagger면 이음매를 반 칸 어긋나게(양끝 반 조각) — 줄마다 이음매가 같은 자리에 오면 판자 벽이 격자로 읽힌다.
        static void AddTimberLayer(List<(double x, double y)> verts, int side, double d, double x0, double x1, bool stagger, List<MinePiece> list)
        {
            var (off, normals) = Offset(verts, side, d);
            int m = verts.Count - 1;
            for (int j = 0; j < m; j++)
            {
                var (ax, ay) = off[j];
                var (bx, by) = off[j + 1];
                double dx = bx - ax, dy = by - ay;
                //  안쪽으로 꺾이는 곳에서 깊은 줄은 마디가 뒤집힐 수 있다 — 그 마디는 건너뛴다(이웃 마디가 덮는다).
                if (dx * (verts[j + 1].x - verts[j].x) + dy * (verts[j + 1].y - verts[j].y) <= 1e-9) { continue; }
                //  [x0, x1]로 자른다.
                double t0 = 0, t1 = 1;
                if (Math.Abs(dx) > 1e-9)
                {
                    double ta = (x0 - ax) / dx, tb = (x1 - ax) / dx;
                    t0 = Math.Max(t0, Math.Min(ta, tb));
                    t1 = Math.Min(t1, Math.Max(ta, tb));
                }
                else if (ax < x0 || ax > x1) { continue; }
                if (t1 - t0 <= 1e-6) { continue; }
                double sx = ax + dx * t0, sy = ay + dy * t0;
                dx *= t1 - t0; dy *= t1 - t0;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.05) { continue; }
                int pieces = Math.Max(1, (int)Math.Ceiling(len / ShaftTimberUnit - 1e-6));
                var cuts = new List<double> { 0 };
                for (int i = 1; i < pieces; i++) { cuts.Add(i / (double)pieces); }
                if (stagger) { cuts = new List<double> { 0 }; for (int i = 0; i < pieces; i++) { cuts.Add((i + 0.5) / pieces); } }
                cuts.Add(1);
                double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                var (nx, ny) = normals[j];
                for (int i = 0; i + 1 < cuts.Count; i++)
                {
                    double l = len * (cuts[i + 1] - cuts[i]);
                    if (l < 0.05) { continue; }
                    double px = sx + dx * cuts[i], py = sy + dy * cuts[i];
                    //  원점 = 왼끝 아래. 천장 쪽은 통로 쪽 변이 아래 변이라 그대로, 바닥 쪽은 위 변이 통로 쪽이라 바깥으로 0.4 내린다.
                    if (side < 0) { px += nx * ShaftTimberHeight; py += ny * ShaftTimberHeight; }
                    list.Add(new MinePiece(MinePartKind.ShaftTimber, (float)px, (float)py, ShaftTimberZ, (float)ang,
                                           (float)Math.Abs(dx * (cuts[i + 1] - cuts[i])), (float)l, (float)l, 1f));
                }
            }
        }

        //  갱목 뒤 바위: 판정선을 바깥으로 ShaftRockInset 평행 이동한 꺾은선을 [from, to]로 자르고(양끝 마디는 이어 늘인다) 그 바깥에 바위 띠.
        //  띠 안쪽 변은 갱목 둘째 줄 밑에 숨는다. 판정면 뒤(앞면 0.02)라 갱목을 가리지 않는다.
        static void AddShaftRock(List<(double x, double y)> verts, int side, double from, double to, List<MinePiece> list)
        {
            var (off, _) = Offset(verts, side, ShaftRockInset);
            var pts = off.Where((p, i) => i == 0 || p.x > off[i - 1].x + 1e-9).ToList();
            double Y(double x)
            {
                int j = 1;
                while (j < pts.Count - 1 && x > pts[j].x) { j++; }
                var (ax, ay) = pts[j - 1];
                var (bx, by) = pts[j];
                return ay + (by - ay) * (x - ax) / (bx - ax);
            }
            var xs = new List<double> { from };
            foreach (var p in pts) { if (p.x > from + 1e-6 && p.x < to - 1e-6) { xs.Add(p.x); } }
            xs.Add(to);
            var runs = new List<(double x0, double x1, double e0, double e1)>();
            for (int i = 1; i < xs.Count; i++) { runs.Add((xs[i - 1], xs[i], Y(xs[i - 1]), Y(xs[i]))); }
            //  삼각형(MergeSlope, 앞면 = Z − 0.135)도 기둥(Z − 0.13)과 같은 앞면 0.02(둘 다 깊이 축척 0.1).
            AddRockBeyondLine(runs, side, ShaftWallDepth - ShaftRockInset, ShaftRockZ, ShaftRockZ + 0.005f, list);
        }

        //  꺾은선에서 곧게 이어지는(방향 차 &lt; 1e-4 rad) 가운데 점을 뺀다 — Breaks의 float x 때문에 곧은 갱도 0.5 m마다 끊겨 있다.
        static List<(double x, double y)> MergeStraight(List<(double x, double y)> v)
        {
            var r = new List<(double x, double y)> { v[0] };
            for (int i = 1; i < v.Count - 1; i++)
            {
                var a = r[r.Count - 1];
                double ax = v[i].x - a.x, ay = v[i].y - a.y, bx = v[i + 1].x - v[i].x, by = v[i + 1].y - v[i].y;
                double cross = (ax * by - ay * bx) / (Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by));
                if (Math.Abs(cross) < 1e-4 && ax * bx + ay * by > 0) { continue; }
                r.Add(v[i]);
            }
            r.Add(v[v.Count - 1]);
            return r;
        }

        // ── 계단 내리막 ──

        /// <summary>
        /// 계단 내리막(스펙 §5): "계단 내리막" 구간(옷 끝 앞)의 계단 칸 = 통로 중심 꺾은선 마디 중 구간 안에서 끝나고 높이가 바뀌는 것
        /// (3막 41.225 ~ 73.625, 5.4 m씩 6칸). 칸마다
        /// <list type="bullet">
        /// <item>바닥 쪽 <see cref="MinePartKind.BenchFace"/>: 원점 = 칸 왼끝 바닥선, 현 각도·길이 — 윗면(밝은 띠)이 바닥선 그대로, 몸통은 아래로 4.5.</item>
        /// <item>천장 쪽 BenchFace: Z축 180°(<see cref="MinePiece.Flipped"/>) — 원점 = 칸 오른끝 천장선, 몸통은 위로.</item>
        /// </list>
        /// </summary>
        public static List<MinePiece> StairBenches(MineCourse c, MineZones z)
        {
            var list = new List<MinePiece>();
            for (int si = 0; si < c.Sections.Count; si++)
            {
                MineSection sec = c.Sections[si];
                if (sec.Name != StairSection || sec.X >= z.DressEnd) { continue; }
                double secTo = Math.Min(si + 1 < c.Sections.Count ? c.Sections[si + 1].X : c.Length, z.DressEnd);
                for (int i = 0; i + 1 < c.Points.Count; i++)
                {
                    var (x0, y0) = c.Points[i];
                    var (x1, y1) = c.Points[i + 1];
                    if (x1 <= sec.X || x1 > secTo || x1 - x0 <= 1e-6 || Math.Abs(y1 - y0) < 1e-9) { continue; }
                    AddBench(c, x0, x1, list);
                }
            }
            return list;
        }

        static void AddBench(MineCourse c, double x0, double x1, List<MinePiece> list)
        {
            double w = x1 - x0;
            double fa = c.CenterAtD(x0) - c.HalfAtD(x0), fb = c.CenterAtD(x1) - c.HalfAtD(x1);
            double fl = Math.Sqrt(w * w + (fb - fa) * (fb - fa));
            list.Add(new MinePiece(MinePartKind.BenchFace, (float)x0, (float)fa, BenchZ, (float)(Math.Atan2(fb - fa, w) * 180.0 / Math.PI),
                                   (float)w, (float)fl, (float)fl, 1f));
            double ca = c.CenterAtD(x0) + c.HalfAtD(x0), cb = c.CenterAtD(x1) + c.HalfAtD(x1);
            double cl = Math.Sqrt(w * w + (cb - ca) * (cb - ca));
            //  뒤집으면(180°) 국소 +x가 왼쪽을 향한다 — 원점을 오른끝에 두면 왼끝까지 닿는다. 각도는 현 각도 그대로(뒤집기는 굽기가 더한다).
            list.Add(new MinePiece(MinePartKind.BenchFace, (float)x1, (float)cb, BenchZ, (float)(Math.Atan2(cb - ca, w) * 180.0 / Math.PI),
                                   (float)w, (float)cl, (float)cl, 1f, flipped: true));

        }

        // ── 고수 갈림길 ──

        /// <summary>
        /// 고수 갈림길 하나(스펙 §3·§5). 판정 기하를 그대로 그린다:
        /// <list type="bullet">
        /// <item><see cref="MinePartKind.DividerWall"/>: 칸막이 <see cref="MineRect"/> 그대로(원점 왼끝 가운데, x 축척 = 폭, y 축척 = 높이 / 1.2).</item>
        /// <item>굴 칸 바위(<see cref="MinePartKind.RockMass"/>): 굴(틈 3.1)·패드 방(틈 4.6) 틈 밖 ~ 칸 끝(칸 끝이 통로 밖이면 통로 바닥·천장선까지).
        /// 굴마다 큰 사각형 하나 + 휘는 가장자리는 회색 박스와 같은 0.25 m 조각(<see cref="TubeStripStep"/>)마다 기둥, 기둥 끝과 현 사이 삼각형은
        /// <see cref="MinePartKind.MergeSlope"/>(빗변 = 현). 그래서 그림 경계가 회색 박스 현 그대로다.</item>
        /// <item>얇은 밝은 테(<see cref="MinePartKind.EdgeRim"/>): 바위의 닿는 가장자리 전부 — 굴·방 틈 가장자리 현(같은 기울기는
        /// 이어 붙여 1.95 m 이하 칸으로), 굴 시작·굴→방·방 끝의 세로 면. 두께 0.4가 바위 쪽, 바위 앞면보다 앞.</item>
        /// <item><see cref="MinePartKind.BoltSign"/>: 칸막이 앞머리 앞면(판정 고체 위, 판정면 앞). <see cref="MinePartKind.BoreFrame"/>은 메시를 고칠 때까지 놓지 않는다(상수 옆 주석).</item>
        /// <item><see cref="MinePartKind.PadGlow"/>: 패드 가운데 x, 윗면 = 방 바닥(굴이 위 칸이면 칸막이 윗면).</item>
        /// <item>합류 경사 <see cref="MinePartKind.MergeSlope"/>: 방 끝 칸막이 쪽 바위 <b>안</b> — 방 끝 2.4 m를 비스듬히 덮는다(남는 삼각형도 RockMass가 이미 덮어
        /// 그림 경계는 판정 사각형 그대로다). 칸막이 쪽 바위가 없으면(위 칸 방은 칸막이가 곧 바닥) 놓지 않는다.</item>
        /// </list>
        /// </summary>
        public static List<MinePiece> ForkPieces(MineCourse c, MineFork f)
        {
            var list = new List<MinePiece>();
            foreach (MineRect w in c.Walls)
            {
                if (Math.Abs(w.X0 - f.X0) > 1e-3f || Math.Abs(w.X1 - f.X1) > 1e-3f) { continue; }
                float width = w.X1 - w.X0;
                list.Add(new MinePiece(MinePartKind.DividerWall, w.X0, (w.Y0 + w.Y1) / 2f, DividerZ, 0f, width, width, width, (w.Y1 - w.Y0) / DividerHeight));
            }

            var tubes = new List<MineTube>();
            foreach (MineTube t in c.Tubes)
            {
                if (t.X0 >= f.X0 - 1e-3f && t.X1 <= f.X1 + 1e-3f) { tubes.Add(t); }
            }
            tubes.Sort((p, q) => p.X0.CompareTo(q.X0));
            if (tubes.Count == 0) { return list; }
            MineRect lane = f.TunnelLane;

            foreach (int side in new[] { -1, 1 }) { AddTubeRock(c, tubes, lane, side, list); }

            foreach (MineRect w in c.Walls)
            {
                if (Math.Abs(w.X0 - f.X0) > 1e-3f || Math.Abs(w.X1 - f.X1) > 1e-3f) { continue; }
                float s = Math.Min(1f, (w.Y1 - w.Y0 - 2f * BoltSignInset) / (2f * BoltSignHalfHeight));
                list.Add(new MinePiece(MinePartKind.BoltSign, w.X0 + BoltSignNose + BoltSignHalfWidth * s, (w.Y0 + w.Y1) / 2f, BoltSignZ, 0f,
                                       BoltSignHalfWidth * 2f * s, BoltSignHalfWidth * 2f * s, s, s));
            }

            foreach (MinePad pad in c.Pads)
            {
                float px = (pad.Rect.X0 + pad.Rect.X1) / 2f;
                if (px < f.X0 || px > f.X1) { continue; }
                MineTube room = tubes.Find(t => px >= t.X0 && px <= t.X1);
                if (room == null) { continue; }
                float floor = (float)Clamp(room.Center(px) - room.Gap * 0.5f, -1, Outer(c, room, lane, -1));
                list.Add(new MinePiece(MinePartKind.PadGlow, px, floor - PadGlowHalfHeight, PadGlowZ, 0f, 1.2f, 1.2f, 1f, 1f));
            }

            //  합류 경사: 마지막 굴(방) 끝, 칸막이 쪽 바위 안.
            MineTube last = tubes[tubes.Count - 1];
            int divSide = f.TunnelUp ? -1 : 1;
            double divEdge = divSide > 0 ? lane.Y1 : lane.Y0;
            double roomEdge = Clamp(last.Center(last.X1) + divSide * last.Gap * 0.5f, divSide, divEdge);
            double h = Math.Abs(divEdge - roomEdge), mw = Math.Min(MergeSlopeWidth, last.X1 - last.X0);
            if (h > 0.05)
            {
                list.Add(new MinePiece(MinePartKind.MergeSlope, (float)(last.X1 - mw), (float)roomEdge, MergeSlopeZ, 0f, (float)mw, (float)mw,
                                       (float)mw, (float)(divSide * h)));
            }
            return list;
        }

        //  굴 칸 한쪽(side −1 = 틈 아래 바위, +1 = 틈 위 바위)의 바위와 밝은 테.
        static void AddTubeRock(MineCourse c, List<MineTube> tubes, MineRect lane, int side, List<MinePiece> list)
        {
            double prevEnd = double.NaN;
            for (int ti = 0; ti < tubes.Count; ti++)
            {
                MineTube t = tubes[ti];
                double outer = Outer(c, t, lane, side);
                double Eff(double e) => Clamp(e, side, outer);

                //  가장자리 현: 회색 박스와 같은 조각, 같은 기울기는 이어 붙인다.
                var runs = new List<(double x0, double x1, double e0, double e1)>();
                foreach (var (x0, x1) in TubeSlices(t.X0, t.X1))
                {
                    double e0 = Eff(t.Center(x0) + side * t.Gap * 0.5f), e1 = Eff(t.Center(x1) + side * t.Gap * 0.5f);
                    if (runs.Count > 0)
                    {
                        var r = runs[runs.Count - 1];
                        if (Math.Abs((r.e1 - r.e0) / (r.x1 - r.x0) - (e1 - e0) / (x1 - x0)) < 1e-4 && Math.Abs(r.e1 - e0) < 1e-5)
                        {
                            runs[runs.Count - 1] = (r.x0, x1, r.e0, e1);
                            continue;
                        }
                    }
                    runs.Add((x0, x1, e0, e1));
                }

                //  바깥 쪽 끝(칸 끝)에 가장 가까운 가장자리 높이 — 그 아래(위)는 굴 전체에 큰 사각형 하나.
                double extreme = side < 0 ? runs.Min(r => Math.Min(r.e0, r.e1)) : runs.Max(r => Math.Max(r.e0, r.e1));
                AddRock(t.X0, t.X1, outer, extreme, list);
                for (int ri = 0; ri < runs.Count; ri++)
                {
                    var r = runs[ri];
                    double near = side < 0 ? Math.Min(r.e0, r.e1) : Math.Max(r.e0, r.e1);
                    AddRock(r.x0, r.x1, extreme, near, list);
                    AddEdgeWedge(r.x0, r.e0, r.x1, r.e1, side, list);
                    if (Math.Abs(r.e0 - outer) < 1e-5 && Math.Abs(r.e1 - outer) < 1e-5) { continue; }   // 바위 없음(칸막이·칸 끝이 곧 가장자리)

                    //  바위가 90° 넘게 볼록하게 꺾이는 곳(두 번째 날갯짓의 V 바닥)에서는 직사각 테가 이웃 현 너머 틈으로 두께 × |cos 꺾임|만큼
                    //  나간다 — 끊지 않고 얇게 한다. 90° 이하로 꺾이면 테 모서리가 두 현의 바위 쪽에 다 있어 그대로 둔다.
                    double maxT = Math.Min(ri > 0 ? ConvexThickness(runs[ri - 1], r, side) : RimThickness,
                                           ri + 1 < runs.Count ? ConvexThickness(r, runs[ri + 1], side) : RimThickness);
                    //  바위가 진행 방향 오른쪽에 오게: 아래 바위는 왼→오, 위 바위는 오→왼. 기운 테는 법선 쪽으로 x가 두께·sin만큼 삐져나오므로
                    //  이 굴 x 폭 밖(굴 앞 열린 칸·옆 굴 틈)으로 RimSlack 넘게 나가지 않게 얇게 한다.
                    if (side < 0) { AddRim(r.x0, r.e0, r.x1, r.e1, t.X0, t.X1, maxT, list); } else { AddRim(r.x1, r.e1, r.x0, r.e0, t.X0, t.X1, maxT, list); }
                }

                //  세로 면: 굴 시작(열린 칸 ↔ 첫 굴), 굴 사이(앞 굴 끝 ↔ 이 굴 시작), 마지막 굴 끝(↔ 열린 통로).
                double start = Eff(t.Center(t.X0) + side * t.Gap * 0.5f);
                MineTube prev = ti > 0 ? tubes[ti - 1] : null;
                if (ti == 0)
                {
                    double open = Clamp(side < 0 ? c.CenterAtD(t.X0) - c.HalfAtD(t.X0) : c.CenterAtD(t.X0) + c.HalfAtD(t.X0), side, side < 0 ? lane.Y0 : lane.Y1);
                    AddFace(t, side, t.X0, open, start, rockRight: true, list);
                }
                else
                {
                    //  더 통로 쪽으로 나온 가장자리의 바위가 면을 낸다.
                    bool thisProtrudes = side < 0 ? start > prevEnd : start < prevEnd;
                    AddFace(thisProtrudes ? t : prev, side, t.X0, prevEnd, start, rockRight: thisProtrudes, list);
                }
                prevEnd = Eff(t.Center(t.X1) + side * t.Gap * 0.5f);
                if (ti == tubes.Count - 1)
                {
                    double open = Clamp(side < 0 ? c.CenterAtD(t.X1) - c.HalfAtD(t.X1) : c.CenterAtD(t.X1) + c.HalfAtD(t.X1), side, side < 0 ? lane.Y0 : lane.Y1);
                    AddFace(t, side, t.X1, open, prevEnd, rockRight: false, list);
                }
            }
        }

        //  두 현 사이 꺾임에서 테가 가질 수 있는 두께. 바위 쪽으로 90° 넘게 볼록하면 RimSlack / |cos 꺾임|(이웃 현 너머로 나간 깊이 =
        //  두께·|cos|), 아니면 RimThickness. 아래 바위(side −1)는 진행 방향 오른쪽 — 오른쪽으로 꺾일 때 볼록. 위 바위는 왼쪽으로 꺾일 때.
        static double ConvexThickness((double x0, double x1, double e0, double e1) a, (double x0, double x1, double e0, double e1) b, int side)
        {
            double a1 = Math.Atan2(a.e1 - a.e0, a.x1 - a.x0), a2 = Math.Atan2(b.e1 - b.e0, b.x1 - b.x0);
            double turn = a2 - a1;   // + = 왼쪽으로
            if (side < 0 ? turn >= 0 : turn <= 0) { return RimThickness; }
            double cos = Math.Cos(turn);
            //  깊이는 세로로 잰다(판정 가장자리는 x의 함수) — 기운 현에서는 세로 깊이 = 수직 깊이 / cos(현 각)이라 그만큼 더 얇게.
            double steep = Math.Min(Math.Abs(Math.Cos(a1)), Math.Abs(Math.Cos(a2)));
            return cos >= 0 ? RimThickness : Math.Min(RimThickness, RimSlack * steep / -cos);
        }

        //  두 현 사이가 바위 쪽으로 오목하면(아래 바위는 왼쪽으로, 위 바위는 오른쪽으로 꺾임) 테를 늘일 길이 = 두께·tan(꺾임/2), 볼록이면 0.
        static double ConcaveOverlap((double x0, double x1, double e0, double e1) a, (double x0, double x1, double e0, double e1) b, int side)
        {
            double turn = Math.Atan2(b.e1 - b.e0, b.x1 - b.x0) - Math.Atan2(a.e1 - a.e0, a.x1 - a.x0);
            bool concave = side < 0 ? turn > 1e-9 : turn < -1e-9;
            return concave ? RimThickness * Math.Tan(Math.Abs(turn) / 2) : 0;
        }

        //  칸 끝 쪽 바위 경계: 아래 바위는 칸 아래 끝과 통로 바닥선 중 위, 위 바위는 칸 위 끝과 통로 천장선 중 아래(칸이 통로 밖으로 1 m 나가 있다).
        //  통로가 굴 구간 안에서 오르내리면 바위가 통로 바닥·천장 쪽으로 넘치는 편(겹침)을 고른다 — 빈틈이 생기지 않게.
        static double Outer(MineCourse c, MineTube t, MineRect lane, int side)
        {
            double line = side < 0 ? double.PositiveInfinity : double.NegativeInfinity;
            foreach (double x in new double[] { t.X0, (t.X0 + t.X1) / 2.0, t.X1 })
            {
                double v = side < 0 ? c.CenterAtD(x) - c.HalfAtD(x) : c.CenterAtD(x) + c.HalfAtD(x);
                line = side < 0 ? Math.Min(line, v) : Math.Max(line, v);
            }
            return side < 0 ? Math.Max(lane.Y0, line) : Math.Min(lane.Y1, line);
        }

        //  가장자리 e를 칸 끝 쪽 경계 outer 너머로 못 가게(side −1: e ≥ outer, +1: e ≤ outer).
        static double Clamp(double e, int side, double outer) => side < 0 ? Math.Max(e, outer) : Math.Min(e, outer);

        /// <summary>
        /// 기운 현 아래(위) 삼각형 — 기둥 윗면(아랫면)과 현 사이. MergeSlope(바위 = 단위 상자 v ≥ u 삼각형, 빗변에 밝은 띠)를
        /// 현에 맞춰 놓는다: 위 바위는 원점 = 현의 낮은 끝, y 축척 +; 아래 바위는 원점 = 높은 끝, y 축척 −. x 축척은 원점에서 다른 끝 쪽(±).
        /// </summary>
        static void AddEdgeWedge(double x0, double e0, double x1, double e1, int side, List<MinePiece> list, float z = MergeSlopeZ)
        {
            double dy = e1 - e0, w = x1 - x0;
            if (Math.Abs(dy) <= 1e-4 || w <= 1e-6) { return; }
            bool originLeft = side > 0 ? e0 < e1 : e0 > e1;
            double ox = originLeft ? x0 : x1, oy = originLeft ? e0 : e1;
            float sx = (float)(originLeft ? w : -w), sy = (float)(side * Math.Abs(dy));
            list.Add(new MinePiece(MinePartKind.MergeSlope, (float)ox, (float)oy, z, 0f, (float)w, (float)w, sx, sy));
        }

        static void AddRock(double x0, double x1, double ya, double yb, List<MinePiece> list, float z = RockZ)
        {
            double lo = Math.Min(ya, yb), hi = Math.Max(ya, yb), w = x1 - x0;
            if (hi - lo <= 1e-4 || w <= 1e-6) { return; }
            list.Add(new MinePiece(MinePartKind.RockMass, (float)x0, (float)lo, z, 0f, (float)w, (float)w, (float)w, (float)(hi - lo)));
        }

        //  세로 면 x의 [ya, yb] 테. rockRight면 바위가 x 오른쪽. 테(두께 0.4)가 덮는 x 폭 안에서 바위를 낸 굴 rock의 가장자리가
        //  휘면(굴 입구의 포물선) 그만큼 테를 줄인다 — 테가 틈 안으로 들어가지 않게.
        static void AddFace(MineTube rock, int side, double x, double ya, double yb, bool rockRight, List<MinePiece> list)
        {
            double lo = Math.Min(ya, yb), hi = Math.Max(ya, yb);
            double x0 = rockRight ? x : x - RimThickness, x1 = rockRight ? x + RimThickness : x;
            x0 = Math.Max(x0, rock.X0); x1 = Math.Min(x1, rock.X1);
            for (int i = 0; i <= 8; i++)
            {
                double e = rock.Center((float)(x0 + (x1 - x0) * i / 8)) + side * rock.Gap * 0.5;
                if (side < 0) { hi = Math.Min(hi, Math.Max(e, lo)); } else { lo = Math.Max(lo, Math.Min(e, hi)); }
            }
            if (hi - lo <= 1e-3) { return; }
            //  위로 가면 오른쪽 법선이 +x, 아래로 가면 −x.
            if (rockRight) { AddRim(x, lo, x, hi, double.NegativeInfinity, double.PositiveInfinity, RimThickness, list); }
            else { AddRim(x, hi, x, lo, double.NegativeInfinity, double.PositiveInfinity, RimThickness, list); }
        }

        /// <summary>
        /// 선분 P→Q를 따라 얇은 밝은 테(<see cref="MinePartKind.EdgeRim"/> — RockMass 메시, 왼끝 아래 원점)를 깐다. 테는 진행 방향 오른쪽(바위 쪽)으로
        /// 두께만큼이라 바위가 오른쪽에 오도록 P→Q를 넘겨야 한다. 두께는 <paramref name="maxThickness"/> 이하이고, 기운 테의 바깥 모서리
        /// (P·Q + 두께·n)가 [xMin, xMax] 밖으로 <see cref="RimSlack"/> 넘게 나가지 않을 만큼만이다(끊지 않는다).
        /// </summary>
        static void AddRim(double px, double py, double qx, double qy, double xMin, double xMax, double maxThickness, List<MinePiece> list)
        {
            double dx = qx - px, dy = qy - py, len = Math.Sqrt(dx * dx + dy * dy);
            if (len <= 1e-4) { return; }
            double thick = maxThickness;
            double nxu = dy / len;   // 오른쪽 법선의 x 성분
            double lx = Math.Min(px, qx), rx = Math.Max(px, qx);
            if (nxu < -1e-9) { thick = Math.Min(thick, (lx - xMin + RimSlack) / -nxu); }
            if (nxu > 1e-9) { thick = Math.Min(thick, (xMax - rx + RimSlack) / nxu); }
            thick = Math.Max(thick, 0.02);
            //  국소 u = P→Q, v = 왼쪽 법선. 띠 [0, len] × [0, thick]의 v = thick 변이 P→Q 선이 되게 원점 = P + thick·(오른쪽 법선).
            double rnx = dy / len, rny = -dx / len;
            float ang = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            list.Add(new MinePiece(MinePartKind.EdgeRim, (float)(px + thick * rnx), (float)(py + thick * rny), RimZ, ang,
                                   (float)Math.Abs(dx), (float)len, (float)len, (float)thick));
        }

        /// <summary>굴 조각 — <c>FlappyMineGeometry.Slices</c>와 같은 float 셈(마지막 조각만 짧고 끝은 정확히 to).</summary>
        static IEnumerable<(float x0, float x1)> TubeSlices(float from, float to)
        {
            int n = (int)Math.Ceiling((to - from) / TubeStripStep - 1e-4f);
            for (int i = 0; i < n; i++)
            {
                float a = from + i * TubeStripStep;
                float b = i == n - 1 ? to : from + (i + 1) * TubeStripStep;
                yield return (a, b);
            }
        }

        // ── 배경 ──

        /// <summary>
        /// 배경 층 전부(z &gt; 2.2). [from − 15, to + 15]를 덮는다. 같은 시드면 같은 결과. 기준선은 모두 <b>통로 중심을 따라</b>
        /// 오르내린다(10-10 — 수직 갱·계단 아래에서도 카메라 시야에 남게). 굴/바깥 이음매 = <see cref="MineZones.EntranceEnd"/>.
        /// <list type="bullet">
        /// <item>실루엣(z 22 / 25): 통로 중심 꺾은선(낮은 천장 물결은 뺀 것)이 꺾이는 x와 이음매에서 끊고, 곧은 구간마다 27.2 m(20 m 띠 × 1.36)
        /// 근처 같은 폭 띠를 그 꺾은선 + 기준 높이의 <b>현</b>으로 놓는다(기울면 띠도 기운다, x 축척 = 현 길이 / 20).
        /// 가운데가 이음매 앞이면 협곡(바닥 쪽만), 뒤면 굴벽(바닥 + 뒤집은 천장 쪽). 높이(y 축척)만 시드로 흔든다.</item>
        /// <item>먼 비계(z 18): 3·FarScale m 칸, 축척 FarScale, 데크 윗면 = 통로 중심 −1.0 + 2.5·sin(0.15·x)의 현. 구역 구분 없이(바깥은 협곡 다리).
        /// <see cref="BackgroundDensity.FarRun"/>칸 놓고 <see cref="BackgroundDensity.FarGap"/>칸 비운다(0이면 이어 깐다).</item>
        /// <item>광차: [from, to] 안 먼 비계 칸 중 시드로 고른 서로 다른 칸, 레일 윗면 위(같은 축척).</item>
        /// <item>가운데 층(z 6, 축척 MidScale)·랜턴(z 4.5): 굴(x ≥ 이음매)에만, 그 자리 통로 중심 기준. 틀은 <see cref="BackgroundDensity.FrameSpacing"/>마다,
        /// 틀마다 발판 하나, 사다리는 확률. 랜턴은 <see cref="BackgroundDensity.LanternSpacing"/>마다 ±1 m.</item>
        /// </list>
        /// </summary>
        public static List<MinePiece> Background(MineCourse c, float from, float to, int seed, BackgroundDensity d)
        {
            var list = new List<MinePiece>();
            var rng = new Lcg(seed);
            double lo = from - BackgroundMargin, hi = to + BackgroundMargin;
            float seam = Zones(c).EntranceEnd;

            AddSilhouettes(c, lo, hi, seam, rng, list);
            var far = AddFarTrestles(c, lo, hi, d.FarScale, d.FarRun, d.FarGap, list);
            AddCarts(far, from, to, d.CartCount, d.FarScale, rng, list);
            AddMidLayer(c, lo, hi, seam, d, rng, list);
            return list;
        }

        //  실루엣 층 하나: (z, 층, 바닥 쪽 기준선 높이, 천장 쪽 기준선 높이(NaN = 없음), y 축척 범위). 기준선은 통로 가운데 기준.
        //  띠의 들쭉날쭉한 윗선은 기준선 위 1.4~5.3(Task 3) — 시안 silhouette(15, −2.0, 4.0)·(15, 6.5, 3.0, up)이 굴의 먼 층이다.
        //  처음 값(z 13·15, 카메라 20)을 z 22·25(카메라 23)로 옮기며 기준선·축척에 카메라 거리 비를 곱했다
        //  (가까운 층 45/33 ≈ 1.36, 먼 층 48/35 ≈ 1.37) — 화면에서 보이는 자리·크기는 그대로다.
        static readonly (float z, int layer, float floorBase, float ceilBase, float sMin, float sMax)[] CaveLayers =
        {
            (SilhouetteNearZ, 0, -8.2f, 13.6f, 1.1f, 1.65f),
            (SilhouetteFarZ, 1, -4.65f, 10.8f, 1.1f, 1.65f),
        };
        //  협곡(노을 바깥): 시안 silhouette(20, −7, 6)·(26, −4, 9) — 바닥 쪽만, 높이 크게.
        static readonly (float z, int layer, float floorBase, float ceilBase, float sMin, float sMax)[] CanyonLayers =
        {
            (SilhouetteNearZ, 0, -11.6f, float.NaN, 1.65f, 2.2f),
            (SilhouetteFarZ, 1, -9.6f, float.NaN, 2.35f, 3.15f),
        };

        //  SilhouetteStrip 원래 폭(x ±10).
        const float SilhouetteUnit = 20f;

        static void AddSilhouettes(MineCourse c, double lo, double hi, float seam, Lcg rng, List<MinePiece> list)
        {
            //  끊는 점 = 범위 양끝 + 이음매 + 통로 중심 꺾은선이 꺾이는 x. 곧은 구간마다 27.2 m 근처 같은 폭 띠 — 띠가 꺾이는 곳을
            //  가로지르지 않으니 현이 곧 꺾은선이다(기준선이 통로 중심을 정확히 따른다).
            var xs = new List<double> { lo };
            if (seam > lo && seam < hi) { xs.Add(seam); }
            for (int i = 1; i + 1 < c.Points.Count; i++)
            {
                double x = c.Points[i].x;
                if (x <= lo || x >= hi) { continue; }
                double s0 = PolySlope(c, i - 1), s1 = PolySlope(c, i);
                if (Math.Abs(s0 - s1) > 1e-9) { xs.Add(x); }
            }
            xs.Add(hi);
            xs.Sort();

            for (int r = 0; r + 1 < xs.Count; r++)
            {
                foreach (var (x0, x1) in Tiles((float)xs[r], (float)xs[r + 1], SilhouetteWidth))
                {
                    double mid = (x0 + x1) / 2;
                    bool canyon = mid < seam;
                    var kind = canyon ? MinePartKind.CanyonSilhouette : MinePartKind.CaveSilhouette;
                    foreach (var l in canyon ? CanyonLayers : CaveLayers)
                    {
                        float s = rng.Range(l.sMin, l.sMax);
                        list.Add(SilhouetteChord(c, kind, x0, x1, l.floorBase, l.z, s, false, l.layer));
                        if (!float.IsNaN(l.ceilBase))
                        {
                            float sc = rng.Range(l.sMin, l.sMax);
                            list.Add(SilhouetteChord(c, kind, x0, x1, l.ceilBase, l.z, sc, true, l.layer));
                        }
                    }
                }
            }
        }

        static MinePiece SilhouetteChord(MineCourse c, MinePartKind kind, double x0, double x1, float baseOffset, float z, float scaleY, bool flipped, int layer)
        {
            var p = Chord(kind, x0, x1, x => PolyCenter(c, x) + baseOffset, z, SilhouetteUnit, scaleY);
            return new MinePiece(p.Kind, p.X, p.Y, p.Z, p.AngleDegrees, p.Width, p.Length, p.ScaleX, p.ScaleY, flipped, layer);
        }

        /// <summary>통로 중심 꺾은선(낮은 천장 물결을 뺀 <c>MineCourse.CenterAt</c>) — 먼 배경 띠가 물결마다 꺾이지 않게.</summary>
        static double PolyCenter(MineCourse c, double x)
        {
            var pts = c.Points;
            if (x <= pts[0].x) { return pts[0].y; }
            for (int i = 1; i < pts.Count; i++)
            {
                if (x <= pts[i].x)
                {
                    var (x0, y0) = pts[i - 1];
                    var (x1, y1) = pts[i];
                    return y0 + (y1 - y0) * (x - x0) / Math.Max(1e-6, x1 - x0);
                }
            }
            return pts[pts.Count - 1].y;
        }

        //  꺾은선 i번째 마디(점 i → i+1)의 기울기.
        static double PolySlope(MineCourse c, int i)
        {
            var (x0, y0) = c.Points[i];
            var (x1, y1) = c.Points[i + 1];
            return (y1 - y0) / Math.Max(1e-6, x1 - x0);
        }

        static List<MinePiece> AddFarTrestles(MineCourse c, double lo, double hi, float scale, int run, int gap, List<MinePiece> list)
        {
            var far = new List<MinePiece>();
            //  칸 폭은 줄인 만큼 좁게(축척은 x·y 같이). 높이 곡선은 그대로 x의 함수다(칸 폭과 무관).
            double w = BgTrestleBayWidth * (double)scale;
            int k0 = (int)Math.Floor(lo / w), k1 = (int)Math.Ceiling(hi / w);
            for (int k = k0; k < k1; k++)
            {
                //  무리로 끊기(Task 7 Fix 1 — 배경이 장애물 집중을 흐렸다): 칸 번호 k의 격자로 정해 범위를 바꿔도 무리가 안 움직인다.
                if (run > 0 && gap > 0 && ((k % (run + gap)) + (run + gap)) % (run + gap) >= run) { continue; }
                var p = Chord(MinePartKind.BgTrestleBay, k * w, (k + 1) * w,
                              x => c.CenterAtD(x) + FarBase + FarAmp * Math.Sin(x / BgTrestleBayWidth * FarPhasePerBay), FarZ, BgTrestleBayWidth, scale);
                far.Add(p);
                list.Add(p);
            }
            return far;
        }

        static void AddCarts(List<MinePiece> far, float from, float to, int count, float scale, Lcg rng, List<MinePiece> list)
        {
            var pool = far.FindAll(p => p.X >= from && p.X <= to);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int j = (int)(rng.Next() * pool.Count);
                var bay = pool[j];
                pool.RemoveAt(j);
                list.Add(new MinePiece(MinePartKind.MineCart, bay.X, bay.Y + RailTop * scale, bay.Z, bay.AngleDegrees,
                                       MineCartWidth * scale, MineCartWidth * scale, scale, scale));
            }
        }

        static void AddMidLayer(MineCourse c, double lo, double hi, float seam, BackgroundDensity d, Lcg rng, List<MinePiece> list)
        {
            //  격자는 굴 시작(이음매 = 갱 입구 문틀 끝)에서 시작한다 — 범위를 늘려도 이미 있던 틀이 움직이지 않게.
            double start = Math.Max(lo, seam);
            if (d.FrameSpacing > 0f)
            {
                for (int k = (int)Math.Ceiling((start - seam) / d.FrameSpacing - 0.5); ; k++)
                {
                    double x = seam + (k + 0.5) * d.FrameSpacing;
                    if (x > hi) { break; }
                    if (x < start) { continue; }
                    //  축척 m = MidScale. 틀 윗면·발판·사다리 위 끝 높이는 그대로 두고 크기만 줄인다(틀 다리는 데크 아래로 숨는다).
                    float fx = (float)x, center = c.CenterAt(fx), m = d.MidScale;
                    list.Add(new MinePiece(MinePartKind.BgFrame, fx, center + FrameTop, MidZ, 0f, BgFrameWidth * m, BgFrameWidth * m, m, m));
                    float walk = WalkLow + rng.Range(0f, WalkRange);
                    list.Add(new MinePiece(MinePartKind.Walkway, fx, center + walk, MidZ, 0f, WalkwayWidth * m, WalkwayWidth * m, m, m));
                    //  확률은 항상 뽑는다 — 사다리 비율을 바꿔도 뒤 소품의 난수가 밀리지 않게.
                    if (rng.Next() < d.LadderChance)
                    {
                        float lx = fx + LadderOffsetX * m;
                        list.Add(new MinePiece(MinePartKind.Ladder, lx, c.CenterAt(lx) + LadderTop, MidZ + LadderDz, 0f, LadderWidth * m, LadderWidth * m, m, m));
                    }
                }
            }

            if (d.LanternSpacing > 0f)
            {
                for (int k = (int)Math.Ceiling((start - seam) / d.LanternSpacing - 0.5); ; k++)
                {
                    double x = seam + (k + 0.5) * d.LanternSpacing;
                    if (x > hi) { break; }
                    float jitter = rng.Range(-LanternJitter, LanternJitter);
                    if (x < start) { continue; }
                    float lx = (float)Math.Max(seam, x + jitter);
                    float hang = c.CenterAt(lx) + LanternGlass + LanternGlassBelowHang;
                    list.Add(new MinePiece(MinePartKind.Lantern, lx, hang, LanternZ, 0f, LanternWidth, LanternWidth, 1f, 1f));
                }
            }
        }

        // ── 회색 박스와 그림의 경계(스펙 §6) ──

        //  회색 박스 조각 경계는 Breaks의 float 값이라 범위 끝과 1e-3 안쪽으로 어긋날 수 있다.
        const double SpanEps = 1e-3;

        /// <summary>x 폭 [x0, x1]이 [from, to] 안에 완전히 드나 — 그런 회색 박스 조각만 렌더러를 끈다(끝이 맞닿은 것은 안).</summary>
        public static bool SpanInside(float x0, float x1, float from, float to)
        {
            return x0 >= from - SpanEps && x1 <= to + SpanEps;
        }

        /// <summary>x 폭 [x0, x1]이 [from, to]와 조금이라도 겹치나(끝만 맞닿은 것은 아니다) — 관문은 걸치기만 해도 통째로 입힌다.</summary>
        public static bool SpanOverlaps(float x0, float x1, float from, float to)
        {
            return x1 > from + SpanEps && x0 < to - SpanEps;
        }

        // ── 도우미 ──

        /// <summary>
        /// 꺾이는 곳에 맞춘 칸. 끊는 점 = {from, to} ∪ (<c>c.Breaks(BreakStep)</c> 중 from &lt; x &lt; to) — 회색 박스 바닥·천장 조각과 같은 x다.
        /// 이웃한 두 구간이 바닥선과 천장선 <b>둘 다</b> 같은 기울기(차 &lt; 1e-6)면 한 직선 구간으로 합치고, 직선 구간마다
        /// <see cref="Tiles"/>로 unit 근처 같은 폭 칸을 낸다. 그래서 칸 양끝이 판정선 위에 있고 칸 안에서 판정선이 꺾이지 않는다.
        /// 곡선(낮은 천장 물결)·전이 구간은 0.5 m 조각마다 꺾이므로 그 칸은 0.5 m로 짧다.
        /// </summary>
        internal static IEnumerable<(double x0, double x1)> BentTiles(MineCourse c, float from, float to, float unit)
        {
            var xs = StraightBreaks(c, from, to);
            for (int i = 1; i < xs.Count; i++)
            {
                foreach (var t in Tiles((float)xs[i - 1], (float)xs[i], unit)) { yield return t; }
            }
        }

        /// <summary>
        /// [from, to]를 바닥선·천장선이 둘 다 곧은 구간으로 끊는 x(양끝 포함, 오름차순). 후보 = {from, to} ∪ (from &lt; x &lt; to인
        /// <c>c.Breaks(BreakStep)</c>), 이웃한 두 구간이 바닥·천장 둘 다 같은 기울기(차 &lt; 1e-6)면 그 사이 점은 뺀다. 범위가 비면 빈 목록.
        /// </summary>
        internal static List<double> StraightBreaks(MineCourse c, float from, float to)
        {
            var result = new List<double>();
            if ((double)to - from <= 1e-6) { return result; }
            var xs = new List<double> { from };
            foreach (float b in c.Breaks(BreakStep))
            {
                if (b > from && b < to) { xs.Add(b); }
            }
            xs.Add(to);

            double Floor(double x) => c.CenterAtD(x) - c.HalfAtD(x);
            double Ceil(double x) => c.CenterAtD(x) + c.HalfAtD(x);

            result.Add(xs[0]);
            for (int i = 1; i < xs.Count; i++)
            {
                //  다음 구간이 이 구간과 기울기가 같으면 이어 붙인다. 마지막 구간이거나 꺾이면 xs[i]가 끊는 점이다.
                bool last = i == xs.Count - 1;
                if (!last)
                {
                    double w0 = xs[i] - xs[i - 1], w1 = xs[i + 1] - xs[i];
                    double f0 = (Floor(xs[i]) - Floor(xs[i - 1])) / w0, f1 = (Floor(xs[i + 1]) - Floor(xs[i])) / w1;
                    double c0 = (Ceil(xs[i]) - Ceil(xs[i - 1])) / w0, c1 = (Ceil(xs[i + 1]) - Ceil(xs[i])) / w1;
                    if (Math.Abs(f0 - f1) < 1e-6 && Math.Abs(c0 - c1) < 1e-6) { continue; }
                }
                result.Add(xs[i]);
            }
            return result;
        }

        /// <summary>[from, to]를 unit에 가장 가까운 같은 폭 n칸으로. 칸 경계는 double로 셈해 오차가 쌓이지 않는다.</summary>
        static IEnumerable<(double x0, double x1)> Tiles(float from, float to, float unit)
        {
            double len = (double)to - from;
            if (len <= 1e-6) { yield break; }
            int n = Math.Max(1, (int)Math.Round(len / unit));
            for (int i = 0; i < n; i++) { yield return (from + len * i / n, from + len * (i + 1) / n); }
        }

        /// <summary>선 f 위의 두 점 (x0, f(x0))·(x1, f(x1))을 잇는 현에 놓인 조각. 축척 x = 현 길이 / 부품 단위, 축척 y = <paramref name="scaleY"/>.</summary>
        static MinePiece Chord(MinePartKind kind, double x0, double x1, Func<double, double> f, float z, float unit, float scaleY = 1f)
        {
            double y0 = f(x0), y1 = f(x1), w = x1 - x0, dy = y1 - y0;
            double len = Math.Sqrt(w * w + dy * dy);
            return new MinePiece(kind, (float)((x0 + x1) / 2), (float)((y0 + y1) / 2), z, (float)(Math.Atan2(dy, w) * 180.0 / Math.PI),
                                 (float)w, (float)len, (float)(len / unit), scaleY);
        }

        /// <summary>MineCourseRule과 같은 LCG(uint 넘침). System.Random은 런타임마다 수열이 다를 수 있어 쓰지 않는다.</summary>
        sealed class Lcg
        {
            uint s;

            public Lcg(int seed) { s = unchecked((uint)seed); }

            public double Next()
            {
                s = unchecked(s * 1664525u + 1013904223u);
                return s / 4294967296.0;
            }

            public float Range(float a, float b) => (float)(a + (b - a) * Next());
        }
    }
}
