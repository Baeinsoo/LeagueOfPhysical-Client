using System;
using System.Collections.Generic;
using System.Linq;

namespace LOP.MapTools
{
    /// <summary>
    /// 광산 옷 부품 종류. 이름은 Art/Models/Mine의 FBX 이름과 같다(실루엣 둘은 SilhouetteStrip 하나를 재질만 바꿔 쓴다).
    /// 10-10 펼치기 1차 부품 14종(Task 3 보고 표)과, 바위 가장자리 밝은 테로 다시 쓰는 <see cref="GateCap"/>(관문 쇠테)을 뒤에 더했다.
    /// </summary>
    public enum MinePartKind
    {
        TrestleBay, CeilingRock, Beam,
        BgTrestleBay, MineCart, BgFrame, Ladder, Walkway, Lantern, CaveSilhouette, CanyonSilhouette,
        EntranceFrame, EntranceSign, LanternString, ShaftTimber, CageRail, Cage, BenchFace, ShortScaffold,
        RockMass, BoreFrame, BoltSign, PadGlow, DividerWall, MergeSlope,
        /// <summary>관문 쇠테(GateCap.fbx). 굴·패드 방 바위의 닿는 가장자리 밝은 테로 쓴다 — RockMass에는 테가 없다.</summary>
        GateCap,
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
        //  원근 카메라가 판정면 뒤 면을 틈 쪽으로 당겨 그려 틈이 좁아 보이지 않게. 장식(문틀·간판·줄·랜턴·케이지·비계·⚡ 판)은
        //  앞면이 판정면 뒤(z &gt; 0).
        /// <summary>
        /// EntranceFrame x ±2.5(문턱 가운데 원점), 기둥 안쪽 면 x ±1.9, 상인방 y 14.62~15.42(문턱 기준), z 0.03~0.68.
        /// 간판(높이 1) 가운데는 아랫변이 상인방 아랫변(14.62)에 오게 — 문틀 안 통로(14.56) 위.
        /// </summary>
        public const float EntranceHalfWidth = 2.5f, EntrancePostInner = 1.9f, EntranceSignCenter = 15.12f;
        /// <summary>랜턴 줄을 거는 높이(문턱 기준, 상인방 바로 아래)와 처짐(LanternString y = −0.35·4t(1−t)).</summary>
        public const float EntranceStringHang = 14.3f, LanternStringSag = 0.35f;
        /// <summary>줄에 다는 랜턴(Lantern, 사슬 위 끝 원점) 축척 — 줄 부품 안 랜턴은 늘어나 알약이 되므로 따로 단다.</summary>
        public const float StringLanternScale = 0.45f;
        /// <summary>문틀(z 0.03~)은 그대로 판정면 뒤. 간판(z −0.085~0.07)은 상인방 앞면에 붙여 앞면이 판정면 바로 뒤. 줄·랜턴은 기둥 사이 깊이 가운데.</summary>
        public const float EntranceFrameZ = 0f, EntranceSignZ = 0.09f, EntranceStringZ = 0.36f;

        /// <summary>ShaftTimber 높이(왼끝 아래 원점, y 0~0.4, z ±0.5), 한 조각 길이 상한, 갱 벽 두께(판정선에서 바깥으로 — 카메라 반 시야 + 대시 여유).</summary>
        public const float ShaftTimberHeight = 0.4f, ShaftTimberUnit = 3f, ShaftWallDepth = 7f, ShaftTimberZ = -0.5f;
        /// <summary>CageRail(아래 끝 가운데, z ±0.11)·Cage(가운데, z ±0.605) — 갱목(뒷면 0) 뒤.</summary>
        public const float CageZ = 1.0f;

        /// <summary>BenchFace(윗면 앞 모서리 왼끝, z −1.35~1.3). ShortScaffold 다리 x ±1.05(아래 가운데 원점, z ±0.35).</summary>
        public const float BenchZ = -1.3f, ScaffoldLegX = 1.05f, ScaffoldZ = 0.4f;

