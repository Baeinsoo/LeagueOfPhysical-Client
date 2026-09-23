using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>부스트 패드 하나를 잰 결과. 씬 타입을 안 들고 와야 순수 계층에서 잴 수 있다.</summary>
    public readonly struct BoostPadMeasure
    {
        public readonly string Name;
        public readonly float X, Y0, Y1;

        /// <summary>밟으면 몇 초 동안 대시가 되나.</summary>
        public readonly float Duration;

        /// <summary>패드 자리에서 회랑의 안쪽 바닥·천장. 고저차 때문에 x마다 다르다.</summary>
        public readonly float CorridorLow, CorridorHigh;

        /// <summary>패드와 겹친 장애물 이름. 없으면 null.</summary>
        public readonly string OverlapName;

        public BoostPadMeasure(string name, float x, float y0, float y1, float duration,
                               float corridorLow, float corridorHigh, string overlapName)
        {
            Name = name;
            X = x;
            Y0 = y0;
            Y1 = y1;
            Duration = duration;
            CorridorLow = corridorLow;
            CorridorHigh = corridorHigh;
            OverlapName = overlapName;
        }

        public bool OutsideCorridor => Y0 < CorridorLow || Y1 > CorridorHigh;
        public bool Overlapped => string.IsNullOrEmpty(OverlapName) == false;
        public bool Ok => OutsideCorridor == false && Overlapped == false;
    }

    /// <summary>
    /// 부스트 패드가 <b>밟을 수 있는 자리에</b> 있는지 본다. 패드는 콜라이더가 없어 아무 데나
    /// 놓여도 조용히 넘어가므로, 놓친 패드를 말해 줄 것이 이 절뿐이다.
    ///
    /// <para>두 가지만 묻는다: ① 회랑 안인가(밖이면 벽 속이라 못 밟는다) ② 장애물과 겹치지
    /// 않는가(겹치면 그 자리에 가려면 먼저 부딪혀야 한다).</para>
    /// </summary>
    public static class BoostPadRule
    {
        /// <summary>
        /// 패드 하나가 벌어 주는 거리(m). 대시와 같은 계산이다 — 부스트 동안 <c>dashMult</c>배로
        /// 가므로, 평소보다 더 간 몫이 이득이다.
        /// </summary>
        public static float GainMeters(float duration, float forwardSpeed, float dashMult)
            => duration * forwardSpeed * (dashMult - 1f);

        public static string Section(IReadOnlyList<BoostPadMeasure> pads, float forwardSpeed,
                                    float dashMult, float collisionCostMeters)
        {
            var text = new StringBuilder();
            text.AppendLine("── ⚡ 부스트 패드 ──────────────────────");

            if (pads == null || pads.Count == 0)
            {
                text.AppendLine("  패드가 없다 — 위험한 쪽을 고른 보상이 거리로 돌아오지 않는다.");
                return text.ToString().TrimEnd();
            }

            int bad = 0;
            float total = 0f;
            for (int i = 0; i < pads.Count; i++)
            {
                BoostPadMeasure pad = pads[i];
                total += GainMeters(pad.Duration, forwardSpeed, dashMult);
                if (pad.Ok)
                {
                    continue;
                }
                bad++;
                string why = pad.Overlapped
                    ? $"장애물과 겹친다 ({pad.OverlapName}) — 밟으려면 먼저 부딪혀야 한다"
                    : $"회랑 밖이다 (패드 y[{pad.Y0:F2}~{pad.Y1:F2}], 회랑 y[{pad.CorridorLow:F2}~{pad.CorridorHigh:F2}])";
                text.AppendLine($"  ❌ x={pad.X:F0} {pad.Name} — {why}");
            }

            float perPad = GainMeters(pads[0].Duration, forwardSpeed, dashMult);
            text.AppendLine($"  패드 {pads.Count}개 · 하나당 +{perPad:F2}m"
                          + $" (충돌 {collisionCostMeters:F1}m의 {perPad / collisionCostMeters:F2}배)");
            text.AppendLine($"  전부 밟으면 +{total:F1}m");
            text.AppendLine(bad == 0
                ? "  ✅ 전부 밟을 수 있는 자리다."
                : $"  ❌ {bad}개가 못 밟는 자리다 — 위 줄을 보라.");

            return text.ToString().TrimEnd();
        }
    }
}
