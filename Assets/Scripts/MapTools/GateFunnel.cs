using System;
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>한 x에서 위아래가 다 막힌 빈 창 하나 — 새가 들어갈 수 있는 자리다.</summary>
    public readonly struct GateWindow
    {
        public readonly float Bottom;
        public readonly float Top;
        public readonly string Floor;
        public readonly string Ceiling;

        public float Height => Top - Bottom;

        public GateWindow(float bottom, float top, string floor = null, string ceiling = null)
        {
            Bottom = bottom;
            Top = top;
            Floor = floor;
            Ceiling = ceiling;
        }
    }

    /// <summary>한 x의 창 전부. <see cref="PinchColumn"/>과 달리 <b>가장 넓은 것 하나로 접지 않는다</b> —
    /// 접으면 회랑을 가로로 가르는 칸막이가 사라져 "넓은 창 하나"로 보인다(x≈82가 정확히 그랬다).</summary>
    public readonly struct GateColumn
    {
        public readonly float X;
        public readonly IReadOnlyList<GateWindow> Windows;

        public GateColumn(float x, IReadOnlyList<GateWindow> windows)
        {
            X = x;
            Windows = windows;
        }
    }

    /// <summary>비행 한 틱의 자리 — 발 높이 기준. 궤적을 순수 계층에 넘기기 위한 최소 기록이다.</summary>
    public readonly struct FlightSample
    {
        public readonly float X;
        public readonly float Y;
        public readonly float VerticalSpeed;

        public FlightSample(float x, float y, float verticalSpeed)
        {
            X = x;
            Y = y;
            VerticalSpeed = verticalSpeed;
        }
    }

    /// <summary>한 비행이 한 관문을 어떻게 했나.</summary>
    public enum GateOutcome
    {
        /// <summary>관문 끝을 지나갔다.</summary>
        Passed,
        /// <summary>관문 안에서 멈췄다.</summary>
        Stopped,
        /// <summary>관문까지 오지도 못했다 — 앞에서 이미 멈춘 비행이다.</summary>
        NeverArrived,
    }

    /// <summary>비행 하나 × 관문 하나의 기록.</summary>
    public readonly struct GateCrossing
    {
        public readonly string Name;
        public readonly GateOutcome Outcome;
        /// <summary>관문 입구에서의 발 높이·세로 속도. <see cref="GateOutcome.NeverArrived"/>면 뜻 없다.</summary>
        public readonly float EntryY;
        public readonly float EntryVerticalSpeed;
        /// <summary>입구에서 들어가 있던 창의 번호. −1이면 어느 창에도 안 들어가 있었다.
        /// <b>번호는 그 열의 것</b>이라 다른 열과 비교하면 안 된다 — 그래서 리포트는 번호가
        /// 아니라 아래 <see cref="EntryWindowSpan"/>의 y범위를 찍는다.</summary>
        public readonly int EntryWindow;
        /// <summary>그 창의 y범위. 번호와 달리 열이 달라도 뜻이 같다.</summary>
        public readonly GateWindow EntryWindowSpan;
        /// <summary>그 창 바닥에서 얼마나 위였나.</summary>
        public readonly float EntryAboveBottom;
        /// <summary>멈춘 자리(Stopped일 때만).</summary>
        public readonly float StopY;
        public readonly float StopVerticalSpeed;
        /// <summary>멈춘 자리에서 가장 가까운 창까지 발이 얼마나 모자랐나. 양수면 그만큼 더
        /// 올라갔어야 했고, 음수면 그만큼 더 내려갔어야 했다. 0이면 창 안에서 멈춘 것이다.</summary>
        public readonly float Shortfall;
        /// <summary>그 가장 가까운 창의 번호(그 열 기준).</summary>
        public readonly int NearestWindow;
        /// <summary>그 창의 y범위.</summary>
        public readonly GateWindow NearestWindowSpan;
        public readonly string Hit;
        /// <summary>NeverArrived일 때 어디까지 갔나.</summary>
        public readonly float FarthestX;

        public GateCrossing(string name, GateOutcome outcome,
                            float entryY = 0f, float entryVerticalSpeed = 0f,
                            int entryWindow = -1, float entryAboveBottom = 0f,
                            float stopY = 0f, float stopVerticalSpeed = 0f,
                            float shortfall = 0f, int nearestWindow = -1,
                            string hit = null, float farthestX = 0f,
                            GateWindow entryWindowSpan = default,
                            GateWindow nearestWindowSpan = default)
        {
            Name = name;
            Outcome = outcome;
            EntryY = entryY;
            EntryVerticalSpeed = entryVerticalSpeed;
            EntryWindow = entryWindow;
            EntryWindowSpan = entryWindowSpan;
            EntryAboveBottom = entryAboveBottom;
            StopY = stopY;
            StopVerticalSpeed = stopVerticalSpeed;
            Shortfall = shortfall;
            NearestWindow = nearestWindow;
            NearestWindowSpan = nearestWindowSpan;
            Hit = hit;
            FarthestX = farthestX;
        }
    }

    /// <summary>깔때기 한 줄 — 이 입구 높이로 들어오면 어떤 세로 속도가 통과로 이어지나.</summary>
    public readonly struct FunnelRow
    {
        public readonly float Y;
        public readonly bool Passes;
        public readonly float MinVerticalSpeed;
        public readonly float MaxVerticalSpeed;
        /// <summary>통과하는 속도가 <b>이어져 있지 않다</b> — 사이에 못 지나는 속도가 끼어 있다.
        /// 이걸 안 적으면 "−14~+23 통과"가 그 사이 전부 통과라는 거짓말이 된다.</summary>
        public readonly bool Gapped;

        public FunnelRow(float y, bool passes, float min, float max, bool gapped)
        {
            Y = y;
            Passes = passes;
            MinVerticalSpeed = min;
            MaxVerticalSpeed = max;
            Gapped = gapped;
        }
    }

    /// <summary>플래피의 한 틱 물리. 순수 계층이 자기 힘으로 굴려 보기 위한 값 묶음이다.</summary>
    public readonly struct FlightKernel
    {
        public readonly float ForwardSpeed;
        public readonly float Gravity;
        public readonly float MaxFallSpeed;
        public readonly float FlapImpulse;
        public readonly float TickSeconds;
        public readonly float BodyHeight;

        public FlightKernel(float forwardSpeed, float gravity, float maxFallSpeed, float flapImpulse,
                            float tickSeconds, float bodyHeight)
        {
            ForwardSpeed = forwardSpeed;
            Gravity = gravity;
            MaxFallSpeed = maxFallSpeed;
            FlapImpulse = flapImpulse;
            TickSeconds = tickSeconds;
            BodyHeight = bodyHeight;
        }

        /// <summary>다음 틱의 세로 속도. 날갯짓은 그때까지의 속도를 <b>덮어쓴다</b>(더하지 않는다).</summary>
        public float NextVerticalSpeed(float verticalSpeed, bool flap)
        {
            if (flap)
            {
                return FlapImpulse;
            }
            float vy = verticalSpeed - Gravity * TickSeconds;
            return vy < -MaxFallSpeed ? -MaxFallSpeed : vy;
        }
    }

    /// <summary>관문 하나에 대해 모은 것 전부 — 리포트 한 절의 재료다.</summary>
    public sealed class GateReport
    {
        public float StartX;
        public float EndX;
        /// <summary>이 관문 안에서 멈춘 비행의 수. 관문을 고르는 기준이다.</summary>
        public int StopCount;
        /// <summary>관문 구간의 열들(입구부터 출구까지).</summary>
        public IReadOnlyList<GateColumn> Columns;
        /// <summary>회랑을 가장 여러 창으로 가른 열 — 관문의 얼굴이다.</summary>
        public int FaceIndex;
        public List<GateCrossing> Crossings = new List<GateCrossing>();
        public List<FunnelRow> Funnel = new List<FunnelRow>();
        /// <summary>이 관문의 지형이 <b>도는가</b>. 돌면 도착 틱에 따라 기하가 달라져 아래 숫자가
        /// 한 위상의 것이 된다 — 안 돌면 이 절의 숫자는 틱과 무관하다.</summary>
        public bool Rotating;
    }

    /// <summary>
    /// <b>관문</b> — 비행이 실제로 멈추는 자리 — 을 "누가 어떤 (높이, 세로속도)로 왔나"로 부검한다.
    ///
    /// <para>②-c(<see cref="StaticPinchRule"/>)는 한 x의 창을 <b>가장 넓은 것 하나로 접는다</b>.
    /// 그래서 회랑을 가로로 가르는 칸막이가 접히면서 사라진다 — 창이 둘로 갈려 각각 5m면
    /// ②-c는 "5m짜리 넓은 자리"라고 보고하지만, 새는 <b>둘 중 하나를 골라 그 안에 들어가
    /// 있어야</b> 지난다. 이 절은 접지 않고 창을 전부 보여 준 뒤, 비행마다 어느 창으로
    /// 들어갔는지를 적는다.</para>
    ///
    /// <para><b>깔때기</b>는 그 반대 방향의 질문이다: 관문 입구에서 어떤 (높이, 세로속도)로
    /// 들어와야 지나가나. 입구 상태를 격자로 훑어 관문 구간만 굴려 본다(날갯짓은 마음대로
    /// 넣어 본다 — 한 갈래라도 빠져나가면 통과다). 실제 봇이 아니라 <b>이상적인 조종</b>이므로,
    /// 깔때기 안인데 못 지났다면 그건 지형이 아니라 겨냥 탓이다.</para>
    /// </summary>
    public static class GateFunnelRule
    {
        //  깔때기 굴려 보기가 이만큼 퍼지면 그만둔다 — 관문은 몇 m짜리라 정상이면 수백으로 닫힌다.
        const int MaxFunnelStates = 20000;
        //  같은 상태로 볼 눈금. 촘촘하면 안 닫히고 굵으면 다른 상태를 뭉갠다(②의 탐색과 같은 이유).
        const float StateGrid = 0.05f;
        const float StateSpeedGrid = 0.5f;

        // ── 창 ──────────────────────────────────────────────────────────────

        /// <summary>발을 <paramref name="feetY"/>에 두었을 때 몸이 통째로 들어가는 창의 번호.
        /// 없으면 −1. <b>몸 높이를 뺀 자리</b>로 재는 것이 핵심이다 — 발만 창 안이고 머리가
        /// 천장을 뚫는 자리를 통과로 세면 안 된다.</summary>
        public static int WindowAt(float feetY, float bodyHeight, IReadOnlyList<GateWindow> windows)
        {
            for (int i = 0; windows != null && i < windows.Count; i++)
            {
                if (feetY >= windows[i].Bottom && feetY + bodyHeight <= windows[i].Top)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>발이 <paramref name="lowY"/>~<paramref name="highY"/> 사이를 훑고 지나갔을 때
        /// 그 전부가 <b>한 창 안</b>이었나. 한 틱에 4m를 떨어지므로 끝점만 보면 칸막이를 뚫고
        /// 지나간 것을 통과로 세게 된다.</summary>
        public static bool SpanFits(float lowY, float highY, float bodyHeight,
                                    IReadOnlyList<GateWindow> windows)
        {
            for (int i = 0; windows != null && i < windows.Count; i++)
            {
                if (lowY >= windows[i].Bottom && highY + bodyHeight <= windows[i].Top)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>가장 가까운 창까지 발이 얼마나 모자랐나. 양수면 그만큼 올라갔어야 했고,
        /// 음수면 그만큼 내려갔어야 했다. 창 안이면 0이다. 들어갈 창이 아예 없으면 −1을 낸다.</summary>
        public static int NearestWindow(float feetY, float bodyHeight, IReadOnlyList<GateWindow> windows,
                                        out float shortfall)
        {
            shortfall = 0f;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; windows != null && i < windows.Count; i++)
            {
                //  몸이 안 들어가는 창은 후보가 아니다 — "0.2m만 올라가면 된다"가 거짓이 된다.
                float highestFeet = windows[i].Top - bodyHeight;
                if (highestFeet < windows[i].Bottom)
                {
                    continue;
                }
                float need = feetY < windows[i].Bottom ? windows[i].Bottom - feetY
                           : feetY > highestFeet ? highestFeet - feetY
                           : 0f;
                float distance = Math.Abs(need);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                    shortfall = need;
                }
            }
            return best;
        }

        // ── 관문 고르기 ─────────────────────────────────────────────────────

        /// <summary>
        /// 비행이 멈춘 x들을 <b>관문 구간</b>으로 묶는다. ②-c의 좁힘 구간을 쓰지 않는 이유는
        /// 단순하다 — 창을 접고 보면 이 맵엔 좁은 구간이 하나도 없는데, 비행은 실제로 멈춘다.
        /// <paramref name="gap"/>보다 멀리 떨어진 멈춤은 다른 관문으로 보고, 앞뒤로
        /// <paramref name="pad"/>만큼 넓혀 <b>막히기 전</b>부터 보게 한다(입구 상태를 재려면
        /// 회랑이 아직 열려 있는 자리가 필요하다).
        /// <para>멈춘 비행이 많은 순으로 돌려준다 — 가장 많이 죽이는 자리가 맨 앞이다.</para>
        /// </summary>
        public static List<(float StartX, float EndX, int StopCount)> Cluster(
            IReadOnlyList<float> stopXs, float gap, float pad, float sampleStep)
        {
            var gates = new List<(float StartX, float EndX, int StopCount)>();
            if (stopXs == null || stopXs.Count == 0)
            {
                return gates;
            }
            var sorted = new List<float>(stopXs);
            sorted.Sort();
            int runStart = 0;
            for (int i = 1; i <= sorted.Count; i++)
            {
                if (i < sorted.Count && sorted[i] - sorted[i - 1] <= gap)
                {
                    continue;
                }
                float low = Snap(sorted[runStart] - pad, sampleStep, down: true);
                float high = Snap(sorted[i - 1] + pad, sampleStep, down: false);
                gates.Add((low, high, i - runStart));
                runStart = i;
            }
            //  같은 수로 멈췄으면 앞쪽 관문이 먼저다 — 코스 순서가 읽기 쉽다.
            gates.Sort((a, b) => a.StopCount != b.StopCount
                ? b.StopCount.CompareTo(a.StopCount)
                : a.StartX.CompareTo(b.StartX));
            return gates;
        }

        static float Snap(float value, float step, bool down)
        {
            if (step <= 0f)
            {
                return value;
            }
            double n = value / step;
            return (float)((down ? Math.Floor(n) : Math.Ceiling(n)) * step);
        }

        /// <summary>
        /// 관문을 한 줄로 설명할 때 보여 줄 <b>얼굴</b> — <b>가장 좋은 선택지가 가장 나쁜 열</b>이다.
        /// 즉 그 열에서 몸이 들어가는 창 중 가장 넓은 것을 고른 다음, 그 값이 가장 작은 열을 고른다.
        /// 같으면 창이 여럿으로 갈린 열, 그래도 같으면 앞쪽 열.
        /// <para><b>"창이 가장 많은 열"로 고르면 안 된다</b> — 코스 위 하늘에 떠 있는 1m짜리 장식
        /// 주머니 하나가 그 열을 우승시켜, 정작 회랑을 가로막은 칸막이 대신 장식을 관문의 얼굴로
        /// 찍는다(실측에서 실제로 그랬다: x≈82의 칸막이 대신 x=83.5의 하늘 주머니가 뽑혔다).</para>
        /// </summary>
        public static int FaceColumn(IReadOnlyList<GateColumn> columns, float bodyHeight)
        {
            int best = -1;
            int bestCount = 0;
            float bestWidest = float.MaxValue;
            for (int i = 0; columns != null && i < columns.Count; i++)
            {
                int count = 0;
                float widest = 0f;
                var windows = columns[i].Windows;
                for (int w = 0; windows != null && w < windows.Count; w++)
                {
                    if (windows[w].Height < bodyHeight)
                    {
                        continue;
                    }
                    count++;
                    if (windows[w].Height > widest)
                    {
                        widest = windows[w].Height;
                    }
                }
                if (widest < bestWidest || (widest == bestWidest && count > bestCount))
                {
                    best = i;
                    bestCount = count;
                    bestWidest = widest;
                }
            }
            return best;
        }

        // ── 통과 기록 ───────────────────────────────────────────────────────

        /// <summary>비행 하나가 관문을 어떻게 했는지 기록한다. 궤적은 발 높이 기준이며,
        /// 마지막 표본은 멈춘 자리여야 한다.</summary>
        public static GateCrossing Cross(string name, float startX, float endX,
                                         IReadOnlyList<GateColumn> columns,
                                         IReadOnlyList<FlightSample> path, float bodyHeight)
        {
            float farthest = float.MinValue;
            int entry = -1;
            int last = -1;
            for (int i = 0; path != null && i < path.Count; i++)
            {
                if (path[i].X > farthest)
                {
                    farthest = path[i].X;
                }
                if (path[i].X >= startX)
                {
                    if (entry < 0)
                    {
                        entry = i;
                    }
                    if (path[i].X <= endX)
                    {
                        last = i;
                    }
                }
            }
            if (entry < 0)
            {
                return new GateCrossing(name, GateOutcome.NeverArrived,
                                        farthestX: farthest == float.MinValue ? 0f : farthest);
            }
            FlightSample at = path[entry];
            var entryWindows = ColumnAt(columns, at.X).Windows;
            int entryWindow = WindowAt(at.Y, bodyHeight, entryWindows);
            GateWindow entrySpan = entryWindow >= 0 ? entryWindows[entryWindow] : default;
            float above = entryWindow >= 0 ? at.Y - entrySpan.Bottom : 0f;
            if (farthest > endX)
            {
                return new GateCrossing(name, GateOutcome.Passed, at.Y, at.VerticalSpeed,
                                        entryWindow, above, entryWindowSpan: entrySpan);
            }
            FlightSample stop = path[last < 0 ? path.Count - 1 : last];
            var stopWindows = ColumnAt(columns, stop.X).Windows;
            int nearest = NearestWindow(stop.Y, bodyHeight, stopWindows, out float shortfall);
            return new GateCrossing(name, GateOutcome.Stopped, at.Y, at.VerticalSpeed,
                                    entryWindow, above,
                                    stop.Y, stop.VerticalSpeed, shortfall, nearest,
                                    farthestX: farthest,
                                    entryWindowSpan: entrySpan,
                                    nearestWindowSpan: nearest >= 0 ? stopWindows[nearest] : default);
        }

        static GateColumn ColumnAt(IReadOnlyList<GateColumn> columns, float x)
        {
            if (columns == null || columns.Count == 0)
            {
                return new GateColumn(x, Array.Empty<GateWindow>());
            }
            if (columns.Count == 1)
            {
                return columns[0];
            }
            float step = columns[1].X - columns[0].X;
            int index = step > 0f ? (int)Math.Round((x - columns[0].X) / step) : 0;
            if (index < 0)
            {
                index = 0;
            }
            if (index >= columns.Count)
            {
                index = columns.Count - 1;
            }
            return columns[index];
        }

        // ── 깔때기 ──────────────────────────────────────────────────────────

        /// <summary>입구에서 이 (높이, 세로속도)로 들어오면 관문을 지날 수 있나. 날갯짓을 마음대로
        /// 넣어 본다 — 한 갈래라도 빠져나가면 참이다(<b>이상적인 조종</b>, 실제 봇이 아니다).</summary>
        public static bool Rolls(float entryY, float entryVerticalSpeed,
                                 IReadOnlyList<GateColumn> columns, in FlightKernel kernel)
        {
            if (columns == null || columns.Count < 2)
            {
                return false;
            }
            float startX = columns[0].X;
            float endX = columns[columns.Count - 1].X;
            //  입구가 창 밖인지는 따로 안 막는다 — 아래 훑기 검사가 첫 틱에 <b>출발 높이도
            //  함께</b> 재므로 창 밖에서 출발하면 거기서 걸린다. 앞에 가드를 하나 더 두면
            //  아무것도 막지 않는 줄이 되고, 그걸 깨는 테스트도 못 만든다(실제로 확인했다).
            var seen = new HashSet<long>();
            var frontier = new List<(float X, float Y, float Vy)> { (startX, entryY, entryVerticalSpeed) };
            var next = new List<(float X, float Y, float Vy)>();
            int states = 0;
            //  틱 수는 구간 길이에서 나온다 — 상수로 박으면 관문 길이가 바뀔 때 조용히 잘린다.
            int ticks = (int)Math.Ceiling((endX - startX) / (kernel.ForwardSpeed * kernel.TickSeconds)) + 2;
            for (int tick = 0; tick < ticks && frontier.Count > 0; tick++)
            {
                next.Clear();
                for (int i = 0; i < frontier.Count; i++)
                {
                    var s = frontier[i];
                    for (int f = 0; f < 2; f++)
                    {
                        float vy = kernel.NextVerticalSpeed(s.Vy, f == 1);
                        float y = s.Y + vy * kernel.TickSeconds;
                        float x = s.X + kernel.ForwardSpeed * kernel.TickSeconds;
                        float low = Math.Min(s.Y, y);
                        float high = Math.Max(s.Y, y);
                        //  한 틱에 지나간 두 열 모두에서 몸이 한 창 안에 있어야 한다.
                        if (SpanFits(low, high, kernel.BodyHeight, ColumnAt(columns, s.X).Windows) == false
                            || SpanFits(low, high, kernel.BodyHeight, ColumnAt(columns, x).Windows) == false)
                        {
                            continue;
                        }
                        if (x > endX)
                        {
                            return true;
                        }
                        long key = ((long)tick << 40)
                                 ^ ((long)Math.Round(y / StateGrid) << 20)
                                 ^ (long)Math.Round(vy / StateSpeedGrid);
                        if (seen.Add(key) == false)
                        {
                            continue;
                        }
                        if (++states > MaxFunnelStates)
                        {
                            return false;
                        }
                        next.Add((x, y, vy));
                    }
                }
                var swap = frontier;
                frontier = next;
                next = swap;
            }
            return false;
        }

        /// <summary>입구 격자를 훑어 깔때기를 낸다 — 높이 한 줄마다 통과하는 세로속도의 범위.</summary>
        public static List<FunnelRow> Funnel(IReadOnlyList<GateColumn> columns, in FlightKernel kernel,
                                             float yStep, float verticalSpeedStep)
        {
            var rows = new List<FunnelRow>();
            if (columns == null || columns.Count == 0)
            {
                return rows;
            }
            var windows = columns[0].Windows;
            if (windows == null || windows.Count == 0)
            {
                return rows;
            }
            float low = float.MaxValue;
            float high = float.MinValue;
            for (int i = 0; i < windows.Count; i++)
            {
                low = Math.Min(low, windows[i].Bottom);
                high = Math.Max(high, windows[i].Top - kernel.BodyHeight);
            }
            for (float y = low; y <= high + 1e-4f; y += yStep)
            {
                float min = float.MaxValue;
                float max = float.MinValue;
                bool gapped = false;
                bool wasPass = false;
                bool sawPass = false;
                for (float vy = -kernel.MaxFallSpeed; vy <= kernel.FlapImpulse + 1e-4f; vy += verticalSpeedStep)
                {
                    bool pass = Rolls(y, vy, columns, kernel);
                    if (pass)
                    {
                        if (sawPass && wasPass == false)
                        {
                            gapped = true;
                        }
                        sawPass = true;
                        min = Math.Min(min, vy);
                        max = Math.Max(max, vy);
                    }
                    wasPass = pass;
                }
                rows.Add(sawPass
                    ? new FunnelRow(y, true, min, max, gapped)
                    : new FunnelRow(y, false, 0f, 0f, false));
            }
            return rows;
        }

        // ── 리포트 ──────────────────────────────────────────────────────────

        /// <summary>리포트의 "① 진단 — 관문 통과" 절 전체(머리말 줄 포함, 끝에 줄바꿈 없음).</summary>
        public static string Section(IReadOnlyList<GateReport> gates, in FlightKernel kernel,
                                     float requiredBand, float verticalSpeedStep)
        {
            var text = new StringBuilder();
            text.AppendLine("── ① 진단 — 관문 통과 ──────────────────");
            text.AppendLine("  (판정이 아니라 진단이다. 비행이 실제로 멈추는 자리를 관문으로 묶고, 비행마다");
            text.AppendLine("   그 관문을 어떤 높이·세로속도로 맞았는지 적는다. y는 전부 <발> 높이다.)");
            text.AppendLine("  (②-c는 한 x의 창을 가장 넓은 것 하나로 접는다 — 회랑을 가로로 가르는 칸막이가");
            text.AppendLine($"   그 접힘에 사라진다. 여기서는 접지 않고 몸({kernel.BodyHeight:F2}m)이 들어가는 창을 전부 적는다.)");
            text.AppendLine($"  (깔때기 = 입구에서 어떤 (높이, 세로속도)로 들어와야 지나가나. 세로속도는"
                          + $" {verticalSpeedStep:F0}m/s 간격으로 훑었고,");
            text.AppendLine("   날갯짓은 마음대로 넣어 본다 — 즉 <이상적인 조종>이다. 깔때기 안인데 못 지났다면");
            text.AppendLine("   그건 지형이 아니라 겨냥 탓이다.)");
            if (gates == null || gates.Count == 0)
            {
                text.Append("  관문 없음 — 멈춘 비행이 하나도 없다");
                return text.ToString();
            }
            for (int g = 0; g < gates.Count; g++)
            {
                AppendGate(text, gates[g], kernel, requiredBand, verticalSpeedStep);
            }
            text.Append("  (주의: 깔때기는 관문 구간만 굴린 것이라 <입구까지 어떻게 오나>는 답하지 않는다 —"
                      + " 입구 상태 자체가 앞 구간의 결과다.)");
            return text.ToString();
        }

        static void AppendGate(StringBuilder text, GateReport gate, in FlightKernel kernel,
                               float requiredBand, float verticalSpeedStep)
        {
            var windows = gate.Columns != null && gate.FaceIndex >= 0 && gate.FaceIndex < gate.Columns.Count
                ? gate.Columns[gate.FaceIndex].Windows
                : Array.Empty<GateWindow>();
            float faceX = gate.Columns != null && gate.FaceIndex >= 0 && gate.FaceIndex < gate.Columns.Count
                ? gate.Columns[gate.FaceIndex].X
                : gate.StartX;
            text.AppendLine();
            text.AppendLine($"  ■ x {gate.StartX:F1}~{gate.EndX:F1} — 비행 {gate.StopCount}개가 여기서 멈췄다"
                          + $"  (가장 좁힌 열 x={faceX:F1} — 그 열에서 가장 넓은 창이 코스에서 가장 좁다)");
            //  도는 지형이면 이 절의 기하가 한 위상의 것이다 — 안 돌면 그렇지 않다는 것도
            //  적어야 읽는 사람이 "혹시 도착 시점 탓인가"를 스스로 배제할 수 있다.
            text.AppendLine(gate.Rotating
                ? "    ⚠️ 이 관문엔 도는 지형이 있다 — 아래 창은 틱 0 자세의 것이다(도착 시점에 따라 달라진다)."
                : "    이 관문은 돌지 않는 지형이다 — 창은 언제 도착하든 같다(도착 시점과 무관).");
            int passable = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].Height < kernel.BodyHeight)
                {
                    continue;
                }
                passable++;
                string wide = windows[i].Height >= requiredBand ? "아치+몸 들어감" : "아치+몸보다 좁음";
                text.AppendLine($"    창{passable}  y {windows[i].Bottom:F2}~{windows[i].Top:F2}"
                              + $" (폭 {windows[i].Height:F2}m, {wide})"
                              + $"  발이 있을 수 있는 높이 {windows[i].Bottom:F2}~{windows[i].Top - kernel.BodyHeight:F2}");
            }
            if (passable >= 2)
            {
                text.AppendLine($"    → 창이 {passable}개다. 폭이 넓어도 <어느 창에 들어가 있느냐>가 통과를 가른다"
                              + " — ②-c는 이 갈림을 접어 버려 ✅로 본다.");
            }
            else if (passable == 0)
            {
                text.AppendLine("    → 몸이 들어가는 창이 없다 — 이 열은 통째로 막혀 있다.");
            }

            AppendCrossings(text, gate);
            AppendFunnel(text, gate, verticalSpeedStep);
        }

        static void AppendCrossings(StringBuilder text, GateReport gate)
        {
            int never = 0;
            float farthest = float.MinValue;
            for (int i = 0; i < gate.Crossings.Count; i++)
            {
                GateCrossing c = gate.Crossings[i];
                if (c.Outcome == GateOutcome.NeverArrived)
                {
                    never++;
                    if (c.FarthestX > farthest)
                    {
                        farthest = c.FarthestX;
                    }
                    continue;
                }
                //  창을 번호가 아니라 y범위로 적는다 — 번호는 <b>그 열</b>의 것이라, 입구와
                //  멈춘 자리가 다른 열이면 같은 번호가 다른 창을 가리킨다(실측에서 한 표 안에
                //  "창2"와 "창3"이 같은 창을 뜻했다).
                string window = c.EntryWindow >= 0
                    ? $"창 y {c.EntryWindowSpan.Bottom:F2}~{c.EntryWindowSpan.Top:F2} 바닥 +{c.EntryAboveBottom:F2}m"
                    : "어느 창에도 안 들어감";
                if (c.Outcome == GateOutcome.Passed)
                {
                    text.AppendLine($"    {c.Name,-16} 통과   입구 y={c.EntryY,7:F2} vy={c.EntryVerticalSpeed,6:F1}"
                                  + $"  ({window})");
                    continue;
                }
                string near = $"창 y {c.NearestWindowSpan.Bottom:F2}~{c.NearestWindowSpan.Top:F2}";
                string miss = c.NearestWindow < 0
                    ? "들어갈 창 자체가 없다"
                    : Math.Abs(c.Shortfall) < 0.005f
                        ? $"{near} 바닥에 얹힘"
                        : c.Shortfall > 0f
                            ? $"{near} 바닥까지 {c.Shortfall:F2}m 모자람"
                            : $"{near} 천장을 {-c.Shortfall:F2}m 넘김";
                text.AppendLine($"    {c.Name,-16} 실패   입구 y={c.EntryY,7:F2} vy={c.EntryVerticalSpeed,6:F1}"
                              + $"  ({window})");
                text.AppendLine($"    {string.Empty,-16}        멈춤 y={c.StopY,7:F2} vy={c.StopVerticalSpeed,6:F1}"
                              + $"  ({miss})");
            }
            if (never > 0)
            {
                text.AppendLine($"    도달 못 함 {never}줄 — 앞 관문에서 이미 멈췄다 (그중 가장 멀리 간 것 x={farthest:F1})");
            }
        }

        static void AppendFunnel(StringBuilder text, GateReport gate, float verticalSpeedStep)
        {
            if (gate.Funnel == null || gate.Funnel.Count == 0)
            {
                text.AppendLine($"    진입 깔때기: 못 쟀다 — 입구 x={gate.StartX:F1}에 몸이 들어가는 창이 없다");
                return;
            }
            text.AppendLine($"    진입 깔때기 (입구 x={gate.StartX:F1}):");
            bool any = false;
            int i = 0;
            while (i < gate.Funnel.Count)
            {
                if (gate.Funnel[i].Passes == false)
                {
                    i++;
                    continue;
                }
                int j = i;
                while (j + 1 < gate.Funnel.Count && gate.Funnel[j + 1].Passes
                       && Same(gate.Funnel[j + 1], gate.Funnel[i], verticalSpeedStep))
                {
                    j++;
                }
                string speed = gate.Funnel[i].MaxVerticalSpeed - gate.Funnel[i].MinVerticalSpeed < 1e-4f
                    ? $"vy={gate.Funnel[i].MinVerticalSpeed:F1}만"
                    : $"vy {gate.Funnel[i].MinVerticalSpeed:F1}~{gate.Funnel[i].MaxVerticalSpeed:F1}";
                string gapped = gate.Funnel[i].Gapped ? "  ⚠️ 그 사이에 못 지나는 속도가 끼어 있다" : "";
                text.AppendLine($"      y {gate.Funnel[i].Y,7:F2}~{gate.Funnel[j].Y,7:F2}   {speed}{gapped}");
                any = true;
                i = j + 1;
            }
            if (any == false)
            {
                text.AppendLine("      어떤 (높이, 세로속도)로 들어와도 못 지난다 — 지형이 막은 것이다");
            }
        }

        static bool Same(in FunnelRow a, in FunnelRow b, float tolerance)
            => Math.Abs(a.MinVerticalSpeed - b.MinVerticalSpeed) < tolerance * 0.5f
            && Math.Abs(a.MaxVerticalSpeed - b.MaxVerticalSpeed) < tolerance * 0.5f
            && a.Gapped == b.Gapped;
    }
}
