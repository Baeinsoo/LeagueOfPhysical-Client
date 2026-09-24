using System;
using System.Collections.Generic;
using GameFramework.Rng;

namespace LOP.MapTools
{
    public enum TerrainKind { Flat, Valley, Hill, StepDown, StepUp }

    /// <summary>한 구간의 지형 손잡이. 구간이 갈수록 깊고 가팔라진다(spec §3).</summary>
    public readonly struct SectionTerrain
    {
        public readonly float ValleyDepth;
        public readonly float HillHeight;
        /// <summary>오르막 기울기. 탭 빈도가 한계를 정한다 — 1.5면 초당 약 3.5탭.</summary>
        public readonly float RiseSlope;
        /// <summary>계단 높이. 0이면 이 구간엔 계단이 없다.</summary>
        public readonly float StepHeight;
        public readonly bool ValleyShortcut;

        public SectionTerrain(float valleyDepth, float hillHeight, float riseSlope, float stepHeight,
                              bool valleyShortcut)
        {
            ValleyDepth = valleyDepth;
            HillHeight = hillHeight;
            RiseSlope = riseSlope;
            StepHeight = stepHeight;
            ValleyShortcut = valleyShortcut;
        }
    }

    /// <summary>회랑 중심이 평평한 x 범위. 관문은 여기에만 선다.</summary>
    public readonly struct FlatSpan
    {
        public readonly float From, To;
        public FlatSpan(float from, float to) { From = from; To = to; }
        public float Length => To - From;
    }

    /// <summary>
    /// U자를 수평으로 가로지르는 지름길. <see cref="X0"/>·<see cref="X1"/>은 천장선이 지름길 윗면을
    /// 지나는 곳(입구·출구)이고, <see cref="Tongue"/>은 지름길과 계곡 사이에 남는 덩어리(혀)다.
    /// </summary>
    public readonly struct ShortcutRect
    {
        public readonly float X0, X1, Y0, Y1;
        /// <summary>(x, y) 네 쌍, 반시계: 윗변 왼쪽 → 바닥 왼쪽 → 바닥 오른쪽 → 윗변 오른쪽.</summary>
        public readonly float[] Tongue;

        public ShortcutRect(float x0, float x1, float y0, float y1, float[] tongue)
        {
            X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; Tongue = tongue;
        }

        public float CenterY => (Y0 + Y1) * 0.5f;
        public float Length => X1 - X0;

        /// <summary>씬 표시(빈 GameObject의 위치·크기)에서 되살린다. 혀는 검사에 필요 없어 비운다.</summary>
        public static ShortcutRect FromCenterSize(float cx, float cy, float w, float h)
            => new ShortcutRect(cx - w * 0.5f, cx + w * 0.5f, cy - h * 0.5f, cy + h * 0.5f, null);
    }

    /// <summary>경사 조각 하나 — 두 x 사이를 곧은 선으로 잇는다. Lift는 회랑 중심 높이.</summary>
    public readonly struct RampPiece
    {
        public readonly float X0, Lift0, X1, Lift1;
        public RampPiece(float x0, float lift0, float x1, float lift1)
        {
            X0 = x0; Lift0 = lift0; X1 = x1; Lift1 = lift1;
        }
    }

    /// <summary>
    /// 코스의 회랑 중심 높이 = <b>꺾은선</b>(도로 설계의 종단면, vertical profile). 조각(평지·계곡·언덕·
    /// 계단)을 이어 붙여 만든다. 꺾은선이라 바닥·천장을 조각 하나 = 곧은 경사 하나로 정확히 덮을 수
    /// 있다 — 곡선을 곧은 조각으로 덮을 때 생기던 수 cm 오차가 없다.
    /// </summary>
    public sealed class CourseProfile
    {
        private readonly float[] xs;
        private readonly float[] ys;
        private readonly List<FlatSpan> flats;
        private readonly List<ShortcutRect> shortcuts;

        internal CourseProfile(float[] xs, float[] ys, List<FlatSpan> flats, List<ShortcutRect> shortcuts)
        {
            this.xs = xs;
            this.ys = ys;
            this.flats = flats;
            this.shortcuts = shortcuts;
            MinY = float.MaxValue;
            MaxY = float.MinValue;
            foreach (float y in ys)
            {
                MinY = Math.Min(MinY, y);
                MaxY = Math.Max(MaxY, y);
            }
        }

