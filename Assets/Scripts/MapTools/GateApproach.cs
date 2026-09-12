using System;
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>관문 앞 한 틱에 봇이 <b>무엇을 보고 무엇을 골랐나</b>. 기반 정책의 답과 굴려 보기
    /// 두 갈래의 점수를 한자리에 모은 것이라, "왜 안 눌렀나"를 이 한 줄로 물을 수 있다.</summary>
    public readonly struct ApproachSample
    {
        /// <summary>비행 안의 순번(0부터). 위상이 아니라 <b>그 비행의</b> 틱이다.</summary>
        public readonly int Tick;
        public readonly float X;
        public readonly float Y;
        public readonly float VerticalSpeed;
        /// <summary>바닥 규칙이 이 틱에 누르고 싶어 했나(<see cref="BotDecision.WantsFlap"/>).</summary>
        public readonly bool WantsFlap;
        /// <summary>아치 훑기가 "지금 누르면 올라가다 박는다"고 하지 <b>않았나</b>. false면
        /// 누르는 쪽이 애초에 후보가 아니었다 — 그 틱은 굴려 보지도 않는다.</summary>
        public readonly bool CeilingSafe;
        /// <summary>실제로 두 갈래를 굴려서 정했나. <see cref="CeilingSafe"/>가 false면 false다.</summary>
        public readonly bool RolledOut;
        /// <summary>누르는 갈래가 몇 틱 살았고 어디까지 갔나. <see cref="RolledOut"/>이 false면 뜻 없다.</summary>
        public readonly int FlapAliveTicks;
        public readonly float FlapReachX;
        /// <summary>안 누르는 갈래. 같은 단서.</summary>
        public readonly int CoastAliveTicks;
        public readonly float CoastReachX;
        /// <summary>그래서 이 틱에 실제로 눌렀나.</summary>
        public readonly bool Flapped;

        public ApproachSample(int tick, float x, float y, float verticalSpeed,
                              bool wantsFlap, bool ceilingSafe, bool rolledOut,
                              int flapAliveTicks, float flapReachX,
                              int coastAliveTicks, float coastReachX, bool flapped)
        {
            Tick = tick;
            X = x;
            Y = y;
            VerticalSpeed = verticalSpeed;
            WantsFlap = wantsFlap;
            CeilingSafe = ceilingSafe;
            RolledOut = rolledOut;
            FlapAliveTicks = flapAliveTicks;
            FlapReachX = flapReachX;
            CoastAliveTicks = coastAliveTicks;
            CoastReachX = coastReachX;
            Flapped = flapped;
        }
    }

    /// <summary>한 틱이 어느 모양에 해당하나. 셋이 후보 가설이고 넷째는 "이 틱은 실패의 설명이
    /// 아니다"(실제로 눌렀다)이다.</summary>
    public enum ApproachShape
    {
        /// <summary>천장 가드가 "누르기"를 후보에서 뺐다 — 굴려 보지도 않았다.</summary>
        GuardBlocked,
        /// <summary>후보는 둘인데 누르는 갈래도 관문 얼굴에 닿기 전에 죽는다 — 아치로도 창에 못 닿는다.</summary>
        FlapDiesShort,
        /// <summary>두 갈래 점수가 사실상 같다 — 평가가 둘을 구별하지 못해 기반 정책 그대로 갔다.</summary>
        Tie,
        /// <summary>누르는 갈래가 더 나쁘게 나왔다(덜 살거나 덜 갔다).</summary>
        FlapWorse,
        /// <summary>이 틱엔 실제로 눌렀다. 실패를 설명하는 틱이 아니다.</summary>
        Flapped,
    }

    /// <summary>관문 앞 창(窓) 하나를 세어 본 결과.</summary>
    public readonly struct ApproachTally
    {
        public readonly int Total;
        public readonly int GuardBlocked;
        public readonly int FlapDiesShort;
        public readonly int Tie;
        public readonly int FlapWorse;
        public readonly int Flapped;

        /// <summary>"평가가 구별 못 한다"는 동률과 "누르면 더 나쁘다"를 합친 것이다 — 둘 다
        /// 굴려 봤는데도 누르는 쪽이 안 뽑힌 경우라 같은 가설을 가리킨다.</summary>
        public int Undecided => Tie + FlapWorse;
        /// <summary>실패를 설명할 수 있는 틱 수 = 안 누른 틱 전부.</summary>
        public int NotFlapped => Total - Flapped;

        public ApproachTally(int total, int guardBlocked, int flapDiesShort, int tie, int flapWorse,
                             int flapped)
        {
            Total = total;
            GuardBlocked = guardBlocked;
            FlapDiesShort = flapDiesShort;
            Tie = tie;
            FlapWorse = flapWorse;
            Flapped = flapped;
        }

        public ApproachTally Add(in ApproachTally other)
            => new ApproachTally(Total + other.Total, GuardBlocked + other.GuardBlocked,
                                 FlapDiesShort + other.FlapDiesShort, Tie + other.Tie,
                                 FlapWorse + other.FlapWorse, Flapped + other.Flapped);
    }

    /// <summary>한 비행이 한 관문 앞에서 무엇을 했나 — 표 한 덩이.</summary>
    public sealed class GateApproach
    {
        public string Name;
        /// <summary>관문 입구에서의 발 높이·세로 속도(<see cref="GateCrossing"/>과 같은 값).</summary>
        public float EntryY;
        public float EntryVerticalSpeed;
        /// <summary>그 진입 상태가 깔때기 <b>안</b>이었나 — 이상적인 조종이면 지날 수 있었나.</summary>
        public bool InFunnel;
        /// <summary>관문에 들어간 뒤 몇 틱 만에 멈췄나.</summary>
        public int TicksAfterEntry;
        /// <summary>관문 진입 전 마지막 K틱. 오래된 것부터.</summary>
        public IReadOnlyList<ApproachSample> Samples = Array.Empty<ApproachSample>();
    }

    /// <summary>
    /// <b>관문 앞 판단</b> — 봇이 관문에 막히기 직전 몇 틱 동안 무엇을 보고 무엇을 골랐는지 부검한다.
    ///
    /// <para>①의 깔때기(<see cref="GateFunnelRule"/>)가 "그 진입 상태에서도 <i>이상적인 조종</i>이면
    /// 지난다"고 답하고 나면, 남는 질문은 하나다 — <b>봇의 60틱 전방탐색은 왜 그 날갯짓을 안 고르나.</b>
    /// 관문을 지나려면 약 17틱(3.7m) 전에 눌러야 하니 시야 안인데도 안 누른다.</para>
    ///
    /// <para><b>이 절은 재기만 한다.</b> 규칙을 손으로 고치는 것은 여기서 하지 않는다 — 겨냥 규칙을
    /// 짐작으로 고쳤다가 도달이 28%에서 3%로 떨어진 적이 있다. 먼저 숫자로 어느 가설인지 정한다.</para>
    ///
    /// <para><b>세 가설</b>과 기록에 나타나는 모양:</para>
    /// <para>① <b>천장 가드가 막는다</b> — 결정 틱 대부분에서 누르기가 아예 후보가 아니다.</para>
    /// <para>② <b>이미 너무 낮다</b> — 후보는 둘인데 "누르면"도 관문 얼굴에 닿기 전에 죽는다.</para>
    /// <para>③ <b>평가가 구별 못 한다</b> — 두 갈래 점수가 같거나 "누르면"이 더 나쁘게 나온다.</para>
    /// </summary>
    public static class GateApproachRule
    {
        /// <summary>관문 앞 몇 틱을 볼 것인가. 날갯짓 아치가 17틱이라 30틱이면 아치 두 번에 가까워
        /// "눌렀어야 할 자리"를 넉넉히 덮는다.</summary>
        public const int WindowTicks = 30;

        /// <summary>관문 진입 <b>전</b> 마지막 <paramref name="windowTicks"/>틱을 뽑는다. 진입 뒤는
        /// 안 본다 — 이미 창을 고른 뒤의 틱은 "왜 그 창을 못 골랐나"에 답하지 못한다.</summary>
        public static List<ApproachSample> Window(IReadOnlyList<ApproachSample> samples,
                                                  float gateStartX, int windowTicks)
        {
            var window = new List<ApproachSample>();
            if (samples == null || windowTicks < 1)
            {
                return window;
            }
            int entry = -1;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].X >= gateStartX)
                {
                    entry = i;
                    break;
                }
            }
            //  관문에 못 와 본 비행은 "관문 앞 판단"이 없다 — 앞에서 이미 멈춘 것이다.
            if (entry < 0)
            {
                return window;
            }
            int first = entry - windowTicks;
            if (first < 0)
            {
                first = 0;
            }
            for (int i = first; i < entry; i++)
            {
                window.Add(samples[i]);
            }
            return window;
        }

        /// <summary>한 틱이 어느 모양인가. <paramref name="faceX"/>는 관문의 <b>얼굴</b> 열 —
        /// 누르는 갈래가 거기 닿기 전에 죽으면 "아치로도 창에 못 닿는다"는 뜻이다.
        ///
        /// <para><b>순서가 곧 뜻이다</b>(한 틱이 여러 모양에 걸릴 수 있어서다): 가드가 막았으면
        /// 굴려 본 값 자체가 없으므로 가드가 먼저고, 실제로 누른 틱은 실패의 설명이 아니므로
        /// 그다음이며, 그 뒤에야 "눌러도 못 닿나"와 "평가가 갈랐나"를 본다.</para></summary>
        public static ApproachShape Classify(in ApproachSample sample, float faceX,
                                             float sameReachEpsilon)
        {
            if (sample.CeilingSafe == false)
            {
                return ApproachShape.GuardBlocked;
            }
            if (sample.Flapped)
            {
                return ApproachShape.Flapped;
            }
            if (sample.RolledOut && sample.FlapReachX < faceX)
            {
                return ApproachShape.FlapDiesShort;
            }
            if (sample.FlapAliveTicks != sample.CoastAliveTicks)
            {
                return ApproachShape.FlapWorse;
            }
            return Math.Abs(sample.FlapReachX - sample.CoastReachX) > sameReachEpsilon
                ? ApproachShape.FlapWorse
                : ApproachShape.Tie;
        }

        /// <summary>창 하나를 모양별로 센다.</summary>
        public static ApproachTally Tally(IReadOnlyList<ApproachSample> samples, float faceX,
                                          float sameReachEpsilon)
        {
            int guard = 0, dies = 0, tie = 0, worse = 0, flapped = 0;
            for (int i = 0; samples != null && i < samples.Count; i++)
            {
                switch (Classify(samples[i], faceX, sameReachEpsilon))
                {
                    case ApproachShape.GuardBlocked: guard++; break;
                    case ApproachShape.FlapDiesShort: dies++; break;
                    case ApproachShape.Tie: tie++; break;
                    case ApproachShape.FlapWorse: worse++; break;
                    default: flapped++; break;
                }
            }
            return new ApproachTally(samples == null ? 0 : samples.Count,
                                     guard, dies, tie, worse, flapped);
        }

        /// <summary>센 것을 한 문장으로. <b>섞여 있으면 비율을 적고</b>, 안 누른 틱이 아예 없으면
        /// 모른다고 적는다 — 짐작으로 하나를 고르지 않는다.</summary>
        public static string Verdict(in ApproachTally tally)
        {
            if (tally.Total == 0)
            {
                return "잴 것이 없다 — 관문 앞 틱을 하나도 못 모았다.";
            }
            if (tally.NotFlapped == 0)
            {
                return $"안 누른 틱이 없다({tally.Total}틱 모두 눌렀다) — 세 가설 중 어느 것도 "
                     + "이 관문을 설명하지 못한다. 관문 <안>에서 무엇이 일어났는지를 따로 재야 한다.";
            }
            int guard = tally.GuardBlocked;
            int dies = tally.FlapDiesShort;
            int undecided = tally.Undecided;
            int top = Math.Max(guard, Math.Max(dies, undecided));
            var parts = new List<string>();
            if (guard > 0)
            {
                parts.Add($"천장 가드가 막음 {guard}틱");
            }
            if (dies > 0)
            {
                parts.Add($"눌러도 관문 전에 죽음 {dies}틱");
            }
            if (undecided > 0)
            {
                parts.Add($"평가가 구별 못 함 {undecided}틱(동률 {tally.Tie} + 누르면 더 나쁨 {tally.FlapWorse})");
            }
            string mix = string.Join(" · ", parts);
            string share = $"안 누른 {tally.NotFlapped}틱 중 {mix}";
            if (top == guard && guard > undecided && guard > dies)
            {
                return $"① 천장 가드가 막는다 — {share}.";
            }
            if (top == dies && dies > guard && dies > undecided)
            {
                return $"② 이미 너무 낮다 — {share}.";
            }
            if (top == undecided && undecided > guard && undecided > dies)
            {
                return $"③ 평가가 구별 못 한다 — {share}.";
            }
            return $"하나로 안 갈린다 — {share}. 가장 많은 모양이 둘 이상 동률이라 더 재야 한다.";
        }

        /// <summary>리포트의 "① 진단 — 관문 앞 판단" 절 전체(머리말 포함, 끝에 줄바꿈 없음).</summary>
        /// <param name="tableLimit">표(틱 줄)를 몇 비행까지 찍을 것인가. 나머지는 요약 한 줄만
        /// 찍는다 — 열두 비행의 30틱을 다 찍으면 리포트가 표로 뒤덮인다.</param>
        public static string Section(float gateStartX, float gateEndX, float faceX,
                                     IReadOnlyList<GateApproach> approaches,
                                     float sameReachEpsilon, int tableLimit)
        {
            var text = new StringBuilder();
            text.AppendLine("── ① 진단 — 관문 앞 판단 ───────────────");
            text.AppendLine("  (관문 바로 앞에서 봇이 무엇을 보고 무엇을 골랐나. 깔때기가 \"이상적인 조종이면");
            text.AppendLine("   지난다\"고 한 뒤 남는 질문 — 앞을 내다보는 전방탐색은 왜 그 날갯짓을 안 고르나.)");
            text.AppendLine("  (\"누르면\"/\"안누르면\" = 그 갈래를 창 끝까지 굴려 본 결과다: 몇 틱 살았고 어디까지 갔나.");
            text.AppendLine("   천장 가드가 막은 틱은 <굴려 보지도 않는다> — 그래서 두 칸이 —다. 그게 곧 가설 ①의 모양이다.)");
            text.AppendLine("  ⚠️ <깔때기 안>을 믿지 마라 — 깔때기는 관문 뒤를 안 본다(① 관문 통과 절의 ⚠️ 참고).");
            if (approaches == null || approaches.Count == 0)
            {
                text.Append("  잴 것이 없다 — 관문 앞 틱을 모은 비행이 하나도 없다");
                return text.ToString();
            }
            text.AppendLine();
            text.AppendLine($"  ■ x {gateStartX:F1}~{gateEndX:F1} (얼굴 열 x={faceX:F1}) — 여기서 멈춘 비행"
                          + $" {approaches.Count}개의 진입 직전 {WindowTicks}틱");

            var total = default(ApproachTally);
            for (int i = 0; i < approaches.Count; i++)
            {
                GateApproach approach = approaches[i];
                ApproachTally tally = Tally(approach.Samples, faceX, sameReachEpsilon);
                total = total.Add(tally);
                text.AppendLine();
                text.AppendLine($"    {approach.Name} (진입 y={approach.EntryY:F2}"
                              + $" vy={approach.EntryVerticalSpeed:F1},"
                              + $" 깔때기 {(approach.InFunnel ? "안" : "밖")},"
                              + $" 진입 {approach.TicksAfterEntry}틱 뒤 멈춤)");
                if (i < tableLimit)
                {
                    AppendTable(text, approach.Samples);
                }
                text.AppendLine($"      → {Verdict(tally)}");
            }
            text.AppendLine();
            text.AppendLine($"  ▶ 관문 전체 {total.Total}틱: {Verdict(total)}");
            text.Append("  (주의: 이 절은 관문 <앞>만 본다 — 진입 상태 자체가 앞 구간의 결과라는 점은"
                      + " 관문 통과 절의 주의와 같다.)");
            return text.ToString();
        }

        static void AppendTable(StringBuilder text, IReadOnlyList<ApproachSample> samples)
        {
            text.AppendLine("      틱     x       y      vy   누르고싶나 천장가드     누르면     안누르면  선택");
            for (int i = 0; samples != null && i < samples.Count; i++)
            {
                ApproachSample s = samples[i];
                string flap = s.RolledOut ? $"{s.FlapAliveTicks}틱 x{s.FlapReachX:F1}" : "—";
                string coast = s.RolledOut ? $"{s.CoastAliveTicks}틱 x{s.CoastReachX:F1}" : "—";
                text.AppendLine($"      {s.Tick,4} {s.X,7:F1} {s.Y,7:F2} {s.VerticalSpeed,7:F1}"
                              + $"   {(s.WantsFlap ? "예" : "아니오"),-6}"
                              + $" {(s.CeilingSafe ? "통과" : "막힘"),-6}"
                              + $" {flap,12} {coast,12}"
                              + $"  {(s.Flapped ? "누름" : "안누름")}");
            }
        }
    }
}
