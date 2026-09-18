using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>코스에서 찾아낸 관문 하나 — 지형이 목표 창 언저리까지 좁아지는 한 구간.</summary>
    public readonly struct Gate
    {
        public readonly float StartX;
        public readonly float EndX;
        /// <summary>그 구간에서 <b>가장 좁은</b> 밴드. 관문의 난이도를 정하는 것은 최솟값이다.</summary>
        public readonly float Window;

        public float CenterX => (StartX + EndX) * 0.5f;

        public Gate(float startX, float endX, float window)
        {
            StartX = startX;
            EndX = endX;
            Window = window;
        }
    }

    /// <summary>
    /// <b>관문을 어떻게 놓아야 하는가</b>의 목표치와, 실제 맵이 그 목표에서 얼마나 벗어났는지.
    ///
    /// <para><b>숫자를 박지 않는다.</b> 목표 창은 물리(아치·몸 높이)에서, 목표 간격은 전진 속도에서
    /// 그때그때 계산한다. 미터로 박아 두면 물리를 만질 때마다 손으로 다시 계산해야 하고, 실제로
    /// 그래서 한 번 사고가 났다 — 회랑 하한 4.912가 물리 변경 뒤에도 문서·주석에 남아 있었다.</para>
    ///
    /// <para><b>목표의 출처는 원본 플래피 버드다.</b> 원본은 한 번의 날갯짓이 창 빈틈(창 − 몸)의
    /// 90%를 먹고, 파이프가 1.67초마다 온다. 둘 다 <i>비율과 시간</i>이라 길이 단위가 없어서,
    /// 우리 물리·속도에 그대로 옮길 수 있다.</para>
    ///
    /// <para><b>이 규칙이 답하지 않는 것</b>: 창이 목표대로라고 해서 새가 거기 <i>도달할 수</i>
    /// 있다는 뜻은 아니다. 도달 가능성은 봇 비행(①)이 답한다.</para>
    /// </summary>
    public static class GateRhythmRule
    {
        /// <summary>원본에서 한 번의 날갯짓이 창 빈틈의 이만큼을 먹는다. 1에 가까울수록
        /// 탭 한 번이 곧 천장이라 되돌릴 수 없는 결정이 된다.</summary>
        public const float ArcShareOfWindow = 0.90f;

        /// <summary>원본의 파이프 간격. <b>미터가 아니라 초</b>다 — 전진 속도를 바꾸면
        /// 미터는 따라 움직여야 하지만 박자는 그대로여야 한다.</summary>
        public const float SpacingSeconds = 1.67f;

        /// <summary>어디까지를 "관문"으로 볼 것인가. 목표 창의 이 배수 이하로 좁아진 x를 센다.
        /// 1.5인 이유: 목표보다 한참 넓은 회랑(우리 맵의 18m 구간은 목표의 4배)은 빼되,
        /// 목표를 조금 넘긴 관문(5.03m는 목표의 1.15배)은 놓치지 않는 폭이다.</summary>
        public const float GateBandFactor = 1.5f;

        /// <summary>날갯짓 한 번이 빈틈의 <see cref="ArcShareOfWindow"/>를 먹는 창 높이.</summary>
        public static float TargetWindow(float flapImpulse, float gravity, float tickSeconds,
                                         float bodyHeight)
            => BotPilot.FlapArc(flapImpulse, gravity, tickSeconds) / ArcShareOfWindow + bodyHeight;

        /// <summary>원본 박자를 우리 전진 속도로 옮긴 간격(m).</summary>
        public static float TargetSpacing(float forwardSpeed) => SpacingSeconds * forwardSpeed;

        /// <summary>
        /// 밴드가 목표 창의 <see cref="GateBandFactor"/>배 이하로 좁아지는 x를 이어 붙여 관문으로 센다.
        /// 트인 칸(천장이 없는 곳)은 좁힐 것 자체가 없으므로 세지 않는다.
        /// </summary>
        public static List<Gate> Find(IReadOnlyList<PinchColumn> columns, float targetWindow)
        {
            var gates = new List<Gate>();
            if (columns == null)
            {
                return gates;
            }
            float ceiling = targetWindow * GateBandFactor;
            int runStart = -1;
            for (int i = 0; i <= columns.Count; i++)
            {
                bool narrow = i < columns.Count
                           && columns[i].Open == false
                           && columns[i].Band > 0f
                           && columns[i].Band <= ceiling;
                if (narrow)
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
                float narrowest = columns[runStart].Band;
                for (int k = runStart + 1; k <= i - 1; k++)
                {
                    if (columns[k].Band < narrowest)
                    {
                        narrowest = columns[k].Band;
                    }
                }
                gates.Add(new Gate(columns[runStart].X, columns[i - 1].X, narrowest));
                runStart = -1;
            }
            return gates;
        }

        /// <summary>리포트의 "②-d 관문 박자" 절 전체(머리말 줄 포함, 끝에 줄바꿈 없음).</summary>
        public static string Section(IReadOnlyList<Gate> gates, float targetWindow, float targetSpacing,
                                     float forwardSpeed, float courseStartX, float finishX)
        {
            var text = new StringBuilder();
            text.AppendLine("── ②-d 관문 박자 ──────────────────────");
            text.AppendLine($"  (원본 플래피는 한 날갯짓이 창 빈틈의 {ArcShareOfWindow * 100:F0}%를 먹고 파이프가"
                          + $" {SpacingSeconds:F2}초마다 온다.");
            text.AppendLine($"   우리 물리·속도로 옮기면 목표 창 {targetWindow:F2}m · 목표 간격 {targetSpacing:F1}m"
                          + $" (전진 {forwardSpeed:F1} m/s).");
            text.AppendLine("   창이 넓으면 창 안에서 고쳐 칠 여유가 생기고, 간격이 넓으면 마주치는 시간이 줄어"
                          + " 좁힌 보람이 사라진다.)");

            if (gates == null || gates.Count == 0)
            {
                text.Append("  관문을 하나도 못 찾았다 — 코스 전체가 목표 창의"
                          + $" {GateBandFactor:F1}배보다 넓다");
                return text.ToString();
            }

            text.AppendLine($"  관문 {gates.Count}개");

            float worstGapStart = courseStartX;
            float worstGap = gates[0].CenterX - courseStartX;
            float sum = 0f;
            int counted = 0;
            for (int i = 0; i < gates.Count; i++)
            {
                Gate g = gates[i];
                string spacing = "  —";
                if (i > 0)
                {
                    float gap = g.CenterX - gates[i - 1].CenterX;
                    sum += gap;
                    counted++;
                    if (gap > worstGap)
                    {
                        worstGap = gap;
                        worstGapStart = gates[i - 1].CenterX;
                    }
                    spacing = $"{gap,6:F1}m ({gap / forwardSpeed,4:F1}초)";
                }
                string windowMark = g.Window > targetWindow * 1.1f ? "  창 넓음" : string.Empty;
                text.AppendLine($"  x={g.CenterX,6:F1}  창 {g.Window,5:F2}m   앞 관문에서 {spacing}{windowMark}");
            }

            //  마지막 관문에서 결승까지도 공백이다 — 여기가 비어 있으면 코스가 맥없이 끝난다.
            float tailGap = finishX - gates[gates.Count - 1].CenterX;
            if (tailGap > worstGap)
            {
                worstGap = tailGap;
                worstGapStart = gates[gates.Count - 1].CenterX;
            }

            if (counted > 0)
            {
                float mean = sum / counted;
                text.AppendLine($"  평균 간격 {mean:F1}m ({mean / forwardSpeed:F1}초)"
                              + $" — 목표 {targetSpacing:F1}m ({SpacingSeconds:F2}초)의 {mean / targetSpacing:F1}배");
            }
            text.Append($"  가장 긴 공백 {worstGap:F0}m ({worstGap / forwardSpeed:F1}초)"
                      + $" — x {worstGapStart:F0}부터. 이 구간은 화면에 관문이 없다");
            return text.ToString();
        }
    }
}