        public int VertexCount => xs.Length;
        public float X(int i) => xs[i];
        public float Y(int i) => ys[i];
        public IReadOnlyList<FlatSpan> Flats => flats;
        public IReadOnlyList<ShortcutRect> Shortcuts => shortcuts;
        public float MinY { get; }
        public float MaxY { get; }

        public float CenterAt(float x)
        {
            if (x <= xs[0]) { return ys[0]; }
            int last = xs.Length - 1;
            if (x >= xs[last]) { return ys[last]; }
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (xs[mid] <= x) { lo = mid; } else { hi = mid; }
            }
            float t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + (ys[hi] - ys[lo]) * t;
        }

        /// <summary>이 x에 관문을 세워도 되나 — 평지 안쪽으로 <paramref name="margin"/>만큼 들어와 있어야 한다.</summary>
        public bool GateAllowedAt(float x, float margin)
        {
            foreach (FlatSpan f in flats)
            {
                if (x >= f.From + margin && x <= f.To - margin) { return true; }
            }
            return false;
        }
    }

    public static class CourseProfileRule
    {
        /// <summary>내리막 기울기(≈68°). 최대 낙하 30m/s로 따라갈 수 있는 한계 4.4보다 한참 안쪽.</summary>
        public const float DropSlope = 2.5f;
        /// <summary>U 바닥 길이. 관문을 끝에서 <see cref="GateMargin"/> 안쪽에 두면 보통 하나가 선다.</summary>
        public const float ValleyBottom = 16f;
        public const float HillTop = 12f;
        /// <summary>관문을 평지 끝에서 이만큼 안쪽에만 둔다 — 벽을 내려오자마자 창이 있으면 못 멈춘다.</summary>
        public const float GateMargin = 2.5f;
        /// <summary>
        /// 지름길과 계곡 사이 혀의 최소 두께. 이보다 얇으면 두 길이 사실상 붙는다.
        /// 빌더가 파이프 윗끝을 천장 속으로 1m 박아 넣는데, 지름길 아래 계곡에선 그 천장이 곧 혀다 —
        /// 그래서 이 값을 그 1m보다 줄이면 안 된다 — 1m면 파이프 끝이 지름길 바닥에 딱 닿고, 더 얇으면
        /// 파이프가 혀를 뚫고 지름길로 삐져나온다. 파이프를 더 깊이 박게 바꾸면 이 값도 같이 올릴 것.
        /// </summary>
        public const float MinTongue = 1f;

        /// <summary>구간 1(배우기) · 2 · 3. spec §3 표 그대로.</summary>
        public static readonly SectionTerrain[] Sections =
        {
            new SectionTerrain(valleyDepth: 15f, hillHeight: 12f, riseSlope: 1.0f, stepHeight: 0f, valleyShortcut: false),
            new SectionTerrain(valleyDepth: 30f, hillHeight: 15f, riseSlope: 1.3f, stepHeight: 10f, valleyShortcut: true),
            new SectionTerrain(valleyDepth: 40f, hillHeight: 20f, riseSlope: 1.5f, stepHeight: 10f, valleyShortcut: true),
        };

        public static float ValleyLength(float depth, float rise) => depth / DropSlope + ValleyBottom + depth / rise;
        public static float HillLength(float height, float rise) => height / rise + HillTop + height / DropSlope;
        public static float StepLength(float height, bool down, float rise) => down ? height / DropSlope : height / rise;

        /// <param name="leadIn">출발선 앞 평지(스폰 뒤를 덮는 여유).</param>
        /// <param name="tail">결승선 뒤 평지.</param>
        public static CourseProfile Compose(float startX, float length, float spacing, float corridorHalf,
                                            float window, ulong seed, float leadIn, float tail)
        {
            var rng = new DeterministicRandom(seed);
            var xs = new List<float> { startX - leadIn };
            var ys = new List<float> { 0f };
            var flats = new List<FlatSpan>();
            var shortcuts = new List<ShortcutRect>();

            float x = startX;
            float y = 0f;
            float flatStart = startX - leadIn;
            float sectionLen = length / Sections.Length;

            for (int s = 0; s < Sections.Length; s++)
            {
                SectionTerrain t = Sections[s];
                var kinds = new List<TerrainKind> { TerrainKind.Valley, TerrainKind.Hill };
                //  계단 방향은 구간 시작에 정한다 — 계곡·언덕은 제자리로 돌아오므로 구간 안에서
                //  기준 높이가 바뀌는 것은 계단뿐이다. 0에서 멀어지지 않게 되돌리는 쪽으로 간다.
                bool stepDown = y > 1e-3f || (Math.Abs(y) <= 1e-3f && rng.Range(0, 2) == 0);
                if (t.StepHeight > 0f)
                {
                    kinds.Add(stepDown ? TerrainKind.StepDown : TerrainKind.StepUp);
                }
                for (int i = kinds.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
                }

                float piecesLen = 0f;
                foreach (TerrainKind k in kinds) { piecesLen += PieceLength(k, t); }

                int gaps = kinds.Count + 1;
                var mins = new float[gaps];
                for (int g = 0; g < gaps; g++) { mins[g] = spacing; }
                if (s == 0) { mins[0] = spacing * 3f; }                    // 스폰 앞 평지
                if (s == Sections.Length - 1) { mins[gaps - 1] = spacing * 2f; }  // 결승선 앞 평지
                float minSum = 0f;
                foreach (float m in mins) { minSum += m; }
                float free = sectionLen - piecesLen - minSum;
                if (free < 0f)
                {
                    throw new InvalidOperationException(
                        $"구간 {s + 1}에 조각이 안 들어간다 (조각 {piecesLen:F1}m + 최소 평지 {minSum:F1}m > {sectionLen:F1}m)");
                }
                var weights = new float[gaps];
                float weightSum = 0f;
                for (int g = 0; g < gaps; g++) { weights[g] = rng.Range(0f, 1f) + 0.1f; weightSum += weights[g]; }

                for (int g = 0; g < gaps; g++)
                {
                    x += mins[g] + free * weights[g] / weightSum;
                    if (g == gaps - 1) { break; }

                    //  평지가 여기서 끝나고 조각이 시작된다.
                    flats.Add(new FlatSpan(flatStart, x));
                    xs.Add(x); ys.Add(y);
                    TerrainKind kind = kinds[g];
                    switch (kind)
                    {
                        case TerrainKind.Valley:
                        {
                            float d = t.ValleyDepth;
                            float x0 = x;
                            float bottom0 = x0 + d / DropSlope;
                            float bottom1 = bottom0 + ValleyBottom;
                            xs.Add(bottom0); ys.Add(y - d);
                            xs.Add(bottom1); ys.Add(y - d);
                            flats.Add(new FlatSpan(bottom0, bottom1));
                            x = bottom1 + d / t.RiseSlope;
                            xs.Add(x); ys.Add(y);
                            if (t.ValleyShortcut)
                            {
                                shortcuts.Add(ValleyShortcut(x0, y, d, t.RiseSlope, corridorHalf, window));
                            }
                            break;
                        }
                        case TerrainKind.Hill:
                        {
                            float h = t.HillHeight;
                            float top0 = x + h / t.RiseSlope;
                            float top1 = top0 + HillTop;
                            xs.Add(top0); ys.Add(y + h);
                            xs.Add(top1); ys.Add(y + h);
                            flats.Add(new FlatSpan(top0, top1));
                            x = top1 + h / DropSlope;
                            xs.Add(x); ys.Add(y);
                            break;
                        }
                        default:
                        {
                            bool down = kind == TerrainKind.StepDown;
                            x += StepLength(t.StepHeight, down, t.RiseSlope);
                            y += down ? -t.StepHeight : t.StepHeight;
                            xs.Add(x); ys.Add(y);
                            break;
                        }
                    }
                    flatStart = x;
                }
            }

            float end = startX + length + tail;
            flats.Add(new FlatSpan(flatStart, end));
            xs.Add(end); ys.Add(y);
            return new CourseProfile(xs.ToArray(), ys.ToArray(), flats, shortcuts);
        }

        static float PieceLength(TerrainKind kind, SectionTerrain t)
        {
            switch (kind)
            {
                case TerrainKind.Valley: return ValleyLength(t.ValleyDepth, t.RiseSlope);
                case TerrainKind.Hill: return HillLength(t.HillHeight, t.RiseSlope);
                case TerrainKind.StepDown: return StepLength(t.StepHeight, true, t.RiseSlope);
                default: return StepLength(t.StepHeight, false, t.RiseSlope);
            }
        }

        /// <summary>
        /// 계곡의 깊이 절반 높이에 수평 지름길을 뚫는다. 입구는 내리막 벽 중간이라 먼저 절반을 급강하해야
        /// 들어갈 수 있다(spec §4). 혀(지름길과 계곡 사이)가 <see cref="MinTongue"/>보다 얇으면 던진다.
        /// </summary>
        /// <param name="x0">계곡이 시작하는 x(평지 끝).</param>
        public static ShortcutRect ValleyShortcut(float x0, float baseY, float depth, float riseSlope,
                                                  float corridorHalf, float window)
        {
            float center = baseY - depth * 0.5f;
            float y0 = center - window * 0.5f;
            float y1 = center + window * 0.5f;
            float topCeiling = baseY + corridorHalf;
            float bottomCeiling = baseY - depth + corridorHalf;
            if (y0 - bottomCeiling < MinTongue)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth,
                    $"깊이 {depth}m면 지름길 아래 혀가 {y0 - bottomCeiling:F2}m다 (최소 {MinTongue}m)");
            }
            float bottom0 = x0 + depth / DropSlope;
            float bottom1 = bottom0 + ValleyBottom;
            //  내리막 천장선 y = topCeiling − DropSlope·(x − x0), 오르막 천장선 y = bottomCeiling + rise·(x − bottom1).
            float entry = x0 + (topCeiling - y1) / DropSlope;
            float exit = bottom1 + (y1 - bottomCeiling) / riseSlope;
            float tongueLeft = x0 + (topCeiling - y0) / DropSlope;
            float tongueRight = bottom1 + (y0 - bottomCeiling) / riseSlope;
            var tongue = new[]
            {
                tongueLeft, y0,
                bottom0, bottomCeiling,
                bottom1, bottomCeiling,
                tongueRight, y0,
            };
            return new ShortcutRect(entry, exit, y0, y1, tongue);
        }

        /// <summary>바닥 경사 조각. 꺾은선의 모든 꼭짓점과 <paramref name="splitXs"/>(구간 경계)에서 끊는다.</summary>
        public static List<RampPiece> FloorPieces(CourseProfile p, IReadOnlyList<float> splitXs)
            => Pieces(p, splitXs, cutShortcuts: false);

        /// <summary>천장 경사 조각. 바닥과 같되 지름길 입구~출구는 도려낸다 — 그 자리는 빌더가 위쪽 상자와 혀로 채운다.</summary>
        public static List<RampPiece> CeilingPieces(CourseProfile p, IReadOnlyList<float> splitXs)
            => Pieces(p, splitXs, cutShortcuts: true);

        static List<RampPiece> Pieces(CourseProfile p, IReadOnlyList<float> splitXs, bool cutShortcuts)
        {
            float first = p.X(0);
            float last = p.X(p.VertexCount - 1);
            var cuts = new List<float>();
            for (int i = 0; i < p.VertexCount; i++) { cuts.Add(p.X(i)); }
            foreach (float s in splitXs) { if (s > first && s < last) { cuts.Add(s); } }
            if (cutShortcuts)
            {
                foreach (ShortcutRect r in p.Shortcuts) { cuts.Add(r.X0); cuts.Add(r.X1); }
            }
            cuts.Sort();

            var pieces = new List<RampPiece>();
            for (int i = 1; i < cuts.Count; i++)
            {
                float a = cuts[i - 1], b = cuts[i];
                if (b - a < 1e-4f) { continue; }
                if (cutShortcuts)
                {
                    float mid = (a + b) * 0.5f;
                    bool inside = false;
                    foreach (ShortcutRect r in p.Shortcuts) { inside |= mid > r.X0 && mid < r.X1; }
                    if (inside) { continue; }
                }
                pieces.Add(new RampPiece(a, p.CenterAt(a), b, p.CenterAt(b)));
            }
            return pieces;
        }
    }
}
