using System;
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>지형으로 판정된 관문에 <b>무엇을 얼마나</b> 바꾸면 되나 — 시험해 볼 편집 한 가지.</summary>
    public enum GateEditKind
    {
        /// <summary>회랑을 가로로 가르는 칸막이를 Δm만큼 얇게. 가운데를 고정하고 양쪽으로 똑같이
        /// 깎으므로, Δ가 두께에 닿으면 두 창이 하나로 합쳐진다.</summary>
        DividerThin,
        /// <summary>관문과 그 뒤 구간의 천장을 Δm만큼 위로. 열마다 <b>가장 높은 창의 천장</b>만
        /// 올린다 — 칸막이는 그대로 둔다.</summary>
        CeilingRaise,
        /// <summary>천장의 하강 기울기를 x 1m당 Δ만큼 줄인다. 관문 입구를 축으로 삼으므로 입구에서는
        /// 그대로고, 앞으로 갈수록 Δ·(x−입구)만큼 천장이 올라간다.</summary>
        CeilingSlopeEase,
    }

    /// <summary>깔때기에 물어볼 진입 상태 하나 — 봇이 <b>실제로 도착한</b> 높이와 세로속도다.</summary>
    public readonly struct GateEntry
    {
        public readonly float Y;
        public readonly float VerticalSpeed;

        public GateEntry(float y, float verticalSpeed)
        {
            Y = y;
            VerticalSpeed = verticalSpeed;
        }
    }

    /// <summary>사다리 한 칸 — 이 Δ에서 몇 개가 깔때기 안이 되었나.</summary>
    public readonly struct PrescriptionRung
    {
        public readonly float Delta;
        public readonly int InFunnel;

        public PrescriptionRung(float delta, int inFunnel)
        {
            Delta = delta;
            InFunnel = inFunnel;
        }
    }

    /// <summary>편집 한 가지를 Δ로 훑어 본 결과.</summary>
    public readonly struct PrescriptionScan
    {
        /// <summary>"그런 Δ는 못 찾았다". Δ는 늘 0 이상이라 음수가 명확한 빈 값이다.</summary>
        public const float None = -1f;

        public readonly GateEditKind Kind;
        /// <summary>고치기 전 값 — 칸막이 두께(m), 천장 기울기(m/m). 천장 올리기엔 뜻이 없어 0이다.</summary>
        public readonly float Baseline;
        public readonly float Step;
        public readonly float Cap;
        public readonly int Total;
        /// <summary>훑은 Δ 안에서 가장 많이 들어온 수(= Cap에서의 수, 단조라서 같다).</summary>
        public readonly int Best;
        /// <summary><b>전부</b>가 깔때기 안이 되는 최소 Δ. 못 찾았으면 <see cref="None"/>.</summary>
        public readonly float DeltaForAll;
        /// <summary><b>절반 이상</b>이 들어오는 최소 Δ. 못 찾았으면 <see cref="None"/>.</summary>
        public readonly float DeltaForHalf;
        /// <summary>수가 <b>바뀐</b> Δ만. Δ=0의 칸이 늘 맨 앞이다.</summary>
        public readonly IReadOnlyList<PrescriptionRung> Ladder;
        /// <summary>굴려 보기가 상태 한도에 걸려 <b>더 못 잰</b> 채로 멈췄나. 참이면
        /// <see cref="Cap"/> 위의 Δ는 "안 된다"가 아니라 <b>모른다</b>는 뜻이다.</summary>
        public readonly bool GaveUp;

        public PrescriptionScan(GateEditKind kind, float baseline, float step, float cap, int total,
                                int best, float deltaForAll, float deltaForHalf,
                                IReadOnlyList<PrescriptionRung> ladder, bool gaveUp = false)
        {
            Kind = kind;
            Baseline = baseline;
            Step = step;
            Cap = cap;
            Total = total;
            Best = best;
            DeltaForAll = deltaForAll;
            DeltaForHalf = deltaForHalf;
            Ladder = ladder ?? Array.Empty<PrescriptionRung>();
            GaveUp = gaveUp;
        }

        /// <summary>이 Δ가 찾아진 값인가. <see cref="None"/>과 비교하는 자리를 한 군데로 모은다.</summary>
        public static bool Found(float delta) => delta >= 0f;
    }

    /// <summary>
    /// <b>처방</b> — 지형이 막았다고 판정된 관문에 "무엇을 얼마나 바꾸면 봇이 실제로 도착하는
    /// 진입 상태가 깔때기 안에 들어오나"를 숫자로 낸다.
    ///
    /// <para><b>왜 씬을 안 고치나.</b> 씬을 고쳐 재려면 콜라이더를 옮기고 다시 훑고 되돌려야 하는데,
    /// 되돌리기가 한 번이라도 어긋나면 맵 파일이 조용히 바뀐다. 여기서는 <b>자유공간 판정이 낸 창
    /// 목록</b>(<see cref="GateColumn"/>)을 Δ만큼 옮긴 사본으로 바꿔 굴려 볼 뿐이라, 씬은 열지도
    /// 않는다. 대신 <b>가상 변경</b>이라는 한계가 생긴다 — 실제로 그 콜라이더를 옮기면 이어진
    /// 지형·옆 관문·도는 지형이 따라 움직일 수 있다(리포트가 그 주의를 적는다).</para>
    ///
    /// <para><b>왜 단조인가.</b> 세 편집 모두 <b>자유공간을 넓히기만 한다</b> — 칸막이는 얇아지고,
    /// 천장은 올라간다. 넓어진 공간에서는 원래 되던 조작열이 그대로 되므로, Δ가 커질 때 깔때기 안에
    /// 들어온 진입 상태가 다시 나갈 수 없다. 그래서 "이미 들어온 것은 다시 안 묻는다"는 지름길이
    /// 안전하고, 최소 Δ를 눈금 위에서 한 번만 훑어 찾을 수 있다.</para>
    /// </summary>
    public static class GatePrescriptionRule
    {
        //  창 두 개가 이만큼 가까우면 붙은 것으로 본다 — 칸막이를 완전히 없앤 Δ에서 두 창이
        //  부동소수 오차만큼 떨어져 남는 것을 막는다.
        const float MergeEpsilon = 1e-4f;

        // ── 지형을 Δ만큼 가상으로 옮긴다 ────────────────────────────────────

        /// <summary>한 열의 창 목록에 편집을 먹인다. <paramref name="x"/>는 기울기 편집에만 쓴다.</summary>
        public static List<GateWindow> Edit(IReadOnlyList<GateWindow> windows, GateEditKind kind,
                                            float delta, float x, float pivotX)
        {
            var result = new List<GateWindow>();
            if (windows == null || windows.Count == 0)
            {
                return result;
            }
            if (delta <= 0f)
            {
                result.AddRange(windows);
                return result;
            }
            switch (kind)
            {
                case GateEditKind.DividerThin:
                    return ThinDividers(windows, delta);
                case GateEditKind.CeilingRaise:
                    return RaiseTop(windows, delta);
                default:
                    //  입구보다 뒤(x < pivot)는 안 건드린다 — 기울기를 "여기서부터" 줄이는 편집이라
                    //  축에서는 0이고 앞으로 갈수록 커진다.
                    return RaiseTop(windows, delta * Math.Max(0f, x - pivotX));
            }
        }

        /// <summary>열 전체에 편집을 먹인 사본.</summary>
        public static List<GateColumn> Edit(IReadOnlyList<GateColumn> columns, GateEditKind kind,
                                            float delta, float pivotX)
        {
            var result = new List<GateColumn>();
            for (int i = 0; columns != null && i < columns.Count; i++)
            {
                result.Add(new GateColumn(columns[i].X,
                                          Edit(columns[i].Windows, kind, delta, columns[i].X, pivotX)));
            }
            return result;
        }

        //  칸막이마다 가운데를 고정하고 양쪽을 δ/2씩 깎는다. 두께보다 큰 Δ는 두께까지만 먹으므로
        //  창이 서로를 뚫고 지나가지 않는다.
        static List<GateWindow> ThinDividers(IReadOnlyList<GateWindow> windows, float delta)
        {
            int n = windows.Count;
            var bottom = new float[n];
            var top = new float[n];
            for (int i = 0; i < n; i++)
            {
                bottom[i] = windows[i].Bottom;
                top[i] = windows[i].Top;
            }
            for (int i = 0; i + 1 < n; i++)
            {
                float thickness = windows[i + 1].Bottom - windows[i].Top;
                if (thickness <= 0f)
                {
                    continue;
                }
                float shrink = Math.Min(delta, thickness) * 0.5f;
                top[i] += shrink;
                bottom[i + 1] -= shrink;
            }
            var merged = new List<GateWindow>();
            int start = 0;
            while (start < n)
            {
                int end = start;
                while (end + 1 < n && bottom[end + 1] <= top[end] + MergeEpsilon)
                {
                    end++;
                }
                merged.Add(new GateWindow(bottom[start], top[end],
                                          windows[start].Floor, windows[end].Ceiling));
                start = end + 1;
            }
            return merged;
        }

        //  가장 높은 창의 천장만 올린다 — 그 위엔 아무것도 없으므로 다른 창과 부딪히지 않는다.
        static List<GateWindow> RaiseTop(IReadOnlyList<GateWindow> windows, float lift)
        {
            var result = new List<GateWindow>(windows);
            int last = result.Count - 1;
            result[last] = new GateWindow(result[last].Bottom, result[last].Top + lift,
                                          result[last].Floor, result[last].Ceiling);
            return result;
        }

        // ── 고치기 전 값 재기 ───────────────────────────────────────────────

        /// <summary>이 열에서 <b>가장 두꺼운</b> 칸막이. 칸막이가 없으면 0이다.</summary>
        public static float DividerThickness(IReadOnlyList<GateWindow> windows)
        {
            float thickest = 0f;
            for (int i = 0; windows != null && i + 1 < windows.Count; i++)
            {
                float thickness = windows[i + 1].Bottom - windows[i].Top;
                if (thickness > thickest)
                {
                    thickest = thickness;
                }
            }
            return thickest;
        }

        /// <summary>천장이 x 1m당 얼마나 <b>내려오나</b>. 양수면 내려오는 것이고, 올라가거나 평평하면
        /// 0 이하다. 양 끝 열의 <b>가장 높은 창의 천장</b> 두 점으로 잰다 — 그 사이에서 천장이
        /// 갈아타면 이 값은 그 구간의 평균 기울기라는 뜻이다.</summary>
        public static float CeilingDescent(IReadOnlyList<GateColumn> span)
        {
            int first = -1, last = -1;
            for (int i = 0; span != null && i < span.Count; i++)
            {
                if (span[i].Windows == null || span[i].Windows.Count == 0)
                {
                    continue;
                }
                if (first < 0)
                {
                    first = i;
                }
                last = i;
            }
            if (first < 0 || last <= first)
            {
                return 0f;
            }
            float run = span[last].X - span[first].X;
            if (run <= 0f)
            {
                return 0f;
            }
            float high = span[first].Windows[span[first].Windows.Count - 1].Top;
            float low = span[last].Windows[span[last].Windows.Count - 1].Top;
            return (high - low) / run;
        }

        // ── 훑기 ────────────────────────────────────────────────────────────

        /// <summary>이 Δ에서 진입 상태 몇 개가 깔때기 안인가. <paramref name="already"/>가 있으면
        /// <b>이미 들어온 것은 다시 안 묻는다</b> — 단조라서 답이 바뀔 수 없다(클래스 주석 참고).</summary>
        public static int CountInFunnel(IReadOnlyList<GateEntry> entries,
                                        IReadOnlyList<GateColumn> columns,
                                        IReadOnlyList<GateColumn> runout,
                                        in FlightKernel kernel, bool[] already)
            => CountInFunnel(entries, columns, runout, kernel, already, out _);

        /// <summary>위와 같은데, 굴려 보기가 상태 한도에 걸려 <b>못 잰</b> 진입 상태가 하나라도
        /// 있었는지 함께 낸다. 그 경우 이 수는 "안 들어온 것"이 아니라 <b>모르는 것</b>을 섞고
        /// 있으므로, 부르는 쪽은 답이라고 적으면 안 된다.</summary>
        public static int CountInFunnel(IReadOnlyList<GateEntry> entries,
                                        IReadOnlyList<GateColumn> columns,
                                        IReadOnlyList<GateColumn> runout,
                                        in FlightKernel kernel, bool[] already, out bool gaveUp)
        {
            int count = 0;
            gaveUp = false;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                bool inside = already != null && already[i];
                if (inside == false)
                {
                    inside = GateFunnelRule.TryRolls(entries[i].Y, entries[i].VerticalSpeed,
                                                     columns, runout, kernel, out _, out bool one);
                    gaveUp |= one;
                    if (already != null)
                    {
                        already[i] = inside;
                    }
                }
                if (inside)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Δ를 0부터 <paramref name="cap"/>까지 <paramref name="step"/> 눈금으로 훑는다.
        /// 전부 들어온 Δ에서 멈춘다 — 단조라서 그 뒤는 볼 것이 없다.</summary>
        public static PrescriptionScan Scan(IReadOnlyList<GateEntry> entries,
                                            IReadOnlyList<GateColumn> columns,
                                            IReadOnlyList<GateColumn> runout,
                                            in FlightKernel kernel, GateEditKind kind,
                                            float pivotX, float step, float cap)
        {
            int total = entries == null ? 0 : entries.Count;
            var ladder = new List<PrescriptionRung>();
            if (total == 0 || step <= 0f)
            {
                return new PrescriptionScan(kind, 0f, step, cap, total, 0,
                                            PrescriptionScan.None, PrescriptionScan.None, ladder);
            }
            //  실제로 답을 얻은 마지막 Δ. 상태 한도에 걸려 멈추면 끝(cap)이 아니라 여기까지가
            //  "훑은 범위"다 — 안 그러면 못 잰 구간을 훑었다고 적게 된다.
            float measuredTo = 0f;
            bool gaveUp = false;
            //  "절반"은 올림이다 — 12개의 절반은 6개고, 5개의 절반은 3개다(모자란 쪽으로 세면
            //  "절반이 들어온다"가 거짓이 된다).
            int half = (total + 1) / 2;
            var already = new bool[total];
            int best = 0;
            float deltaForAll = PrescriptionScan.None;
            float deltaForHalf = PrescriptionScan.None;
            for (float delta = 0f; delta <= cap + step * 0.5f; delta += step)
            {
                var editedColumns = Edit(columns, kind, delta, pivotX);
                var editedRunout = Edit(runout, kind, delta, pivotX);
                int inside = CountInFunnel(entries, editedColumns, editedRunout, kernel, already,
                                           out bool oneGaveUp);
                if (oneGaveUp)
                {
                    //  이 Δ의 수는 "못 들어온 것"과 "못 잰 것"이 섞여 있다 — 적지 않고 멈춘다.
                    //  더 큰 Δ는 창이 더 넓어 상태가 더 터지므로 그 위도 못 잰다.
                    gaveUp = true;
                    break;
                }
                measuredTo = delta;
                if (inside > best || ladder.Count == 0)
                {
                    ladder.Add(new PrescriptionRung(delta, inside));
                }
                if (inside > best)
                {
                    best = inside;
                }
                if (PrescriptionScan.Found(deltaForHalf) == false && inside >= half)
                {
                    deltaForHalf = delta;
                }
                if (inside >= total)
                {
                    deltaForAll = delta;
                    break;
                }
            }
            return new PrescriptionScan(kind, 0f, step, measuredTo, total, best,
                                        deltaForAll, deltaForHalf, ladder, gaveUp);
        }

        /// <summary>고치기 전 값을 얹은 사본. 훑기 자체는 그 값을 모르고, 글로 적을 때만 필요하다.</summary>
        public static PrescriptionScan WithBaseline(in PrescriptionScan scan, float baseline)
            => new PrescriptionScan(scan.Kind, baseline, scan.Step, scan.Cap, scan.Total, scan.Best,
                                    scan.DeltaForAll, scan.DeltaForHalf, scan.Ladder, scan.GaveUp);

        // ── 글 ──────────────────────────────────────────────────────────────

        /// <summary>편집 한 가지를 한 줄로. 들여쓰기는 부르는 쪽이 붙인다.</summary>
        public static string Line(in PrescriptionScan scan)
        {
            var text = new StringBuilder();
            text.Append(Change(scan, PrescriptionScan.Found(scan.DeltaForAll)
                                     ? scan.DeltaForAll : scan.Cap));
            if (PrescriptionScan.Found(scan.DeltaForAll))
            {
                text.Append($": {scan.Total}개 중 {scan.Total}개가 깔때기 안");
            }
            else
            {
                text.Append($": 훑은 Δ 안에서는 전부 들어오는 Δ가 없다"
                          + $" — 가장 많아야 {scan.Total}개 중 {scan.Best}개");
            }
            if (scan.GaveUp)
            {
                text.Append($"  ⚠️ Δ {Amount(scan, scan.Cap + scan.Step)} 위로는 <못 쟀다>"
                          + " — 창이 넓어져 굴려 보기가 상태 한도에 걸렸다(안 된다는 뜻이 아니다)");
            }
            text.Append(PrescriptionScan.Found(scan.DeltaForHalf)
                ? $" (절반 {(scan.Total + 1) / 2}개는 {Amount(scan, scan.DeltaForHalf)})"
                : " (절반도 못 채운다)");
            text.Append($"  [Δ 눈금 {Amount(scan, scan.Step)}, Δ ≤ {Amount(scan, scan.Cap)}까지 훑음]");
            text.Append("  사다리 ");
            for (int i = 0; i < scan.Ladder.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(" · ");
                }
                text.Append($"{Amount(scan, scan.Ladder[i].Delta)}→{scan.Ladder[i].InFunnel}개");
            }
            return text.ToString();
        }

        //  "무엇을 얼마로" 한 토막. 고치기 전 값이 있으면 <b>결과 값</b>까지 적는다 — 디자이너가
        //  옮겨 적을 숫자는 Δ가 아니라 "0.77 → 0.61" 쪽이다.
        static string Change(in PrescriptionScan scan, float delta)
        {
            switch (scan.Kind)
            {
                case GateEditKind.DividerThin:
                    return $"칸막이를 {scan.Baseline:F2}m → {Math.Max(0f, scan.Baseline - delta):F2}m 로 줄이면"
                         + $" (Δ {delta:F2}m)";
                case GateEditKind.CeilingRaise:
                    return $"관문과 그 뒤 천장을 {delta:F2}m 올리면";
                default:
                    return $"천장 기울기를 {scan.Baseline:F2} → {Math.Max(0f, scan.Baseline - delta):F2} m/m 로 낮추면"
                         + $" (Δ {delta:F2} m/m)";
            }
        }

        //  Δ의 단위. 기울기만 m/m이고 나머지는 m다.
        static string Amount(in PrescriptionScan scan, float delta)
            => scan.Kind == GateEditKind.CeilingSlopeEase ? $"{delta:F2} m/m" : $"{delta:F2}m";
    }
}
