using System;
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>한 x에서 잰 빈 띠 하나 — 아래 끝과 위 끝, 그리고 그 끝을 막고 있는 것의 이름.</summary>
    public readonly struct FreeBand
    {
        public readonly float Bottom;
        public readonly float Top;
        /// <summary>아래를 막고 있는 것. <see cref="OpenBelow"/>면 아무것도 없다.</summary>
        public readonly string Floor;
        /// <summary>위를 막고 있는 것. <see cref="OpenAbove"/>면 아무것도 없다.</summary>
        public readonly string Ceiling;
        /// <summary>아래가 안 막혀 있다 — 맵 밑 허공까지 이어진다.</summary>
        public readonly bool OpenBelow;
        /// <summary>위가 안 막혀 있다 — 열린 하늘까지 이어진다.</summary>
        public readonly bool OpenAbove;

        public float Height => Top - Bottom;
        /// <summary>위아래가 다 막힌 띠. 좁힘을 말할 수 있는 것은 이것뿐이다.</summary>
        public bool Enclosed => OpenBelow == false && OpenAbove == false;

        public FreeBand(float bottom, float top, string floor, string ceiling,
                        bool openBelow, bool openAbove)
        {
            Bottom = bottom;
            Top = top;
            Floor = floor;
            Ceiling = ceiling;
            OpenBelow = openBelow;
            OpenAbove = openAbove;
        }
    }

    /// <summary>코스의 한 x를 세로로 훑어 접은 값 — <b>위아래가 다 막힌 띠 중 가장 넓은 것</b>.</summary>
    public readonly struct PinchColumn
    {
        public readonly float X;
        /// <summary>가장 넓은 갇힌 띠의 높이. 갇힌 띠가 하나도 없으면 0이다
        /// (<see cref="Open"/>이 참이면 "트여 있어서 0", 거짓이면 "통째로 막혀서 0").</summary>
        public readonly float Band;
        public readonly float BandBottom;
        public readonly float BandTop;
        public readonly string Floor;
        public readonly string Ceiling;
        /// <summary>갇힌 띠가 <b>하나도 없는데</b> 트인 띠는 있다 — 하늘이나 맵 밑 허공뿐이라
        /// 좁힐 것 자체가 없다.
        /// <para><b>0m와 반드시 구분해야 한다</b>: 트인 것(통과 가능)과 통째로 막힌 것(통과 불가)이
        /// 둘 다 "갇힌 띠 0개"로 나오므로, 이 구분이 없으면 열린 하늘을 0m 벽으로 보고한다.</para></summary>
        public readonly bool Open;

        public PinchColumn(float x, float band, float bandBottom, float bandTop,
                           string floor, string ceiling, bool open)
        {
            X = x;
            Band = band;
            BandBottom = bandBottom;
            BandTop = bandTop;
            Floor = floor;
            Ceiling = ceiling;
            Open = open;
        }
    }

    /// <summary>밴드가 기준에 못 미치는 x들을 이어 붙인 구간 하나.</summary>
    public readonly struct StaticPinch
    {
        public readonly float StartX;
        public readonly float EndX;
        /// <summary>구간 안에서 <b>가장 좁은</b> 밴드. 한 자리만 좁아도 거기서 막히므로 최솟값이다.</summary>
        public readonly float Band;
        /// <summary>그 가장 좁은 밴드가 있던 세로 위치.</summary>
        public readonly float BandBottom;
        public readonly float BandTop;
        public readonly string Floor;
        public readonly string Ceiling;

        public float Length => EndX - StartX;

        public StaticPinch(float startX, float endX, float band, float bandBottom, float bandTop,
                           string floor, string ceiling)
        {
            StartX = startX;
            EndX = endX;
            Band = band;
            BandBottom = bandBottom;
            BandTop = bandTop;
            Floor = floor;
            Ceiling = ceiling;
        }
    }

    /// <summary>
    /// 회랑이 <b>갈린</b> 구간 하나 — 한 열에 몸이 들어가는 창이 둘 이상인 x들이 이어진 곳.
    /// <para><see cref="StaticPinch"/>와 <b>다른 축</b>이다. 좁힘은 "폭이 모자라나"를 묻고,
    /// 갈림은 "폭은 넉넉한데 자리가 갈라졌나"를 묻는다. 그래서 등급(✅/⚠️/❌)을 따로 주지
    /// 않는다 — 등급은 여전히 가장 넓은 창 기준이고, 여기는 <b>사실 하나를 덧붙일</b> 뿐이다.</para>
    /// </summary>
    public readonly struct StaticSplit
    {
        public readonly float StartX;
        public readonly float EndX;
        /// <summary>구간의 <b>얼굴 열</b>에서 몸이 들어가는 창의 수. 둘 이상이라 갈림이다.</summary>
        public readonly int WindowCount;
        /// <summary>그 얼굴 열의 창들(몸이 들어가는 것만, 아래에서 위로).</summary>
        public readonly IReadOnlyList<GateWindow> Windows;
        /// <summary>창과 창 사이를 막은 것 중 <b>가장 얇은</b> 것의 두께. 이게 칸막이다.</summary>
        public readonly float Divider;
        /// <summary>그 칸막이의 이름. 고칠 곳을 짚으려면 이름이 있어야 한다.</summary>
        public readonly string DividerName;
        /// <summary>구간이 덮는 x 길이. 표본 간격만큼 보수적으로 센다(<see cref="StaticPinch"/>와 같은 셈).</summary>
        public readonly float Length;

        public StaticSplit(float startX, float endX, int windowCount, IReadOnlyList<GateWindow> windows,
                           float divider, string dividerName, float length)
        {
            StartX = startX;
            EndX = endX;
            WindowCount = windowCount;
            Windows = windows;
            Divider = divider;
            DividerName = dividerName;
            Length = length;
        }
    }

    /// <summary>좁힘 하나의 등급. 셋은 뜻이 다르다 — 뭉치면 고칠 곳을 잘못 짚는다.</summary>
    public enum PinchGrade
    {
        /// <summary><b>보장</b>. 밴드가 아치+몸보다 넓어 어디서 어떻게 와도 높이를 유지하며 지난다.</summary>
        Guaranteed,
        /// <summary><b>가능하지만 보장은 아니다</b>. 좁지만 짧아서 밑에서 올라가며 타고 넘을 수 있다.</summary>
        Possible,
        /// <summary><b>통과 불가</b>. 좁은데 길어서 어떤 진입으로도 못 지난다.</summary>
        Impossible,
    }

    /// <summary>좁힘 하나에 대한 판정.</summary>
    public readonly struct PinchVerdict
    {
        public readonly PinchGrade Grade;
        /// <summary>이 밴드로 타고 넘을 수 있는 최대 길이(m). 몸도 안 들어가면 0이다.</summary>
        public readonly float MaxLength;
        /// <summary>밴드를 이만큼 넓히면 ✅가 된다. 이미 ✅면 0이다.</summary>
        public readonly float WidenBy;
        /// <summary>구간을 이만큼 줄이면 ⚠️가 된다. ❌가 아니면 0이다.</summary>
        public readonly float ShortenBy;

        public PinchVerdict(PinchGrade grade, float maxLength, float widenBy, float shortenBy)
        {
            Grade = grade;
            MaxLength = maxLength;
            WidenBy = widenBy;
            ShortenBy = shortenBy;
        }
    }

    /// <summary>
    /// <b>돌지 않는 지형</b>이 코스를 얼마나 좁히는지를 산술로 판정한다. 규칙은 ②-b(회전 장애물)와
    /// 같은 불변식에서 출발한다 — <b>밴드 ≥ 날갯짓 아치 + 몸 높이</b>. 플래피의 날갯짓은 크기가
    /// 하나뿐이라 그보다 좁은 띠 안에서는 아예 누를 수가 없다.
    ///
    /// <para><b>정적에만 있는 조건 하나</b>: 좁아도 <i>짧으면</i> 타고 넘을 수 있다. 새가 띠 바닥에서
    /// 위로 올라가며 들어가 포물선을 그리고 나오면 된다. 쓸 수 있는 세로 여유가
    /// <c>R = 밴드 − 몸높이</c>일 때 대칭 포물선의 최대 체공은 <c>t = √(8R/중력)</c>이고,
    /// 그동안 전진하는 거리 <c>L_max = 전진속도 × t</c>가 통과 가능한 최대 길이다.</para>
    ///
    /// <para><b>이 판정이 보장하지 않는 것</b>: 밴드가 <i>있다</i>는 것과 새가 <i>거기 도달할 수
    /// 있다</i>는 것은 다르다. 도달 가능성은 봇 비행(①)이 답한다.</para>
    /// </summary>
    public static class StaticPinchRule
    {
        /// <summary>밴드가 이만큼은 돼야 보장이다 — ②-b와 <b>같은 기준</b>을 그대로 쓴다.</summary>
        public static float RequiredBand(float flapImpulse, float gravity, float tickSeconds, float bodyHeight)
            => ObstaclePlacementRule.RequiredBand(flapImpulse, gravity, tickSeconds, bodyHeight);

        /// <summary>
        /// 이 밴드에서 타고 넘을 수 있는 최대 구간 길이(m).
        /// <para>밴드가 <b>몸 높이보다도 좁으면</b> 쓸 수 있는 세로 여유 R이 음수가 된다 — 몸조차
        /// 안 들어간다는 뜻이므로 <b>0을 돌려준다</b>(어떤 길이도 못 지난다).</para>
        /// </summary>
        public static float MaxLength(float band, float bodyHeight, float forwardSpeed, float gravity)
        {
            float slack = band - bodyHeight;
            if (slack <= 0f || gravity <= 0f || forwardSpeed <= 0f)
            {
                return 0f;
            }
            return forwardSpeed * (float)Math.Sqrt(8.0 * slack / gravity);
        }

        public static PinchVerdict Judge(in StaticPinch pinch, float requiredBand, float bodyHeight,
                                         float forwardSpeed, float gravity)
        {
            float maxLength = MaxLength(pinch.Band, bodyHeight, forwardSpeed, gravity);
            //  딱 기준만큼이면 보장이다 — 그 폭에서는 아치가 밴드 안에 정확히 들어간다.
            if (pinch.Band >= requiredBand)
            {
                return new PinchVerdict(PinchGrade.Guaranteed, maxLength, 0f, 0f);
            }
            float widenBy = requiredBand - pinch.Band;
            if (pinch.Length <= maxLength)
            {
                return new PinchVerdict(PinchGrade.Possible, maxLength, widenBy, 0f);
            }
            return new PinchVerdict(PinchGrade.Impossible, maxLength, widenBy, pinch.Length - maxLength);
        }

        /// <summary>
        /// 세로로 훑어 나온 띠들을 한 x의 값 하나로 접는다 — <b>위아래가 다 막힌 띠 중 가장 넓은 것</b>.
        /// <para>하늘이나 맵 밑 허공으로 트인 띠는 세지 않는다. 거기엔 천장이 없어 좁힐 것 자체가
        /// 없기 때문이다. 다만 그런 띠가 있다고 해서 그 x를 통과로 치지는 <b>않는다</b> — 같은 x에
        /// 갇힌 회랑이 따로 있으면 그게 좁힘이다(맵 위쪽이 하늘로 트여 있어도 아래 회랑은 좁다).</para>
        /// <para>갇힌 띠가 하나도 없을 때만 <c>Open</c>을 세운다 — "트여서 잴 것이 없다"와
        /// "통째로 막혔다"를 반드시 갈라야 한다(안 그러면 열린 하늘을 0m 벽으로 보고한다).</para>
        /// <para>한 x에 갇힌 띠가 여럿이면 <b>가장 넓은 것</b>을 쓴다 — 우회는 한쪽만 되면 되므로.
        /// 새가 그 띠에 도달할 수 있는지는 이 검사가 답하지 않는다(①이 답한다).</para>
        /// </summary>
        public static PinchColumn Column(float x, IReadOnlyList<FreeBand> bands)
        {
            bool sawOpen = false;
            bool sawEnclosed = false;
            float best = 0f;
            float bottom = 0f;
            float top = 0f;
            string floor = null;
            string ceiling = null;
            for (int i = 0; bands != null && i < bands.Count; i++)
            {
                FreeBand band = bands[i];
                if (band.Enclosed == false)
                {
                    sawOpen = true;
                    continue;
                }
                if (sawEnclosed == false || band.Height > best)
                {
                    best = band.Height;
                    bottom = band.Bottom;
                    top = band.Top;
                    floor = band.Floor;
                    ceiling = band.Ceiling;
                }
                sawEnclosed = true;
            }
            return new PinchColumn(x, best, bottom, top, floor, ceiling,
                                   open: sawEnclosed == false && sawOpen);
        }

        /// <summary>
        /// 기준에 못 미치는 x들을 <b>연속 구간</b>으로 묶는다.
        /// <para>길이는 표본 간격만큼 <b>보수적으로</b> 잡는다 — 표본 하나가 덮는 칸이 간격 하나이므로,
        /// 표본 n개짜리 구간의 길이는 <c>n × 간격</c>이다. 표본 하나뿐인 구간을 길이 0으로 세면
        /// 무조건 ⚠️가 되어 실제보다 낙관적인 답을 준다.</para>
        /// </summary>
        public static List<StaticPinch> Segments(IReadOnlyList<PinchColumn> columns, float requiredBand,
                                                 float sampleStep)
        {
            var pinches = new List<StaticPinch>();
            if (columns == null)
            {
                return pinches;
            }
            int runStart = -1;
            for (int i = 0; i <= columns.Count; i++)
            {
                bool pinched = i < columns.Count
                            && columns[i].Open == false
                            && columns[i].Band < requiredBand;
                if (pinched)
                {
                    if (runStart < 0)
                    {
                        runStart = i;
                    }
                    continue;
                }
                if (runStart < 0)
                {
                    continue;
                }
                pinches.Add(Fold(columns, runStart, i - 1, sampleStep));
                runStart = -1;
            }
            return pinches;
        }

        private static StaticPinch Fold(IReadOnlyList<PinchColumn> columns, int first, int last,
                                        float sampleStep)
        {
            int narrowest = first;
            for (int i = first + 1; i <= last; i++)
            {
                if (columns[i].Band < columns[narrowest].Band)
                {
                    narrowest = i;
                }
            }
            PinchColumn worst = columns[narrowest];
            return new StaticPinch(columns[first].X - sampleStep * 0.5f,
                                   columns[last].X + sampleStep * 0.5f,
                                   worst.Band, worst.BandBottom, worst.BandTop,
                                   worst.Floor, worst.Ceiling);
        }

        /// <summary>
        /// 회랑이 <b>갈린</b> 구간들을 찾는다 — 좁힘과 달리 폭이 아니라 <b>자리 수</b>를 센다.
        ///
        /// <para><b>무엇을 갈림으로 세는가</b> (이 셋이 정의다):</para>
        /// <para>① 한 열에서 <b>몸이 통째로 들어가는</b> 창이 둘 이상이면 그 열은 갈렸다.
        /// 몸이 안 들어가는 틈은 새가 고를 수 있는 자리가 아니므로 창으로 세지 않는다 —
        /// 안 그러면 손가락만 한 틈 하나가 회랑을 "갈린 것"으로 만든다.</para>
        /// <para>② 갈린 열이 <b>연달아</b> 이어진 만큼이 한 구간이다. 경계에서 창이 붙었다
        /// 갈렸다 하면 거기서 구간이 끊기고 다음 구간이 새로 시작한다 — 끊긴 것을 억지로
        /// 잇지 않는다(이었다가는 회랑 절반이 한 구간이 된다).</para>
        /// <para>③ 그렇게 이어진 길이가 <paramref name="minLength"/>보다 짧으면 <b>버린다</b>.
        /// 표본 한 칸만 갈린 것은 창이 붙는 경계를 스친 것이지 가로막은 칸막이가 아니다.
        /// 길이는 좁힘과 같은 셈으로 보수적으로 잡는다 — 표본 n개짜리 구간은 n×간격.</para>
        ///
        /// <para><b>얼굴 열</b>(찍어 보여 줄 열)은 ①의 관문 절과 <b>같은 규칙</b>으로 고른다
        /// (<see cref="GateFunnelRule.FaceColumn"/> — 가장 좋은 선택지가 가장 나쁜 열).
        /// 둘이 다른 열을 고르면 같은 갈림을 두 절이 다른 그림으로 말하게 된다.</para>
        /// </summary>
        public static List<StaticSplit> Splits(IReadOnlyList<GateColumn> columns, float bodyHeight,
                                               float sampleStep, float minLength)
        {
            var splits = new List<StaticSplit>();
            if (columns == null)
            {
                return splits;
            }
            int runStart = -1;
            for (int i = 0; i <= columns.Count; i++)
            {
                bool split = i < columns.Count && PassableCount(columns[i].Windows, bodyHeight) >= 2;
                if (split)
                {
                    if (runStart < 0)
                    {
                        runStart = i;
                    }
                    continue;
                }
                if (runStart < 0)
                {
                    continue;
                }
                int last = i - 1;
                float length = (last - runStart + 1) * sampleStep;
                if (length >= minLength)
                {
                    splits.Add(FoldSplit(columns, runStart, last, bodyHeight, length));
                }
                runStart = -1;
            }
            return splits;
        }

        /// <summary>
        /// 갈림 목록에서 <b>별개 공간</b>을 뺀다 — 칸막이가 <paramref name="maxDivider"/>(아치+몸)보다
        /// 두꺼운 것들이다.
        ///
        /// <para><b>왜 두꺼우면 갈림이 아닌가.</b> 그만큼 두꺼우면 한쪽 창에서 다른 창으로 옮겨 갈
        /// 일이 없다 — 날갯짓 한 번이 그리는 아치에 몸 높이를 더해도 칸막이를 넘지 못하므로, 새는
        /// 자기가 들어간 창 안에서만 논다. 그러면 그 둘은 <b>같은 회랑의 두 길이 아니라 서로 다른
        /// 공간</b>이다. 갈림이 묻는 것은 "여기서 어느 창을 골랐느냐가 통과를 가르나"인데, 다른
        /// 공간은 애초에 고를 수 있는 선택지가 아니다.</para>
        ///
        /// <para>실측에서 이것이 잡음이었다: 갈림 14곳 중 셋의 칸막이가 28.1m / 22.8m / 11.0m로,
        /// 회랑이 갈린 게 아니라 코스 위아래로 멀리 떨어진 별개 공간이었다.</para>
        /// </summary>
        public static List<StaticSplit> WithoutSeparateSpaces(IReadOnlyList<StaticSplit> splits,
                                                              float maxDivider, out int removed)
        {
            removed = 0;
            var kept = new List<StaticSplit>();
            for (int i = 0; splits != null && i < splits.Count; i++)
            {
                if (splits[i].Divider > maxDivider)
                {
                    removed++;
                    continue;
                }
                kept.Add(splits[i]);
            }
            return kept;
        }

        static int PassableCount(IReadOnlyList<GateWindow> windows, float bodyHeight)
        {
            int count = 0;
            for (int i = 0; windows != null && i < windows.Count; i++)
            {
                if (windows[i].Height >= bodyHeight)
                {
                    count++;
                }
            }
            return count;
        }

        static StaticSplit FoldSplit(IReadOnlyList<GateColumn> columns, int first, int last,
                                     float bodyHeight, float length)
        {
            var run = new List<GateColumn>(last - first + 1);
            for (int i = first; i <= last; i++)
            {
                run.Add(columns[i]);
            }
            int face = GateFunnelRule.FaceColumn(run, bodyHeight);
            var windows = new List<GateWindow>();
            var faceWindows = run[face < 0 ? 0 : face].Windows;
            for (int i = 0; faceWindows != null && i < faceWindows.Count; i++)
            {
                if (faceWindows[i].Height >= bodyHeight)
                {
                    windows.Add(faceWindows[i]);
                }
            }
            windows.Sort((a, b) => a.Bottom.CompareTo(b.Bottom));
            //  가장 얇은 칸막이를 고른다 — 창이 셋 이상이면 그중 제일 얇은 것이 "여기가 갈렸다"의
            //  가장 정직한 두께다. 이름은 아래 창의 천장(= 칸막이 밑면)을 먼저 본다.
            float divider = 0f;
            string dividerName = null;
            for (int i = 0; i + 1 < windows.Count; i++)
            {
                float gap = windows[i + 1].Bottom - windows[i].Top;
                if (i == 0 || gap < divider)
                {
                    divider = gap;
                    dividerName = string.IsNullOrEmpty(windows[i].Ceiling)
                        ? windows[i + 1].Floor
                        : windows[i].Ceiling;
                }
            }
            return new StaticSplit(columns[first].X, columns[last].X, windows.Count, windows,
                                   divider, dividerName, length);
        }

        /// <summary>리포트의 "②-c 정적 좁힘" 절 전체(머리말 줄 포함, 끝에 줄바꿈 없음).</summary>
        public static string Section(IReadOnlyList<StaticPinch> pinches, float requiredBand,
                                     float bodyHeight, float forwardSpeed, float gravity,
                                     float sampleStep, IReadOnlyList<StaticSplit> splits = null,
                                     int separateSpaces = 0)
        {
            var text = new StringBuilder();
            text.AppendLine("── ②-c 정적 좁힘 ──────────────────────");
            text.AppendLine($"  (돌지 않는 지형이 코스를 얼마나 좁히는지 — 밴드가 아치+몸({requiredBand:F2}m)보다 좁은 x를");
            text.AppendLine("   이어 붙인 구간이다. ②-b와 같은 자로 재고 같은 기준을 쓴다.)");
            text.AppendLine($"  (좁아도 *짧으면* 타고 넘을 수 있다: 쓸 수 있는 세로 여유 R = 밴드 − 몸높이({bodyHeight:F2}m)일 때");
            text.AppendLine($"   대칭 포물선의 최대 체공이 t=√(8R/{gravity:F0})이라 통과 가능 길이는 L_max = {forwardSpeed:F0}×t다.)");
            text.AppendLine($"  (x는 {sampleStep:F2}m 간격으로 훑고 구간 길이는 그만큼 보수적으로 잡는다 — 간격보다 짧은 좁힘은");
            text.AppendLine("   놓칠 수 있다. 위아래가 다 막힌 띠만 센다: 하늘이나 맵 밑 허공으로 트인 띠에는 천장이 없어");
            text.AppendLine("   좁힐 것이 없다. 풍차는 빼고 잰다 — 돌아가는 것은 ②-b가 본다.)");

            if (pinches == null || pinches.Count == 0)
            {
                text.AppendLine("  좁은 구간 없음 — 코스 전체에서 아치+몸이 들어간다");
                AppendSplits(text, splits, separateSpaces);
                text.Append(Caveats(splits));
                return text.ToString();
            }

            int impossible = 0;
            int possible = 0;
            int guaranteed = 0;
            for (int i = 0; i < pinches.Count; i++)
            {
                PinchGrade grade = Judge(pinches[i], requiredBand, bodyHeight, forwardSpeed, gravity).Grade;
                if (grade == PinchGrade.Impossible)
                {
                    impossible++;
                }
                else if (grade == PinchGrade.Possible)
                {
                    possible++;
                }
                else
                {
                    guaranteed++;
                }
            }
            text.AppendLine($"  좁은 구간 {pinches.Count}개 — ❌ 통과 불가 {impossible}개 · ⚠️ 가능하나 보장 아님 {possible}개"
                          + $" · ✅ 보장 {guaranteed}개");

            for (int i = 0; i < pinches.Count; i++)
            {
                StaticPinch pinch = pinches[i];
                PinchVerdict verdict = Judge(pinch, requiredBand, bodyHeight, forwardSpeed, gravity);
                string mark = verdict.Grade == PinchGrade.Guaranteed ? "✅"
                            : verdict.Grade == PinchGrade.Possible ? "⚠️" : "❌";
                string band = pinch.Band > 0f
                    ? $"가장 넓은 밴드 {pinch.Band:F2}m (y {pinch.BandBottom:F1}~{pinch.BandTop:F1})"
                    : "자유 밴드 없음 — 이 구간은 통째로 막혀 있다";
                text.AppendLine($"  {mark} x {pinch.StartX:F1}~{pinch.EndX:F1} (길이 {pinch.Length:F1}m)  {band}"
                              + $"  L_max {verdict.MaxLength:F2}m");
                text.AppendLine($"     막는 것: 밑 {Describe(pinch.Floor)} · 위 {Describe(pinch.Ceiling)}");
                if (verdict.Grade == PinchGrade.Guaranteed)
                {
                    continue;
                }
                text.AppendLine($"     → 밴드를 {verdict.WidenBy:F2}m 넓히면 ✅");
                if (verdict.Grade == PinchGrade.Impossible)
                {
                    text.AppendLine($"     → 또는 구간을 {verdict.ShortenBy:F2}m 줄이면 ⚠️ (길이 {verdict.MaxLength:F2}m 이하)");
                }
            }
            AppendSplits(text, splits, separateSpaces);
            text.Append(Caveats(splits));
            return text.ToString();
        }

        //  갈림은 등급이 아니라 <b>덧붙이는 사실</b>이다 — 위의 ✅/⚠️/❌는 여전히 "가장 넓은 창"
        //  기준 그대로 두고, 여기서 "그 넓은 창이 사실 둘로 갈려 있다"를 따로 찍는다.
        private static void AppendSplits(StringBuilder text, IReadOnlyList<StaticSplit> splits,
                                         int separateSpaces)
        {
            int count = splits == null ? 0 : splits.Count;
            //  하나도 안 남았어도 <b>거른 개수가 있으면</b> 한 줄은 찍는다 — 아무 줄도 없으면
            //  "갈림을 안 쟀다"로 읽힌다.
            if (count == 0 && separateSpaces == 0)
            {
                return;
            }
            string excluded = separateSpaces > 0 ? $" (별개 공간 {separateSpaces}곳 제외)" : string.Empty;
            text.AppendLine($"  갈림 {count}곳{excluded} — 창이 둘 이상이라 폭이 넉넉해도 \"어느 창이냐\"가 통과를 가른다");
            if (separateSpaces > 0)
            {
                text.AppendLine("     (뺀 것: 칸막이가 아치+몸보다 두꺼워 한쪽 창에서 다른 창으로 옮겨 갈 수 없는 것들 —"
                              + " 같은 회랑의 두 길이 아니라 서로 다른 공간이다)");
            }
            for (int i = 0; i < count; i++)
            {
                StaticSplit split = splits[i];
                var spans = new StringBuilder();
                for (int w = 0; split.Windows != null && w < split.Windows.Count; w++)
                {
                    if (w > 0)
                    {
                        spans.Append(" / ");
                    }
                    spans.Append($"{split.Windows[w].Bottom:F2}~{split.Windows[w].Top:F2}");
                }
                text.AppendLine($"  ⚠ x {split.StartX:F1}~{split.EndX:F1}  창 {split.WindowCount}개"
                              + $"  (y {spans})  칸막이 {split.Divider:F1}m");
                text.AppendLine($"     막는 것: {Describe(split.DividerName)}");
            }
        }

        private static string Describe(string name) => string.IsNullOrEmpty(name) ? "(모름)" : name;

        //  갈림 주의는 갈림이 있을 때만 붙인다 — 없는데 붙이면 읽는 사람이 "어디에 갈림이
        //  있다는 거지?" 하고 절을 다시 훑게 된다.
        private static string Caveats(IReadOnlyList<StaticSplit> splits)
        {
            var text = new StringBuilder();
            text.AppendLine("  (주의: 밴드가 있다는 것과 새가 거기 도달할 수 있다는 것은 다르다 — 이 검사는 앞엣것만 본다.");
            text.AppendLine("   도달 가능성은 ① 봇 비행이 답한다. ⚠️는 특히 그렇다: 창이 회랑 한참 아래에 있으면");
            text.AppendLine("   새가 애초에 거기 못 갈 수 있다.)");
            if (splits == null || splits.Count == 0)
            {
                text.Append("  (주의: L_max는 창 바닥에서 정확한 속도로 올라가며 들어가는 *이상적인 진입*을 전제한다 — 실제로는 더 어렵다.)");
                return text.ToString();
            }
            text.AppendLine("  (주의: L_max는 창 바닥에서 정확한 속도로 올라가며 들어가는 *이상적인 진입*을 전제한다 — 실제로는 더 어렵다.)");
            text.Append("  (주의: 폭이 넉넉해도 갈림은 어렵다 — 도착할 때 이미 맞는 창에 있어야 한다."
                      + " 어느 창인지는 ① 진단의 관문 통과·깔때기가 말해 준다.)");
            return text.ToString();
        }
    }
}