        /// <summary>
        /// 굴 회색 박스 조각 폭 — <c>FlappyMineCourseBuilder.TubeStripStep</c>과 같아야 한다(바위 테가 같은 현을 따라간다).
        /// </summary>
        public const float TubeStripStep = 0.25f;
        /// <summary>
        /// RockMass(왼끝 아래, z ±1.3) — 뒷면 0. 밝은 테(GateCap, z −0.80~0.73, 두께 0.4)는 바위 앞면(−2.6)보다 앞.
        /// 테가 틈 쪽으로 삐져나올 수 있는 곳(바위가 90° 넘게 볼록하게 꺾이는 V, 굴 x 폭 끝에서 기운 테)은 끊지 않고 y 축척으로 얇게 해
        /// 삐져나온 깊이를 <see cref="RimSlack"/> 이하로 둔다(그림 경계 = 판정 경계 ± 0.05).
        /// </summary>
        public const float RockZ = -1.3f, RimZ = -1.9f, RimThickness = 0.4f, RimSlack = 0.04f;
        /// <summary>MergeSlope(왼끝 아래, z −1.35~1.3) — 바위 앞면과 겹치지 않게 0.02 앞. 방 끝에서 덮는 폭.</summary>
        public const float MergeSlopeZ = -1.32f, MergeSlopeWidth = 2.4f;
        /// <summary>DividerWall(왼끝 가운데, y ±0.6 = 1.2, z ±0.75).</summary>
        public const float DividerZ = -0.75f, DividerHeight = 1.2f;
        //  BoreFrame(굴 입구 문틀)은 Fix round 1부터 놓지 않는다: 상인방·문턱이 x −0.3~1.0 곧은 각재라 굴 앞에 두면 열린 칸 위,
        //  굴 안에 두면 휘는 틈 위에 판정면 앞(앞면 −1.84)으로 걸치고, 틈을 가로지르는 문설주(z 0.2~0.5)를 판정면 뒤에 두려면
        //  바위(앞면 −2.6)에 묻힌다. 메시를 고친 뒤(문설주를 뒤로, 상인방·문턱을 짧게·앞으로) 다시 넣는다. 굴 시작 세로 면은 밝은 테가 맡는다.
        /// <summary>BoltSign(가운데, x ±0.95, y ±0.547, z −0.11~0.06) — 굴 입구 앞 열린 칸 위쪽, 판정면 뒤.</summary>
        public const float BoltSignZ = 0.12f, BoltSignHalfWidth = 0.95f, BoltSignHalfHeight = 0.547f, BoltSignAboveMouth = 0.25f, BoltSignBeforeMouth = 0.3f;
        /// <summary>PadGlow(가운데, y ±0.15, z −0.67~0.65) — 윗면이 방 바닥선, 밝은 테보다 앞.</summary>
        public const float PadGlowZ = -2.1f, PadGlowHalfHeight = 0.15f;
        /// <summary>GateCap 폭(x ±0.975). 긴 테는 이 폭 이하로 나눠 볼트가 늘어나지 않게.</summary>
        const float RimUnit = 1.95f;

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

