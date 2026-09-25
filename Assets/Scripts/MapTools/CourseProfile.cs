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
        public readonly ShortcutEntrance Entrance;

        public SectionTerrain(float valleyDepth, float hillHeight, float riseSlope, float stepHeight,
                              bool valleyShortcut, ShortcutEntrance entrance = default)
        {
            ValleyDepth = valleyDepth;
            HillHeight = hillHeight;
            RiseSlope = riseSlope;
            StepHeight = stepHeight;
            ValleyShortcut = valleyShortcut;
            Entrance = entrance;
        }
    }

    /// <summary>
    /// 날갯짓 한 번이 그리는 호 — <b>틱 단위로 뗀</b> 궤적. 커널은 한 틱에 속도를 먼저 바꾸고 그 속도로
    /// 움직이므로, 날갯짓 틱부터 t초 뒤 높이는 연속 포물선이 아니라 (v + g·dt/2)·t − g·t²/2 다.
    /// 연속식으로 굴을 파면 한 호 끝에서 0.37m 어긋나 제때 쳐도 박는다(spec §13).
    /// </summary>
    public readonly struct FlapArc
    {
        public readonly float FlapImpulse, Gravity, ForwardSpeed, TickSeconds;

        public FlapArc(float flapImpulse, float gravity, float forwardSpeed, float tickSeconds)
        {
            FlapImpulse = flapImpulse; Gravity = gravity; ForwardSpeed = forwardSpeed; TickSeconds = tickSeconds;
        }

        public float EffectiveImpulse => FlapImpulse + Gravity * TickSeconds * 0.5f;
        /// <summary>한 호의 틱 수 — 친 높이로 돌아오기 직전까지(내림). 그래서 호마다 조금씩 오른다.</summary>
        public int TicksPerArc => (int)Math.Floor(2f * EffectiveImpulse / Gravity / TickSeconds);
        public float Span => TicksPerArc * ForwardSpeed * TickSeconds;
        public float RisePerArc => HeightAt(Span);
        public float Apex => EffectiveImpulse * EffectiveImpulse / (2f * Gravity);

        /// <summary>친 자리에서 앞으로 <paramref name="dx"/>m 갔을 때 친 높이보다 얼마나 위인가.</summary>
        public float HeightAt(float dx)
        {
            float t = dx / ForwardSpeed;
            return EffectiveImpulse * t - 0.5f * Gravity * t * t;
        }
    }

    /// <summary>지름길 입구 난이도. 굴이 좁을수록·호가 많을수록 박자가 빡빡하고, 놓치면 호마다 한 번씩 부딪힌다.</summary>
    public readonly struct ShortcutEntrance
    {
        /// <summary>이어진 호(날갯짓 박자) 수. 0이면 굴이 없다(씬 표시에서 되살린 값).</summary>
        public readonly int Arcs;
        /// <summary>굴의 세로 폭.</summary>
        public readonly float Thickness;
        /// <summary>입구 밑으로 내려온 턱 길이. 낮게 빗나간 새가 여기에 부딪혀 계곡으로 떨어진다.</summary>
        public readonly float Lip;

        public ShortcutEntrance(int arcs, float thickness, float lip)
        {
            Arcs = arcs; Thickness = thickness; Lip = lip;
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
    /// U자 계곡을 가로지르는 지름길. 앞은 날갯짓 호 모양으로 판 좁은 굴(<see cref="X0"/>~<see cref="ChannelEnd"/>),
    /// 뒤는 곧은 수평 길(~<see cref="X1"/>, 높이 <see cref="Y0"/>~<see cref="Y1"/>)이다. <see cref="X0"/>은 내리막
    /// 천장이 굴 윗면을 지나는 곳(입구), <see cref="X1"/>은 오르막 천장이 곧은 길 윗면을 지나는 곳(출구).
    /// </summary>
    public readonly struct ShortcutRect
    {
        public readonly float X0, ChannelEnd, X1, Y0, Y1;
        public readonly ShortcutEntrance Entrance;
        public readonly FlapArc Arc;
        /// <summary>혀(지름길과 계곡 사이 덩어리)가 끝나는 x — 오르막 천장이 곧은 길 바닥을 지나는 곳.</summary>
        public readonly float TongueEnd;
        /// <summary>혀 아랫면을 그리는 데 쓰는 계곡 모양: 바닥 시작·끝 x, 바닥 위 천장 높이, 오르막 기울기.</summary>
        public readonly float ValleyBottom0, ValleyBottom1, BottomCeiling, RiseSlope;

        public ShortcutRect(float x0, float channelEnd, float x1, float y0, float y1,
                            ShortcutEntrance entrance, FlapArc arc, float tongueEnd,
                            float valleyBottom0, float valleyBottom1, float bottomCeiling, float riseSlope)
        {
            X0 = x0; ChannelEnd = channelEnd; X1 = x1; Y0 = y0; Y1 = y1;
            Entrance = entrance; Arc = arc; TongueEnd = tongueEnd;
            ValleyBottom0 = valleyBottom0; ValleyBottom1 = valleyBottom1;
            BottomCeiling = bottomCeiling; RiseSlope = riseSlope;
        }

        public float CenterY => (Y0 + Y1) * 0.5f;
        public float Length => X1 - X0;
        public float LipBottom => CenterY - Entrance.Thickness * 0.5f - Entrance.Lip;

        /// <summary>
        /// 굴 가운데선 높이. 입구에서 <see cref="CenterY"/>로 시작해, 호마다 한 번 친 새가 지나는 틱 궤적을
        /// 그대로 따른다. 굴 밖은 양 끝 값에 고정한다.
        /// </summary>
        public float ChannelCenterAt(float x)
        {
            if (Entrance.Arcs < 1 || x <= X0) { return CenterY; }
            float span = Arc.Span;
            float dx = Math.Min(x - X0, Entrance.Arcs * span);
            int k = Math.Min((int)(dx / span), Entrance.Arcs - 1);
            return CenterY + k * Arc.RisePerArc + Arc.HeightAt(dx - k * span);
        }

        /// <summary>씬 표시(빈 GameObject의 위치·크기)에서 되살린다. 검사기는 곧은 길 띠만 쓰므로 굴은 비운다.</summary>
        public static ShortcutRect FromCenterSize(float cx, float cy, float w, float h)
        {
            float x0 = cx - w * 0.5f, x1 = cx + w * 0.5f;
            return new ShortcutRect(x0, x0, x1, cy - h * 0.5f, cy + h * 0.5f,
                                    default, default, x1, x0, x1, cy - h * 0.5f, 0f);
        }
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
        /// <summary>굴 끝에서 출구까지 곧은 길의 최소 길이 — 호를 늘리다 굴이 출구를 넘는 실수를 막는다.</summary>
        public const float MinStraight = 2f;
        /// <summary>입구 턱 밑으로 계곡 새가 지나갈 최소 틈.</summary>
        public const float MinValleyGap = 6f;

        /// <summary>구간 1(배우기) · 2 · 3. spec §3 표 그대로.</summary>
        public static readonly SectionTerrain[] Sections =
        {
            new SectionTerrain(valleyDepth: 15f, hillHeight: 12f, riseSlope: 1.0f, stepHeight: 0f, valleyShortcut: false),
            new SectionTerrain(valleyDepth: 30f, hillHeight: 15f, riseSlope: 1.3f, stepHeight: 10f, valleyShortcut: true,
                               entrance: new ShortcutEntrance(arcs: 1, thickness: 4.35f, lip: 4f)),
            new SectionTerrain(valleyDepth: 40f, hillHeight: 20f, riseSlope: 1.5f, stepHeight: 10f, valleyShortcut: true,
                               entrance: new ShortcutEntrance(arcs: 3, thickness: 3.6f, lip: 6f)),
        };

        public static float ValleyLength(float depth, float rise) => depth / DropSlope + ValleyBottom + depth / rise;
        public static float HillLength(float height, float rise) => height / rise + HillTop + height / DropSlope;
        public static float StepLength(float height, bool down, float rise) => down ? height / DropSlope : height / rise;

        /// <param name="leadIn">출발선 앞 평지(스폰 뒤를 덮는 여유).</param>
        /// <param name="tail">결승선 뒤 평지.</param>
        /// <param name="arc">입구 굴을 파는 날갯짓 호 — FlappyConfig에서 만든다.</param>
        public static CourseProfile Compose(float startX, float length, float spacing, float corridorHalf,
                                            float window, ulong seed, float leadIn, float tail, FlapArc arc)
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
                                shortcuts.Add(ValleyShortcut(x0, y, d, t.RiseSlope, corridorHalf, window, t.Entrance, arc));
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
        /// 계곡의 깊이 절반 높이에 지름길을 뚫는다. 입구는 내리막 벽 중간의 좁은 굴(날갯짓 호 모양)이고,
        /// 굴이 끝나면 곧은 수평 길이 출구까지 간다(spec §4, §13). 혀가 <see cref="MinTongue"/>보다 얇거나,
        /// 곧은 길이 <see cref="MinStraight"/>보다 짧거나, 턱 밑 틈이 <see cref="MinValleyGap"/>보다 좁으면 던진다.
        /// </summary>
        /// <param name="x0">계곡이 시작하는 x(평지 끝).</param>
        public static ShortcutRect ValleyShortcut(float x0, float baseY, float depth, float riseSlope,
                                                  float corridorHalf, float window,
                                                  ShortcutEntrance entrance, FlapArc arc)
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
            if (entrance.Arcs < 1 || entrance.Thickness <= 0f || entrance.Thickness > window)
            {
                throw new ArgumentOutOfRangeException(nameof(entrance), entrance.Thickness,
                    $"입구 굴은 호 1개 이상, 두께 0~{window:F2}m여야 한다 (호 {entrance.Arcs}, 두께 {entrance.Thickness})");
            }
            float bottom0 = x0 + depth / DropSlope;
            float bottom1 = bottom0 + ValleyBottom;
            //  내리막 천장선 y = topCeiling − DropSlope·(x − x0), 오르막 천장선 y = bottomCeiling + rise·(x − bottom1).
            float mouth = x0 + (topCeiling - (center + entrance.Thickness * 0.5f)) / DropSlope;
            if (mouth >= bottom0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth,
                    $"입구 {mouth:F1}이 계곡 바닥 시작 {bottom0:F1}보다 뒤다");
            }
            float channelEnd = mouth + entrance.Arcs * arc.Span;
            float exit = bottom1 + (y1 - bottomCeiling) / riseSlope;
            float tongueEnd = bottom1 + (y0 - bottomCeiling) / riseSlope;
            if (exit - channelEnd < MinStraight)
            {
                throw new ArgumentOutOfRangeException(nameof(entrance), entrance.Arcs,
                    $"호 {entrance.Arcs}개면 굴이 {channelEnd:F1}까지라 곧은 길이 {exit - channelEnd:F1}m뿐이다 (최소 {MinStraight}m)");
            }
            float floorAtMouth = topCeiling - DropSlope * (mouth - x0) - 2f * corridorHalf;
            float lipBottom = center - entrance.Thickness * 0.5f - entrance.Lip;
            if (lipBottom - floorAtMouth < MinValleyGap)
            {
                throw new ArgumentOutOfRangeException(nameof(entrance), entrance.Lip,
                    $"턱 {entrance.Lip}m 밑 계곡 틈이 {lipBottom - floorAtMouth:F1}m다 (최소 {MinValleyGap}m)");
            }
            return new ShortcutRect(mouth, channelEnd, exit, y0, y1, entrance, arc, tongueEnd,
                                    bottom0, bottom1, bottomCeiling, riseSlope);
        }

        /// <summary>
        /// 지름길 윗덩어리(지붕)를 세로 띠로 자른다. 굴 구간은 굴 윗면을 따라, 곧은 길은 <see cref="ShortcutRect.Y1"/>
        /// 위에 얹는다. 호가 이어진 모양은 오목해서 한 덩어리 볼록 콜라이더로 만들면 굴이 메워진다 — 그래서 띠다.
        /// </summary>
        public static List<float[]> ShortcutRoof(ShortcutRect r, float wallThickness, float step)
        {
            var strips = new List<float[]>();
            List<float> cuts = ChannelCuts(r, step);
            float half = r.Entrance.Thickness * 0.5f;
            for (int i = 1; i < cuts.Count; i++)
            {
                float a = cuts[i - 1], b = cuts[i];
                float bottomA = r.ChannelCenterAt(a) + half;
                float bottomB = r.ChannelCenterAt(b) + half;
                strips.Add(Strip(a, bottomA, bottomA + wallThickness, b, bottomB, bottomB + wallThickness));
            }
            strips.Add(Strip(r.ChannelEnd, r.Y1, r.Y1 + wallThickness, r.X1, r.Y1, r.Y1 + wallThickness));
            return strips;
        }

        /// <summary>
        /// 지름길과 계곡 사이 덩어리(혀)를 세로 띠로 자른다. 윗면은 굴 바닥(곧은 길에선 <see cref="ShortcutRect.Y0"/>),
        /// 아랫면은 계곡 천장선 — 단 입구 밑은 턱(세로 벽)에서 계곡 바닥 시작점까지 곧게 내려온다.
        /// </summary>
        public static List<float[]> ShortcutTongue(ShortcutRect r, float step)
        {
            //  계곡 천장이 꺾이는 곳(바닥 시작·끝)과 혀 끝은 띠의 곧은 아랫변이 반드시 지나야 하는
            //  모서리라 ChannelCuts에 "지켜야 할 자리"로 같이 넘긴다 — 그래야 바로 옆 step 자르기가
            //  알아서 비켜간다(아래 ChannelCuts 참고).
            List<float> cuts = ChannelCuts(r, step, r.ValleyBottom0, r.ValleyBottom1, r.TongueEnd);
            float half = r.Entrance.Thickness * 0.5f;
            var strips = new List<float[]>();
            for (int i = 1; i < cuts.Count; i++)
            {
                float a = cuts[i - 1], b = cuts[i];
                bool inChannel = (a + b) * 0.5f < r.ChannelEnd;
                float topA = inChannel ? r.ChannelCenterAt(a) - half : r.Y0;
                float topB = inChannel ? r.ChannelCenterAt(b) - half : r.Y0;
                strips.Add(Strip(a, TongueBottom(r, a), topA, b, TongueBottom(r, b), topB));
            }
            return strips;
        }

        //  경계(굴 끝·호 경계·keep으로 받은 모서리) 바로 옆에 step 자르기가 겹치면 폭 1cm 미만인
        //  얇은 조각이 생긴다 — 그 조각을 메시 콜라이더로 구우면(Task 2) 쉽게 깨지는 판정면이 된다.
        //  그래서 그런 자리의 step 자르기는 건너뛴다 — 경계 자체는 항상 남긴다.
        const float MinCutGap = 0.01f;

        //  굴 구간을 step마다 + 호 경계마다 + keep마다 자른 x들(입구·굴 끝 포함). 호 경계·keep은
        //  꼭 남아야 하는 모서리라 step보다 먼저 넣는다 — 그래야 바로 옆 step이 NearAnyCut에 걸려
        //  알아서 빠진다. keep은 굴 범위를 넘어(TongueEnd까지) 있을 수 있다(혀의 계곡 쪽 꺾인 점 등).
        static List<float> ChannelCuts(ShortcutRect r, float step, params float[] keep)
        {
            var cuts = new List<float> { r.X0, r.ChannelEnd };
            for (int k = 1; k < r.Entrance.Arcs; k++) { cuts.Add(r.X0 + k * r.Arc.Span); }
            foreach (float g in keep)
            {
                if (g > r.X0 && g <= r.TongueEnd) { cuts.Add(g); }
            }
            for (int i = 1; r.X0 + i * step < r.ChannelEnd; i++)
            {
                float x = r.X0 + i * step;
                if (NearAnyCut(cuts, x) == false) { cuts.Add(x); }
            }
            SortUnique(cuts);
            return cuts;
        }

        static bool NearAnyCut(List<float> cuts, float x)
        {
            foreach (float c in cuts)
            {
                if (Math.Abs(x - c) < MinCutGap) { return true; }
            }
            return false;
        }

        static void SortUnique(List<float> xs)
        {
            xs.Sort();
            for (int i = xs.Count - 1; i > 0; i--)
            {
                if (xs[i] - xs[i - 1] < 1e-4f) { xs.RemoveAt(i); }
            }
        }

        static float TongueBottom(ShortcutRect r, float x)
        {
            float ceiling;
            if (x <= r.ValleyBottom0) { ceiling = r.BottomCeiling + DropSlope * (r.ValleyBottom0 - x); }
            else if (x <= r.ValleyBottom1) { ceiling = r.BottomCeiling; }
            else { ceiling = r.BottomCeiling + r.RiseSlope * (x - r.ValleyBottom1); }
            if (x >= r.ValleyBottom0) { return ceiling; }
            float t = (x - r.X0) / (r.ValleyBottom0 - r.X0);
            float lip = r.LipBottom + (r.BottomCeiling - r.LipBottom) * t;
            return Math.Min(ceiling, lip);
        }

        //  반시계: 왼쪽 아래 → 오른쪽 아래 → 오른쪽 위 → 왼쪽 위 (빌더의 경사 조각과 같은 순서).
        static float[] Strip(float a, float bottomA, float topA, float b, float bottomB, float topB)
            => new[] { a, bottomA, b, bottomB, b, topB, a, topA };

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
