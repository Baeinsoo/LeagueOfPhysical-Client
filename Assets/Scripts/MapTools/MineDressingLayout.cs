using System;
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>광산 옷 부품 종류. 이름은 Art/Models/Mine의 FBX 이름과 같다(실루엣 둘은 SilhouetteStrip 하나를 재질만 바꿔 쓴다).</summary>
    public enum MinePartKind
    {
        TrestleBay, CeilingRock, Beam,
        BgTrestleBay, MineCart, BgFrame, Ladder, Walkway, Lantern, CaveSilhouette, CanyonSilhouette,
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
    /// 맞춘 <b>현</b>이다 — 가운데 y = 양끝 평균, 각도 = 현의 기울기, 길이 = 현의 길이. 선이 직선인 곳(보기 구간 전부)에선
    /// 가운데도 선 위에 있고, 꺾이는 칸에선 양끝만 정확하다(칸 사이 이음매가 벌어지지 않는 쪽을 골랐다).</para>
    ///
    /// <para>부품 크기·원점은 Task 3 보고(<c>.superpowers/sdd/2026-10-07-flappy-mine-look-slice1/task-3-report.md</c>)의 유니티 실측값이다.</para>
    /// </summary>
    public static class MineDressingLayout
    {
        //  ── 구역(스펙 §3) ──
        /// <summary>노을 바깥의 끝 = 굴 입구 시작. 여기 앞은 천장 대신 들보, 협곡 실루엣.</summary>
        public const float OutsideEnd = 24f;
        /// <summary>굴 입구 끝 = 굴 안 시작.</summary>
        public const float CaveInside = 34f;
        /// <summary>배경 층은 카메라 시야를 덮도록 범위 앞뒤로 이만큼 더 깐다.</summary>
        public const float BackgroundMargin = 15f;

        //  ── 깊이(Global Constraints) ──
        public const float HazeZ = 2.2f, LanternZ = 4.5f, MidZ = 6f, FarZ = 11f, SilhouetteNearZ = 13f, SilhouetteFarZ = 15f;

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
        /// <summary>SilhouetteStrip x ±10 — 양끝 높이가 같아 20 m마다 이어진다.</summary>
        public const float SilhouetteWidth = 20f;
        /// <summary>BgFrame x ±3.3(윗면 원점), Walkway x ±3.0(윗면 원점), Ladder 위 끝 원점.</summary>
        public const float BgFrameWidth = 6.6f, WalkwayWidth = 6f, LadderWidth = 0.82f, LanternWidth = 0.6f, MineCartWidth = 1.95f;

        /// <summary>굴 입구 아치(CaveMouth, 원점 = 통로 가운데, x ±5) — 24~34를 덮는다.</summary>
        public const float CaveMouthX = (OutsideEnd + CaveInside) / 2f;

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

            public BackgroundDensity(float frameSpacing, float ladderChance, float lanternSpacing, int cartCount)
            {
                FrameSpacing = frameSpacing; LadderChance = ladderChance; LanternSpacing = lanternSpacing; CartCount = cartCount;
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

        // ── 바닥·천장 ──

        /// <summary>
        /// 바닥 비계(TrestleBay + 같은 자리 RailSpan). [from, to]를 2.4 m에 가장 가까운 같은 폭으로 나눠 빈틈없이 깐다 —
        /// 칸 폭이 2.4에서 조금 어긋나므로 x 축척 = <see cref="MinePiece.ScaleX"/>(현 길이 / 2.4). Y = 데크 윗면(원점).
        /// </summary>
        public static List<MinePiece> TrestleBays(MineCourse c, float from, float to)
        {
            var list = new List<MinePiece>();
            foreach (var (x0, x1) in Tiles(from, to, TrestleBayWidth))
            {
                list.Add(Chord(MinePartKind.TrestleBay, x0, x1, x => c.CenterAtD(x) - c.HalfAtD(x), 0f, TrestleBayWidth));
            }
            return list;
        }

        /// <summary>
        /// 천장 조각. [from, to]를 2 m 근처 같은 폭으로 나눈다. 가운데가 노을 바깥(x &lt; 24)이면 <see cref="MinePartKind.Beam"/>
        /// (1 m 단위), 아니면 <see cref="MinePartKind.CeilingRock"/>(2 m 단위). Y = 아랫면 = 천장선.
        /// </summary>
        public static List<MinePiece> CeilingPieces(MineCourse c, float from, float to)
        {
            var list = new List<MinePiece>();
            foreach (var (x0, x1) in Tiles(from, to, CeilingPieceWidth))
            {
                bool beam = (x0 + x1) / 2.0 < OutsideEnd;
                list.Add(Chord(beam ? MinePartKind.Beam : MinePartKind.CeilingRock, x0, x1, x => c.CenterAtD(x) + c.HalfAtD(x), 0f,
                               beam ? BeamUnit : CeilingRockUnit));
            }
            return list;
        }

        /// <summary>굴 입구 아치(CaveMouth) 원점 = (29, 그 자리 통로 가운데).</summary>
        public static (float x, float y) CaveMouthAt(MineCourse c) => (CaveMouthX, c.CenterAt(CaveMouthX));

        // ── 배경 ──

        /// <summary>
        /// 배경 층 전부(z &gt; 2.2). [from − 15, to + 15]를 덮는다. 같은 시드면 같은 결과.
        /// <list type="bullet">
        /// <item>실루엣(z 13 / 15): x = 24를 이음매로 20 m 띠. 가운데가 24 앞이면 협곡(바닥 쪽만), 뒤면 굴벽(바닥 + 뒤집은 천장 쪽).
        /// 높이(y 축척)만 시드로 흔든다.</item>
        /// <item>먼 비계(z 11): 3 m 칸(x = 3k ~ 3k+3), 데크 윗면 = 가운데선 −1.0 + 2.5·sin(0.15·x)의 현. 구역 구분 없이(바깥은 협곡 다리).</item>
        /// <item>광차: [from, to] 안 먼 비계 칸 중 시드로 고른 서로 다른 칸, 레일 윗면 위.</item>
        /// <item>가운데 층(z 6)·랜턴(z 4.5): 굴(x ≥ 24)에만. 틀은 <see cref="BackgroundDensity.FrameSpacing"/>마다, 틀마다 발판 하나,
        /// 사다리는 확률. 랜턴은 <see cref="BackgroundDensity.LanternSpacing"/>마다 ±1 m.</item>
        /// </list>
        /// </summary>
        public static List<MinePiece> Background(MineCourse c, float from, float to, int seed, BackgroundDensity d)
        {
            var list = new List<MinePiece>();
            var rng = new Lcg(seed);
            double lo = from - BackgroundMargin, hi = to + BackgroundMargin;

            AddSilhouettes(c, lo, hi, rng, list);
            var far = AddFarTrestles(c, lo, hi, list);
            AddCarts(far, from, to, d.CartCount, rng, list);
            AddMidLayer(c, lo, hi, d, rng, list);
            return list;
        }

        //  실루엣 층 하나: (z, 층, 바닥 쪽 기준선 높이, 천장 쪽 기준선 높이(NaN = 없음), y 축척 범위). 기준선은 통로 가운데 기준.
        //  띠의 들쭉날쭉한 윗선은 기준선 위 1.4~5.3(Task 3) — 시안 silhouette(15, −2.0, 4.0)·(15, 6.5, 3.0, up)이 굴의 먼 층이다.
        static readonly (float z, int layer, float floorBase, float ceilBase, float sMin, float sMax)[] CaveLayers =
        {
            (SilhouetteNearZ, 0, -6.0f, 10.0f, 0.8f, 1.2f),
            (SilhouetteFarZ, 1, -3.4f, 7.9f, 0.8f, 1.2f),
        };
        //  협곡(노을 바깥): 시안 silhouette(20, −7, 6)·(26, −4, 9) — 바닥 쪽만, 높이 크게.
        static readonly (float z, int layer, float floorBase, float ceilBase, float sMin, float sMax)[] CanyonLayers =
        {
            (SilhouetteNearZ, 0, -8.5f, float.NaN, 1.2f, 1.6f),
            (SilhouetteFarZ, 1, -7.0f, float.NaN, 1.7f, 2.3f),
        };

        static void AddSilhouettes(MineCourse c, double lo, double hi, Lcg rng, List<MinePiece> list)
        {
            //  띠 가운데 = 24 ± 10 + 20k — 협곡/굴벽이 정확히 24에서 갈린다.
            double w = SilhouetteWidth;
            int k0 = (int)Math.Floor((lo - OutsideEnd) / w), k1 = (int)Math.Ceiling((hi - OutsideEnd) / w);
            for (int k = k0; k < k1; k++)
            {
                double x = OutsideEnd + (k + 0.5) * w;
                bool canyon = x < OutsideEnd;
                var kind = canyon ? MinePartKind.CanyonSilhouette : MinePartKind.CaveSilhouette;
                float center = c.CenterAt((float)x);
                foreach (var l in canyon ? CanyonLayers : CaveLayers)
                {
                    float s = rng.Range(l.sMin, l.sMax);
                    list.Add(new MinePiece(kind, (float)x, center + l.floorBase, l.z, 0f, (float)w, (float)w, 1f, s, false, l.layer));
                    if (!float.IsNaN(l.ceilBase))
                    {
                        float sc = rng.Range(l.sMin, l.sMax);
                        list.Add(new MinePiece(kind, (float)x, center + l.ceilBase, l.z, 0f, (float)w, (float)w, 1f, sc, true, l.layer));
                    }
                }
            }
        }

        static List<MinePiece> AddFarTrestles(MineCourse c, double lo, double hi, List<MinePiece> list)
        {
            var far = new List<MinePiece>();
            double w = BgTrestleBayWidth;
            int k0 = (int)Math.Floor(lo / w), k1 = (int)Math.Ceiling(hi / w);
            for (int k = k0; k < k1; k++)
            {
                var p = Chord(MinePartKind.BgTrestleBay, k * w, (k + 1) * w,
                              x => c.CenterAtD(x) + FarBase + FarAmp * Math.Sin(x / w * FarPhasePerBay), FarZ, BgTrestleBayWidth);
                far.Add(p);
                list.Add(p);
            }
            return far;
        }

        static void AddCarts(List<MinePiece> far, float from, float to, int count, Lcg rng, List<MinePiece> list)
        {
            var pool = far.FindAll(p => p.X >= from && p.X <= to);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int j = (int)(rng.Next() * pool.Count);
                var bay = pool[j];
                pool.RemoveAt(j);
                list.Add(new MinePiece(MinePartKind.MineCart, bay.X, bay.Y + RailTop, bay.Z, bay.AngleDegrees, MineCartWidth, MineCartWidth, 1f, 1f));
            }
        }

        static void AddMidLayer(MineCourse c, double lo, double hi, BackgroundDensity d, Lcg rng, List<MinePiece> list)
        {
            //  격자는 굴 입구(24)에서 시작한다 — 범위를 늘려도 이미 있던 틀이 움직이지 않게.
            double start = Math.Max(lo, OutsideEnd);
            if (d.FrameSpacing > 0f)
            {
                for (int k = (int)Math.Ceiling((start - OutsideEnd) / d.FrameSpacing - 0.5); ; k++)
                {
                    double x = OutsideEnd + (k + 0.5) * d.FrameSpacing;
                    if (x > hi) { break; }
                    if (x < start) { continue; }
                    float fx = (float)x, center = c.CenterAt(fx);
                    list.Add(new MinePiece(MinePartKind.BgFrame, fx, center + FrameTop, MidZ, 0f, BgFrameWidth, BgFrameWidth, 1f, 1f));
                    float walk = WalkLow + rng.Range(0f, WalkRange);
                    list.Add(new MinePiece(MinePartKind.Walkway, fx, center + walk, MidZ, 0f, WalkwayWidth, WalkwayWidth, 1f, 1f));
                    //  확률은 항상 뽑는다 — 사다리 비율을 바꿔도 뒤 소품의 난수가 밀리지 않게.
                    if (rng.Next() < d.LadderChance)
                    {
                        float lx = fx + LadderOffsetX;
                        list.Add(new MinePiece(MinePartKind.Ladder, lx, c.CenterAt(lx) + LadderTop, MidZ + LadderDz, 0f, LadderWidth, LadderWidth, 1f, 1f));
                    }
                }
            }

            if (d.LanternSpacing > 0f)
            {
                for (int k = (int)Math.Ceiling((start - OutsideEnd) / d.LanternSpacing - 0.5); ; k++)
                {
                    double x = OutsideEnd + (k + 0.5) * d.LanternSpacing;
                    if (x > hi) { break; }
                    float jitter = rng.Range(-LanternJitter, LanternJitter);
                    if (x < start) { continue; }
                    float lx = (float)Math.Max(OutsideEnd, x + jitter);
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

        /// <summary>[from, to]를 unit에 가장 가까운 같은 폭 n칸으로. 칸 경계는 double로 셈해 오차가 쌓이지 않는다.</summary>
        static IEnumerable<(double x0, double x1)> Tiles(float from, float to, float unit)
        {
            double len = (double)to - from;
            if (len <= 1e-6) { yield break; }
            int n = Math.Max(1, (int)Math.Round(len / unit));
            for (int i = 0; i < n; i++) { yield return (from + len * i / n, from + len * (i + 1) / n); }
        }

        /// <summary>선 f 위의 두 점 (x0, f(x0))·(x1, f(x1))을 잇는 현에 놓인 조각. 축척 x = 현 길이 / 부품 단위.</summary>
        static MinePiece Chord(MinePartKind kind, double x0, double x1, Func<double, double> f, float z, float unit)
        {
            double y0 = f(x0), y1 = f(x1), w = x1 - x0, dy = y1 - y0;
            double len = Math.Sqrt(w * w + dy * dy);
            return new MinePiece(kind, (float)((x0 + x1) / 2), (float)((y0 + y1) / 2), z, (float)(Math.Atan2(dy, w) * 180.0 / Math.PI),
                                 (float)w, (float)len, (float)(len / unit), 1f);
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