            public BackgroundDensity(float frameSpacing, float ladderChance, float lanternSpacing, int cartCount, float farScale, float midScale)
            {
                FrameSpacing = frameSpacing; LadderChance = ladderChance; LanternSpacing = lanternSpacing; CartCount = cartCount;
                FarScale = farScale; MidScale = midScale;
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
        /// 문턱 = 그 자리 통로 중심 − 7.28(문틀 안 통로 14.56이 넓은 통로와 겹친다). 간판은 상인방 앞(통로 위), 랜턴 줄은 기둥 안쪽 면
        /// 사이 상인방 바로 아래 — 줄 부품은 줄로만 쓰고 랜턴은 <see cref="MinePartKind.Lantern"/>을 작게 줄을 따라 <paramref name="lanternsPerString"/>개.
        /// 전부 장식이라 판정면 뒤.
        /// </summary>
        public static List<MinePiece> Entrance(MineCourse c, MineZones z, int lanternsPerString = 4)
        {
            var list = new List<MinePiece>();
            float fx = z.EntranceEnd - EntranceHalfWidth;
            float threshold = c.CenterAt(fx) - MineCourse.BaseHalf;
            float w = EntranceHalfWidth * 2f;
            list.Add(new MinePiece(MinePartKind.EntranceFrame, fx, threshold, EntranceFrameZ, 0f, w, w, 1f, 1f));
            list.Add(new MinePiece(MinePartKind.EntranceSign, fx, threshold + EntranceSignCenter, EntranceSignZ, 0f, 3f, 3f, 1f, 1f));

            float left = fx - EntrancePostInner, span = EntrancePostInner * 2f, hang = threshold + EntranceStringHang;
            list.Add(new MinePiece(MinePartKind.LanternString, left, hang, EntranceStringZ, 0f, span, span, span, 1f));
            for (int i = 0; i < lanternsPerString; i++)
            {
                float t = (i + 0.5f) / lanternsPerString;
                float lw = LanternWidth * StringLanternScale;
                list.Add(new MinePiece(MinePartKind.Lantern, left + t * span, hang - LanternStringSag * 4f * t * (1f - t), EntranceStringZ, 0f,
                                       lw, lw, StringLanternScale, StringLanternScale));
            }
            return list;
        }

        // ── 수직 갱 ──

        /// <summary>
        /// 수직 갱 갱도(스펙 §5): 좁은 구간(<c>Narrows</c>) [a − 3, b + 3]마다(앞은 문틀 끝, 뒤는 옷 끝에서 자른다)
        /// <list type="bullet">
        /// <item>바닥·천장 쪽 벽에 <see cref="MinePartKind.ShaftTimber"/>를 층층이 — 판정선이 꺾이는 곳(<see cref="StraightBreaks"/>)에서 끊은
        /// 꺾은선을 바깥(바닥은 아래, 천장은 위)으로 <paramref name="timberSpacing"/>씩 평행 이동(꺾이는 곳은 맞댐)한 줄마다, 줄 하나를 3 m 이하
        /// 조각으로. 첫 줄의 통로 쪽 변이 판정선이고 나머지는 전부 판정선 바깥이다. 깊이 <see cref="ShaftWallDepth"/>까지.</item>
        /// <item>뒤(z &gt; 0)에 승강 레일 <see cref="MinePartKind.CageRail"/> + 케이지 <see cref="MinePartKind.Cage"/> 하나: 가장 가파른 낙하 마디의
        /// <paramref name="cageRatio"/> 자리 x, 케이지는 그 자리 통로 가운데, 레일은 위·아래 벽 속(갱목이 가린다)까지.</item>
        /// </list>
        /// </summary>
        public static List<MinePiece> ShaftWalls(MineCourse c, MineZones z, float timberSpacing, float cageRatio = 0.3f)
        {
            var list = new List<MinePiece>();
            if (!(timberSpacing > 0f)) { throw new ArgumentOutOfRangeException(nameof(timberSpacing)); }
            foreach (var (a, b, _) in c.Narrows)
            {
                var (from, to) = ShaftSpan(a, b, z);
                if (to - from <= 1e-6) { continue; }
                var xs = StraightBreaks(c, (float)from, (float)to);
                int layers = (int)Math.Ceiling(ShaftWallDepth / timberSpacing - 1e-6);
                foreach (int side in new[] { -1, 1 })
                {
                    var verts = MergeStraight(xs.Select(x => (x, c.CenterAtD(x) + side * c.HalfAtD(x))).ToList());
                    for (int k = 0; k < layers; k++) { AddTimberLayer(verts, side, k * (double)timberSpacing, from, to, list); }
                }
                AddCage(c, a, b, from, to, cageRatio, list);
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

        //  꺾은선 verts를 바깥으로 d만큼 평행 이동(꺾이는 곳은 두 선이 만나는 점으로 맞댐)한 줄에 갱목을 깐다 — 줄은 [x0, x1] 안으로 자른다
        //  (가파른 갱에서 깊은 줄은 법선 방향으로 수 m 옆으로 밀려 범위 밖 문틀 구간으로 나간다).
        //  side −1 = 바닥 쪽(바깥 = 진행 방향 오른쪽 = 아래), +1 = 천장 쪽(왼쪽 = 위).
        static void AddTimberLayer(List<(double x, double y)> verts, int side, double d, double x0, double x1, List<MinePiece> list)
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
                double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                var (nx, ny) = normals[j];
                for (int i = 0; i < pieces; i++)
                {
                    double px = sx + dx * i / pieces, py = sy + dy * i / pieces;
                    //  원점 = 왼끝 아래. 천장 쪽은 통로 쪽 변이 아래 변이라 그대로, 바닥 쪽은 위 변이 통로 쪽이라 바깥으로 0.4 내린다.
                    if (side < 0) { px += nx * ShaftTimberHeight; py += ny * ShaftTimberHeight; }
                    float l = (float)(len / pieces);
                    list.Add(new MinePiece(MinePartKind.ShaftTimber, (float)px, (float)py, ShaftTimberZ, (float)ang,
                                           (float)Math.Abs(dx / pieces), l, l, 1f));
                }
            }
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

        static void AddCage(MineCourse c, double a, double b, double from, double to, float ratio, List<MinePiece> list)
        {
            //  가장 가파른 낙하 마디(통로 중심 꺾은선) — 좁은 구간 [a, b] 안.
            double bestSlope = 0, x0 = double.NaN, x1 = double.NaN;
            for (int i = 0; i + 1 < c.Points.Count; i++)
            {
                var (pa, _) = c.Points[i];
                var (pb, _) = c.Points[i + 1];
                if (pa < a - 1e-6 || pb > b + 1e-6 || pb - pa <= 1e-6) { continue; }
                double slope = Math.Abs(PolySlope(c, i));
                if (slope > bestSlope) { bestSlope = slope; x0 = pa; x1 = pb; }
            }
            if (double.IsNaN(x0)) { return; }
            float cx = (float)(x0 + ratio * (x1 - x0));
            if (cx < from || cx > to) { return; }
            list.Add(new MinePiece(MinePartKind.Cage, cx, c.CenterAt(cx), CageZ, 0f, 1.6f, 1.6f, 1f, 1f));
            float bottom = c.CenterAt(cx) - c.HalfAt(cx) - ShaftWallDepth, top = c.CenterAt(cx) + c.HalfAt(cx) + ShaftWallDepth;
            list.Add(new MinePiece(MinePartKind.CageRail, cx, bottom, CageZ, 0f, 2.06f, top - bottom, 1f, top - bottom));
        }

        // ── 계단 내리막 ──

        /// <summary>
        /// 계단 내리막(스펙 §5): "계단 내리막" 구간(옷 끝 앞)의 계단 칸 = 통로 중심 꺾은선 마디 중 구간 안에서 끝나고 높이가 바뀌는 것
        /// (3막 41.225 ~ 73.625, 5.4 m씩 6칸). 칸마다
        /// <list type="bullet">
        /// <item>바닥 쪽 <see cref="MinePartKind.BenchFace"/>: 원점 = 칸 왼끝 바닥선, 현 각도·길이 — 윗면(밝은 띠)이 바닥선 그대로, 몸통은 아래로 4.5.</item>
        /// <item>천장 쪽 BenchFace: Z축 180°(<see cref="MinePiece.Flipped"/>) — 원점 = 칸 오른끝 천장선, 몸통은 위로.</item>
        /// <item><see cref="MinePartKind.ShortScaffold"/>: 칸 가운데, 판정면 뒤. 내리막 쪽 다리가 바닥선에 닿고 오르막 쪽은 단(앞면이 가린다)에 묻힌다.</item>
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

            float mx = (float)((x0 + x1) / 2);
            float legY = Math.Min(c.CenterAt(mx - ScaffoldLegX) - c.HalfAt(mx - ScaffoldLegX), c.CenterAt(mx + ScaffoldLegX) - c.HalfAt(mx + ScaffoldLegX));
            list.Add(new MinePiece(MinePartKind.ShortScaffold, mx, legY, ScaffoldZ, 0f, 2.4f, 2.4f, 1f, 1f));
        }

        // ── 고수 갈림길 ──

        /// <summary>
        /// 고수 갈림길 하나(스펙 §3·§5). 판정 기하를 그대로 그린다:
        /// <list type="bullet">
        /// <item><see cref="MinePartKind.DividerWall"/>: 칸막이 <see cref="MineRect"/> 그대로(원점 왼끝 가운데, x 축척 = 폭, y 축척 = 높이 / 1.2).</item>
        /// <item>굴 칸 바위(<see cref="MinePartKind.RockMass"/>): 굴(틈 3.1)·패드 방(틈 4.6) 틈 밖 ~ 칸 끝(칸 끝이 통로 밖이면 통로 바닥·천장선까지).
        /// 굴마다 큰 사각형 하나 + 휘는 가장자리는 회색 박스와 같은 0.25 m 조각(<see cref="TubeStripStep"/>)마다 기둥, 기둥 끝과 현 사이 삼각형은
        /// <see cref="MinePartKind.MergeSlope"/>(빗변 = 현). 그래서 그림 경계가 회색 박스 현 그대로다.</item>
        /// <item>밝은 테(<see cref="MinePartKind.GateCap"/> — 관문 쇠테를 다시 씀): 바위의 닿는 가장자리 전부 — 굴·방 틈 가장자리 현(같은 기울기는
        /// 이어 붙여 1.95 m 이하 칸으로), 굴 시작·굴→방·방 끝의 세로 면. 두께 0.4가 바위 쪽, 바위 앞면보다 앞.</item>
        /// <item><see cref="MinePartKind.BoltSign"/>: 굴 입구 앞 열린 칸, 굴 틈 위 끝 위(판정면 뒤). <see cref="MinePartKind.BoreFrame"/>은 메시를 고칠 때까지 놓지 않는다(상수 옆 주석).</item>
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

            MineTube bore = tubes[0];
            float mouth = bore.Center(bore.X0), mouthTop = mouth + bore.Gap * 0.5f;
            list.Add(new MinePiece(MinePartKind.BoltSign, bore.X0 - BoltSignBeforeMouth - BoltSignHalfWidth, mouthTop + BoltSignAboveMouth + BoltSignHalfHeight,
                                   BoltSignZ, 0f, BoltSignHalfWidth * 2f, BoltSignHalfWidth * 2f, 1f, 1f));

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
        static void AddEdgeWedge(double x0, double e0, double x1, double e1, int side, List<MinePiece> list)
        {
            double dy = e1 - e0, w = x1 - x0;
            if (Math.Abs(dy) <= 1e-4 || w <= 1e-6) { return; }
            bool originLeft = side > 0 ? e0 < e1 : e0 > e1;
            double ox = originLeft ? x0 : x1, oy = originLeft ? e0 : e1;
            float sx = (float)(originLeft ? w : -w), sy = (float)(side * Math.Abs(dy));
            list.Add(new MinePiece(MinePartKind.MergeSlope, (float)ox, (float)oy, MergeSlopeZ, 0f, (float)w, (float)w, sx, sy));
        }

        static void AddRock(double x0, double x1, double ya, double yb, List<MinePiece> list)
        {
            double lo = Math.Min(ya, yb), hi = Math.Max(ya, yb), w = x1 - x0;
            if (hi - lo <= 1e-4 || w <= 1e-6) { return; }
            list.Add(new MinePiece(MinePartKind.RockMass, (float)x0, (float)lo, RockZ, 0f, (float)w, (float)w, (float)w, (float)(hi - lo)));
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
        /// 선분 P→Q를 따라 밝은 테(GateCap — 가운데 원점, 윗면 = 원점, 두께 0.4 × y 축척이 국소 −y)를 깐다. 국소 −y가 진행 방향 오른쪽이므로
        /// 바위가 오른쪽에 오도록 P→Q를 넘겨야 한다. 1.95 m 이하 같은 폭 칸으로 나눈다. 두께는 <paramref name="maxThickness"/> 이하이고,
        /// 기운 테의 바깥 모서리(P·Q + 두께·n)가 [xMin, xMax] 밖으로 <see cref="RimSlack"/> 넘게 나가지 않을 만큼만이다(끊지 않는다).
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
            int n = Math.Max(1, (int)Math.Ceiling(len / RimUnit - 1e-3));
            float ang = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI), l = (float)(len / n);
            for (int i = 0; i < n; i++)
            {
                double t = (i + 0.5) / n;
                list.Add(new MinePiece(MinePartKind.GateCap, (float)(px + dx * t), (float)(py + dy * t), RimZ, ang,
                                       (float)Math.Abs(dx / n), l, l / RimUnit, (float)(thick / RimThickness)));
            }
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
        /// <item>먼 비계(z 18): 3·FarScale m 칸, 축척 FarScale, 데크 윗면 = 통로 중심 −1.0 + 2.5·sin(0.15·x)의 현. 구역 구분 없이(바깥은 협곡 다리).</item>
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
            var far = AddFarTrestles(c, lo, hi, d.FarScale, list);
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

        static List<MinePiece> AddFarTrestles(MineCourse c, double lo, double hi, float scale, List<MinePiece> list)
        {
            var far = new List<MinePiece>();
            //  칸 폭은 줄인 만큼 좁게(축척은 x·y 같이). 높이 곡선은 그대로 x의 함수다(칸 폭과 무관).
            double w = BgTrestleBayWidth * (double)scale;
            int k0 = (int)Math.Floor(lo / w), k1 = (int)Math.Ceiling(hi / w);
            for (int k = k0; k < k1; k++)
            {
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

        /// <summary>
        /// 범위 [from, to]에 <b>걸친</b> 조각(겹치지만 안에 다 들지 않는 것)은 렌더러를 켠 채 두므로, 바닥·천장 그림이 그 조각
        /// 끝까지 이어 덮도록 범위를 넓힌다. 범위 밖 조각·끝만 맞닿은 조각은 범위를 바꾸지 않는다.
        /// </summary>
        public static (float from, float to) CoverRange(IEnumerable<(float x0, float x1)> spans, float from, float to)
        {
            foreach (var (x0, x1) in spans)
            {
                if (!SpanOverlaps(x0, x1, from, to) || SpanInside(x0, x1, from, to)) { continue; }
                from = Math.Min(from, x0);
                to = Math.Max(to, x1);
            }
            return (from, to);
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
