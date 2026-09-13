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

        //  정점까지 따라가는 데 이보다 많은 틱이 든다면 입력이 잘못된 것이다 — 실제 값(임펄스 23,
        //  중력 70, 틱 0.02초)으로는 18틱이면 끝난다. BotPilot도 같은 이유로 같은 상한을 쓴다.
        const int MaxArcTicks = 100000;

        /// <summary>날갯짓 한 번이 <b>정점에 닿을 때까지</b>의 틱수. 상수로 박지 않는다 — 중력이나
        /// 임펄스를 바꾸면 이 수도 따라 움직여야 한다(실제 값으로는 18틱).
        /// <para>세는 방법은 <c>BotPilot</c>의 아치 훑기와 같다: 누른 틱은 임펄스 그대로 가고 그다음
        /// 틱부터 중력이 깎으므로, 속도가 0으로 떨어지는 데 드는 틱수 + 1이다. double로 세는 이유도
        /// 같다 — float으로 세면 한 틱에 깎는 양이 값의 최소 단위보다 작아질 때 이 수가 안 닫힌다.</para></summary>
        public int ArcTicks
        {
            get
            {
                if (Gravity <= 0f || TickSeconds <= 0f || FlapImpulse <= 0f)
                {
                    return 0;
                }
                double ticks = Math.Ceiling(FlapImpulse / ((double)Gravity * TickSeconds)) + 1;
                return ticks > MaxArcTicks ? MaxArcTicks : (int)ticks;
            }
        }

        /// <summary>그 아치가 앞으로 나아가는 거리 — 깔때기가 관문 끝 뒤로 더 봐야 하는 거리다.</summary>
        public float ArcDistance => ArcTicks * ForwardSpeed * TickSeconds;
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
        /// <summary>관문 <b>뒤</b>의 열들 — 관문 안에서 누른 날갯짓의 아치가 끝날 때까지 더 보는
        /// 자리다. 비어 있으면 관문 뒤를 못 본다는 뜻이고, 그러면 깔때기는 옛날처럼 관문 끝에서
        /// 눈을 감는다(리포트가 그 사실을 스스로 밝힌다).</summary>
        public IReadOnlyList<GateColumn> Runout;
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

        /// <summary>이 x가 속한 열. <b>재생(<see cref="GateFunnelReplayRule"/>)이 같은 열을
        /// 봐야</b> 깔때기와 재생이 같은 자로 재는 것이 된다 — 그래서 공개한다.</summary>
        public static GateColumn ColumnAt(IReadOnlyList<GateColumn> columns, float x)
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
        /// 넣어 본다 — 한 갈래라도 빠져나가면 참이다(<b>이상적인 조종</b>, 실제 봇이 아니다).
        /// <para><paramref name="runout"/> = 관문 <b>뒤</b>의 열들. 관문을 나설 때 아직 올라가는
        /// 중이면 그 오름은 취소할 수 없으므로(아래 <see cref="ArcClears"/>) 거기까지 더 본다.</para></summary>
        public static bool Rolls(float entryY, float entryVerticalSpeed,
                                 IReadOnlyList<GateColumn> columns, IReadOnlyList<GateColumn> runout,
                                 in FlightKernel kernel)
            => TryRolls(entryY, entryVerticalSpeed, columns, runout, kernel, out _, out _);

        /// <summary><see cref="Rolls"/>와 같은 굴려 보기인데, 지나갔다면 <b>어떤 조작열로</b>
        /// 지나갔는지까지 돌려준다. 둘이 따로 굴리면 "통과한다"와 "그 조작열"이 서로 다른
        /// 물리에서 나올 수 있으므로, <see cref="Rolls"/>는 이 메서드를 부른다.
        /// <para><paramref name="flaps"/>[t] = t번째 틱에 날갯짓했나. 못 지났으면 빈 목록이다.</para></summary>
        public static bool TryRolls(float entryY, float entryVerticalSpeed,
                                    IReadOnlyList<GateColumn> columns, IReadOnlyList<GateColumn> runout,
                                    in FlightKernel kernel, out List<bool> flaps)
            => TryRolls(entryY, entryVerticalSpeed, columns, runout, kernel, out flaps, out _);

        /// <summary><see cref="TryRolls(float,float,IReadOnlyList{GateColumn},IReadOnlyList{GateColumn},in FlightKernel,out List{bool})"/>와
        /// 같은데, <b>거짓이 두 가지 뜻이라는 것</b>을 갈라 준다.
        ///
        /// <para><paramref name="gaveUp"/>가 참이면 "못 지난다"가 아니라 <b>못 쟀다</b>는 뜻이다 —
        /// 굴려 보기가 상태 한도(<see cref="MaxFunnelStates"/>)에 걸려 도중에 그만뒀다. 창이
        /// 넓어질수록 볼 상태가 기하급수로 늘어 이 한도에 먼저 닿는데, 그걸 "막혔다"로 읽으면
        /// <b>천장을 올릴수록 더 안 된다</b>는 거짓말이 된다(처방 훑기에서 실제로 그렇게 나왔다).</para></summary>
        public static bool TryRolls(float entryY, float entryVerticalSpeed,
                                    IReadOnlyList<GateColumn> columns, IReadOnlyList<GateColumn> runout,
                                    in FlightKernel kernel, out List<bool> flaps, out bool gaveUp)
        {
            flaps = new List<bool>();
            gaveUp = false;
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
            //  되짚어 올라가 조작열을 복원하려고 부모를 들고 있는다 — 굴려 보기 자체는
            //  예전과 같고, 이 목록만 곁에 늘어난다.
            var parent = new List<int>();
            var flapOf = new List<bool>();
            var frontier = new List<(float X, float Y, float Vy, int Node)> { (startX, entryY, entryVerticalSpeed, -1) };
            var next = new List<(float X, float Y, float Vy, int Node)>();
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
                            //  관문 끝을 넘었다고 바로 통과가 아니다 — 아직 올라가는 중이면 그
                            //  오름은 이미 확정된 것이라 관문 뒤에서 박을 수 있다. 그 갈래는
                            //  버리고 다른 갈래를 계속 본다(이 자리에서 false로 끝내면 안 된다).
                            if (ArcClears(x, y, vy, runout, kernel) == false)
                            {
                                continue;
                            }
                            Unwind(parent, flapOf, s.Node, f == 1, flaps);
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
                            gaveUp = true;
                            return false;
                        }
                        parent.Add(s.Node);
                        flapOf.Add(f == 1);
                        next.Add((x, y, vy, parent.Count - 1));
                    }
                }
                var swap = frontier;
                frontier = next;
                next = swap;
            }
            return false;
        }

        /// <summary>
        /// 관문을 나선 그 자리에서 <b>이미 확정된 것</b>이 다 끝날 때까지 더 굴려 본다.
        ///
        /// <para><b>왜 필요한가.</b> 날갯짓은 세로 속도를 <b>덮어쓰므로</b>(더하지 않는다) 한 번
        /// 누르면 정점까지 다 올라간다 — 부분 아치가 없다. 그래서 "관문 끝을 넘었다"에서 눈을
        /// 감으면, 관문 안에서 누른 날갯짓이 관문 <i>밖</i> 1.11m에서 천장에 박는 궤적도 통과로
        /// 세게 된다(2026-09-12 실측: x 80.0~83.5 관문, 진짜 커널로 x=84.61에서 박았다).</para>
        ///
        /// <para><b>지평의 끝을 무엇으로 잡나.</b> "누른 지 몇 틱"이 아니라 <b>아직 올라가는
        /// 중인가</b>(vy &gt; 0)로 잡는다. 물리에서 곧바로 나오는 조건이라 중력·임펄스를 바꾸면
        /// 따라 움직이고, "관문 안에서 안 눌렀으면 확정된 것이 없다"도 이 조건의 한 경우로 덮인다
        /// — 다만 <b>안 눌러도 올라가는 중일 수 있다</b>(입구 vy가 양수인 경우). 그래서 "안 눌렀으면
        /// 더 볼 것 없다"는 참이 아니고, 떨어지는 중일 때만 참이다. 떨어지는 중이면 다음 틱에
        /// 눌러 올라갈 수 있으므로 확정된 것이 없다.</para>
        ///
        /// <para><b>왜 여기서는 날갯짓을 안 넣어 보나.</b> 날갯짓은 vy를 23으로 덮어써 <b>더</b>
        /// 올릴 뿐이라, 천장 쪽 막힘은 더 빨리 만난다 — 즉 안 누르는 이 경로가 천장에 대해 가장
        /// 유리하다. 반대로 <b>바닥</b> 쪽 막힘은 누르면 피할 수 있으므로 통과를 취소하지 않는다
        /// (<see cref="CeilingBlocks"/>). 단 그렇게 살린 갈래가 누른 뒤 그리는 새 아치까지는 보지
        /// 않는다 — 그만큼은 여전히 낙관적이다.</para>
        /// </summary>
        public static bool ArcClears(float x, float y, float verticalSpeed,
                                     IReadOnlyList<GateColumn> runout, in FlightKernel kernel)
        {
            int limit = kernel.ArcTicks;
            float vy = verticalSpeed;
            for (int t = 0; t < limit; t++)
            {
                float nextVy = kernel.NextVerticalSpeed(vy, flap: false);
                if (nextVy <= 0f)
                {
                    //  더 오르지 않는다 — 확정된 것이 여기서 끝난다.
                    return true;
                }
                float nextY = y + nextVy * kernel.TickSeconds;
                float nextX = x + kernel.ForwardSpeed * kernel.TickSeconds;
                float low = Math.Min(y, nextY);
                float high = Math.Max(y, nextY);
                if (TryColumnAfter(runout, x, out GateColumn from) == false
                    || TryColumnAfter(runout, nextX, out GateColumn to) == false)
                {
                    //  관문 뒤로 볼 수 있는 지형이 없다 — 없는 것을 막혔다고도 안 막혔다고도
                    //  할 수 없으니 여기서 멈춘다(그만큼 낙관적이다. 리포트가 그 사실을 적는다).
                    return true;
                }
                if (CeilingBlocks(low, high, kernel.BodyHeight, from.Windows)
                    || CeilingBlocks(low, high, kernel.BodyHeight, to.Windows))
                {
                    return false;
                }
                x = nextX;
                y = nextY;
                vy = nextVy;
            }
            return true;
        }

        /// <summary>이 훑기 구간이 <b>천장</b>에 막히나. 창 안이면 거짓이고, 창 밖이어도 <i>바닥</i>
        /// 쪽으로 벗어난 것이면 거짓이다 — 날갯짓이 위로 올려 주므로 피할 수 있는 막힘이다.
        /// 들어갈 창이 아예 없는 열(통째로 지형)은 피할 길이 없으므로 참이다.</summary>
        public static bool CeilingBlocks(float lowY, float highY, float bodyHeight,
                                         IReadOnlyList<GateWindow> windows)
        {
            if (SpanFits(lowY, highY, bodyHeight, windows))
            {
                return false;
            }
            //  가장 적게 벗어난 창을 골라 어느 쪽으로 벗어났는지 본다 — 재생(GateFunnelReplayRule)이
            //  막은 자리를 고르는 셈과 같다.
            float bestPenalty = float.MaxValue;
            bool ceiling = false;
            for (int i = 0; windows != null && i < windows.Count; i++)
            {
                float below = windows[i].Bottom - lowY;
                float above = highY + bodyHeight - windows[i].Top;
                float penalty = Math.Max(below, 0f) + Math.Max(above, 0f);
                if (penalty >= bestPenalty)
                {
                    continue;
                }
                bestPenalty = penalty;
                ceiling = above > below;
            }
            return bestPenalty == float.MaxValue || ceiling;
        }

        //  관문 뒤 열 중 이 x의 것. 볼 수 있는 자리를 벗어났으면 거짓 — 마지막 열을 늘여 붙여
        //  없는 지형을 지어내지 않는다(ColumnAt은 범위 밖을 끝 열로 눌러 버린다).
        static bool TryColumnAfter(IReadOnlyList<GateColumn> runout, float x, out GateColumn column)
        {
            column = default;
            if (runout == null || runout.Count == 0)
            {
                return false;
            }
            float step = runout.Count > 1 ? runout[1].X - runout[0].X : 0f;
            if (x > runout[runout.Count - 1].X + step * 0.5f)
            {
                return false;
            }
            column = ColumnAt(runout, x);
            return true;
        }

        //  마지막 한 틱(lastFlap)을 얹고 부모를 따라 거슬러 올라가 조작열을 시간순으로 편다.
        static void Unwind(List<int> parent, List<bool> flapOf, int node, bool lastFlap, List<bool> into)
        {
            into.Add(lastFlap);
            for (int n = node; n >= 0; n = parent[n])
            {
                into.Add(flapOf[n]);
            }
            into.Reverse();
        }

        /// <summary>입구 격자를 훑어 깔때기를 낸다 — 높이 한 줄마다 통과하는 세로속도의 범위.</summary>
        public static List<FunnelRow> Funnel(IReadOnlyList<GateColumn> columns,
                                             IReadOnlyList<GateColumn> runout, in FlightKernel kernel,
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
                    bool pass = Rolls(y, vy, columns, runout, kernel);
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
            text.AppendLine("   날갯짓은 마음대로 넣어 본다 — 즉 <이상적인 조종>이다.)");
            text.AppendLine($"  (깔때기의 지평은 관문 끝이 아니라 <확정된 아치의 끝>이다 — 날갯짓은 세로속도를 덮어써");
            text.AppendLine($"   아치를 확정하므로, 관문을 나설 때 아직 올라가는 중이면 아치가 끝날 때까지"
                          + $" {kernel.ArcTicks}틱({kernel.ArcDistance:F2}m)을 더 굴려 본다.");
            text.AppendLine("   그래서 관문은 지나고 그 뒤에서 박는 궤적은 통과로 세지 않는다 — 2026-09-12 이전에는");
            text.AppendLine("   관문 끝에서 눈을 감아, x 80.0~83.5를 지나 x=84.61에서 박는 궤적을 통과로 셌다.)");
            text.AppendLine("  ⚠️ 남은 낙관 하나: 창은 <점>으로 잰 것이라 기운 면에서 몸을 구가 아니라 세로 막대로 본다.");
            text.AppendLine("     기울기 m인 면에서 반지름 r인 구에 필요한 세로 여유는 r이 아니라 r·√(1+m²)이므로,");
            text.AppendLine("     창이 r(√(1+m²)−1)만큼 넓게 잡힌다 — 이 맵 x≈84.5의 천장(m=0.77, r=0.45)에서 0.11m다.");
            text.AppendLine("     관문 안(천장이 평평한 구간)에서는 안 보이고, 기운 천장이 있는 관문 뒤에서만 나타난다.");
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
            AppendRunoutNote(text, gate, kernel);
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
            AppendVerdict(text, gate, kernel);
        }

        //  숫자만 늘어놓고 해석을 사람에게 맡기면, 읽는 사람마다 다른 결론을 낸다(그래서 이 절이
        //  이미 답한 것을 두 번 손으로 재는 일이 생겼다). <b>겹침 여부와 깔때기 안/밖을 여기서
        //  계산해 찍는다</b> — 손으로 적는 문장이 아니다.
        static void AppendVerdict(StringBuilder text, GateReport gate, in FlightKernel kernel)
        {
            int passCount = 0;
            int failCount = 0;
            float passMin = float.MaxValue, passMax = float.MinValue;
            float failMin = float.MaxValue, failMax = float.MinValue;
            int failInFunnel = 0;
            //  깔때기 <b>밖</b>이라 지형 탓인 진입 상태들 — 처방은 정확히 이것들을 안으로
            //  들이는 데 얼마가 드나를 잰다(깔때기 안인 것은 이미 지형이 허락한 상태다).
            var outside = new List<GateEntry>();
            for (int i = 0; i < gate.Crossings.Count; i++)
            {
                GateCrossing c = gate.Crossings[i];
                if (c.Outcome == GateOutcome.Passed)
                {
                    passCount++;
                    passMin = Math.Min(passMin, c.EntryVerticalSpeed);
                    passMax = Math.Max(passMax, c.EntryVerticalSpeed);
                }
                else if (c.Outcome == GateOutcome.Stopped)
                {
                    failCount++;
                    failMin = Math.Min(failMin, c.EntryVerticalSpeed);
                    failMax = Math.Max(failMax, c.EntryVerticalSpeed);
                    //  깔때기 안인가 = 그 진입 상태에서 <b>이상적인 조종</b>이면 지나갈 수 있었나.
                    //  깔때기 표를 다시 읽지 않고 같은 함수로 직접 묻는다 — 표는 y를 0.25m 격자로
                    //  반올림한 것이라, 실제 진입 y로 물어야 그 비행에 대한 답이 된다.
                    if (Rolls(c.EntryY, c.EntryVerticalSpeed, gate.Columns, gate.Runout, kernel))
                    {
                        failInFunnel++;
                    }
                    else
                    {
                        outside.Add(new GateEntry(c.EntryY, c.EntryVerticalSpeed));
                    }
                }
            }
            if (passCount == 0 && failCount == 0)
            {
                return;
            }
            //  범위는 늘 작은 값부터 큰 값 순으로 적는다 — 통과/실패 두 줄의 읽는 방향이 달라지면
            //  둘을 겹쳐 보기 어렵다.
            if (passCount > 0)
            {
                text.AppendLine($"    통과 {passCount}개: 진입 vy {passMin:+0.0;-0.0}~{passMax:+0.0;-0.0} {Sense(passMin, passMax)}");
            }
            if (failCount > 0)
            {
                bool overlap = passCount > 0 && passMin <= failMax && failMin <= passMax;
                string overlapNote = passCount == 0 ? string.Empty : overlap ? "   겹침 있음" : "   겹침 없음";
                text.AppendLine($"    실패 {failCount}개: 진입 vy {failMin:+0.0;-0.0}~{failMax:+0.0;-0.0} {Sense(failMin, failMax)}{overlapNote}");
            }
            if (passCount > 0 && failCount > 0)
            {
                text.AppendLine($"    → {Divide(passMin, passMax, failMin, failMax)}");
            }
            if (failCount > 0)
            {
                text.AppendLine($"      {Blame(failCount, failInFunnel)}");
                AppendPrescription(text, gate, kernel, outside, failCount, failInFunnel);
            }
        }

        //  처방 훑기의 눈금과 끝. 눈금이 곧 답의 정밀도라 리포트가 그 값을 함께 적는다.
        const float DividerStep = 0.2f;
        const float CeilingStep = 0.25f;
        const float CeilingCap = 8f;
        const float SlopeStep = 0.02f;
        //  이보다 완만한 천장은 "기운 천장"으로 안 본다 — 평평한 천장에서 기울기를 줄이라는
        //  처방은 아무것도 안 바꾸는 줄이 된다.
        const float SlopeFloor = 0.05f;

        //  "막혔다"까지만 적고 끝내면 디자이너가 무엇을 얼마나 바꿔야 할지 모른다. 지형으로
        //  판정된 진입 상태들을 <b>깔때기 안으로 들이는 데 드는 최소 Δ</b>를 편집 가지마다 잰다.
        static void AppendPrescription(StringBuilder text, GateReport gate, in FlightKernel kernel,
                                       List<GateEntry> outside, int failCount, int failInFunnel)
        {
            if (outside.Count == 0)
            {
                text.AppendLine("      → 지형은 충분하다 — 봇이 그 상태에서 못 고른다."
                              + " (처방 없음: 맵이 아니라 봇 문제다.)");
                return;
            }
            float pivotX = gate.Columns != null && gate.Columns.Count > 0 ? gate.Columns[0].X : 0f;
            var faceWindows = gate.Columns != null && gate.FaceIndex >= 0
                              && gate.FaceIndex < gate.Columns.Count
                ? gate.Columns[gate.FaceIndex].Windows
                : Array.Empty<GateWindow>();
            //  기울기는 관문과 그 뒤를 <b>이어서</b> 잰다 — 범인이 관문 뒤 천장인 자리가 있어서다.
            var span = new List<GateColumn>();
            if (gate.Columns != null)
            {
                span.AddRange(gate.Columns);
            }
            if (gate.Runout != null)
            {
                span.AddRange(gate.Runout);
            }
            float thickness = GatePrescriptionRule.DividerThickness(faceWindows);
            float descent = GatePrescriptionRule.CeilingDescent(span);

            string which = failInFunnel == 0
                ? $"지형으로 판정된 {failCount}개"
                : $"깔때기 밖인 {outside.Count}개";
            text.AppendLine($"      → 처방 ({which}를 깔때기 안으로 들이려면):");
            if (thickness > 0f)
            {
                var scan = GatePrescriptionRule.WithBaseline(
                    GatePrescriptionRule.Scan(outside, gate.Columns, gate.Runout, kernel,
                                              GateEditKind.DividerThin, pivotX,
                                              DividerStep, thickness),
                    thickness);
                text.AppendLine($"         · {GatePrescriptionRule.Line(scan)}");
            }
            var raise = GatePrescriptionRule.Scan(outside, gate.Columns, gate.Runout, kernel,
                                                  GateEditKind.CeilingRaise, pivotX,
                                                  CeilingStep, CeilingCap);
            text.AppendLine($"         · {GatePrescriptionRule.Line(raise)}");
            //  천장 처방이 어떤 Δ에서도 0이면 "이 자리는 가망 없다"로 읽히기 쉽다 — 실은 손잡이를
            //  잘못 잡은 것이다. 그 구별을 리포트가 직접 적는다(실측으로 x 80.0~83.5가 그 경우였다).
            if (raise.Best == 0)
            {
                text.AppendLine("           (천장 처방이 어떤 Δ에서도 0인 것은 <가망 없다>가 아니라 <손잡이가 아니다>는 뜻이다:");
                text.AppendLine("            \"천장을 올린다\"는 열마다 <가장 높은 창>의 천장만 올리므로, 새가 칸막이 아래 칸에");
                text.AppendLine("            갇혀 있으면 아무것도 안 바뀐다. 그 자리에서 듣는 손잡이는 칸막이 쪽이다.)");
            }
            if (descent >= SlopeFloor)
            {
                var slope = GatePrescriptionRule.WithBaseline(
                    GatePrescriptionRule.Scan(outside, gate.Columns, gate.Runout, kernel,
                                              GateEditKind.CeilingSlopeEase, pivotX,
                                              SlopeStep, descent),
                    descent);
                text.AppendLine($"         · {GatePrescriptionRule.Line(slope)}");
            }
            else
            {
                text.AppendLine($"         · (천장 기울기 처방 없음 — 관문과 그 뒤를 이어 재니"
                              + $" x 1m당 {descent:F2}m로 거의 평평하다)");
            }
            text.AppendLine("         ⚠️ 이 숫자는 <가상 변경>으로 잰 것이다 — 창 목록을 Δ만큼 옮겨"
                          + " 굴려 봤을 뿐 씬은 안 고쳤다. 실제로 그 콜라이더를 옮기면 이어진 지형·");
            text.AppendLine("            옆 관문·②-b 풍차 밴드가 따라 움직일 수 있으니, 고친 뒤 다시 재야 한다.");
            text.AppendLine("         ⚠️ 기울기 처방에는 위 머리말의 낙관 0.11m가 그대로 걸린다 —"
                          + " 기운 천장에서 창이 그만큼 넓게 잡히므로,");
            text.AppendLine("            실제로는 여기 적힌 Δ보다 조금 더 필요할 수 있다.");
            text.AppendLine("         ⚠️ 여기 숫자는 전부 <깔때기 판정>이다(이상적인 조종이면 지나나)."
                          + " 진짜 커널로 조작열을 전수로 뒤져 본 것은");
            text.AppendLine("            x≈82의 진입 상태 <하나>뿐이다 — 그 하나는 40틱 안에 x=87까지"
                          + " 가는 무충돌 조작열이 없음이 확인됐다.");
        }

        //  이 범위가 올라가는 중인지 떨어지는 중인지. 0을 걸치면 둘이 섞인 것이라 그렇게 적는다.
        static string Sense(float min, float max)
            => min > 0f ? "(올라가는 중)" : max < 0f ? "(떨어지는 중)" : "(오르내림이 섞여 있다)";

        //  통과와 실패를 진입 vy가 가르나. 겹치면 <b>안 가른다</b>고 적어야 한다 — 겹치는데도
        //  "올라가며 들어가야 한다"고 적으면 거짓이다.
        static string Divide(float passMin, float passMax, float failMin, float failMax)
        {
            if (passMin > failMax)
            {
                return passMin > 0f
                    ? "이 관문은 <올라가며 들어가야> 지난다."
                    : "이 관문은 <덜 떨어지며 들어가야> 지난다.";
            }
            if (passMax < failMin)
            {
                return passMax < 0f
                    ? "이 관문은 <내려가며 들어가야> 지난다."
                    : "이 관문은 <덜 올라가며 들어가야> 지난다.";
            }
            return "진입 vy만으로는 갈리지 않는다 — 같은 vy로 통과한 비행과 실패한 비행이 둘 다 있다.";
        }

        //  지형 탓인가 겨냥 탓인가. 실패한 진입 상태가 깔때기 <b>안</b>이면 이상적인 조종으로는
        //  지날 수 있었다는 뜻이라 겨냥 문제고, <b>밖</b>이면 어떻게 조종해도 못 지나므로 지형이다.
        //  <b>이 판정은 지평이 확정된 아치의 끝까지 늘어난 뒤에야 건전하다.</b> 관문 끝에서 눈을
        //  감던 시절(~2026-09-12)에는 관문을 나선 뒤 박는 궤적을 통과로 세어 "깔때기 안"이 거짓이
        //  될 수 있었고, 그래서 그때는 안쪽에 책임을 묻지 않았다. 지금은 아치가 끝날 때까지 보므로
        //  안쪽도 다시 근거가 된다 — 남은 낙관(기운 천장의 0.11m)은 절 머리말이 밝힌다.
        static string Blame(int failCount, int inFunnel)
        {
            if (inFunnel == 0)
            {
                return $"실패 {failCount}개의 진입 상태는 모두 깔때기 밖이므로 겨냥이 아니라 지형이 원인이다.";
            }
            if (inFunnel == failCount)
            {
                return $"실패 {failCount}개의 진입 상태는 모두 깔때기 안이므로 지형이 아니라 겨냥이 원인이다.";
            }
            return $"실패 {failCount}개 중 {inFunnel}개는 깔때기 안이므로 겨냥이 원인이고,"
                 + $" 나머지 {failCount - inFunnel}개는 깔때기 밖이므로 지형이 원인이다.";
        }

        //  관문 뒤로 <b>얼마나</b> 볼 수 있었나. 아치보다 짧으면 그 너머는 안 본 것이라 그만큼
        //  낙관적이다 — 조용히 두면 "지평을 늘렸다"는 말이 그 관문에서는 거짓이 된다.
        static void AppendRunoutNote(StringBuilder text, GateReport gate, in FlightKernel kernel)
        {
            float seen = gate.Runout == null || gate.Runout.Count == 0
                ? 0f
                : gate.Runout[gate.Runout.Count - 1].X - gate.EndX;
            if (seen + 1e-4f >= kernel.ArcDistance)
            {
                text.AppendLine($"    관문 뒤 {seen:F2}m까지 함께 본다 — 아치({kernel.ArcDistance:F2}m)가 다 들어간다.");
                return;
            }
            text.AppendLine($"    ⚠️ 관문 뒤로 볼 수 있는 지형이 {seen:F2}m뿐이다(아치 {kernel.ArcDistance:F2}m보다 짧다)"
                          + " — 그 너머에서 박는 궤적은 여기서도 통과로 센다.");
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
