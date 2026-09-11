using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>원판의 x 구간 안 한 자리에서 잰 위·아래 빈 높이(m).</summary>
    public readonly struct BandSample
    {
        /// <summary>원판 위쪽으로 이어지는 빈 높이. <b>음수면 그만큼 원판 윗끝이 지형에 묻혀
        /// 있다</b>(그만큼 내려야 밖으로 나온다).</summary>
        public readonly float Above;
        /// <summary>원판 아래쪽으로 이어지는 빈 높이. <b>음수면 그만큼 원판 아랫끝이 지형에
        /// 묻혀 있다</b>(그만큼 올려야 밖으로 나온다).</summary>
        public readonly float Below;

        public BandSample(float above, float below)
        {
            Above = above;
            Below = below;
        }
    }

    /// <summary>
    /// 돌아가는 장애물 하나의 배치를 잰 값. <b>어떤 위상에서든</b>을 말하려면 팔이 쓸고 가는
    /// 원판 전체를 막힌 것으로 쳐야 하므로, 남는 것은 원판 위·아래 두 밴드뿐이다.
    /// </summary>
    public readonly struct ObstaclePlacement
    {
        public readonly string Name;
        /// <summary>회전 중심.</summary>
        public readonly float CenterX;
        public readonly float CenterY;
        /// <summary>회전 중심에서 가장 먼 콜라이더 점까지의 거리 — 팔이 쓸고 가는 원판의 반지름.</summary>
        public readonly float DiscRadius;
        /// <summary>원판 위 밴드 — x 구간 <b>전체</b>에 걸친 최솟값이다. 한 자리에서만 넓으면
        /// 소용없다(그 옆에서 막히면 밴드를 타고 지나갈 수 없다). 음수면 묻힌 깊이다.</summary>
        public readonly float BandAbove;
        /// <summary>원판 아래 밴드 — 위와 같이 구간 안 최솟값.</summary>
        public readonly float BandBelow;
        /// <summary>재는 데 성공했는가. 콜라이더를 못 찾아 원판 반지름이 0이거나 표본이 아예
        /// 없으면 false다 — 그런 장애물은 통과/미달 어느 쪽으로도 세지 않는다.</summary>
        public readonly bool Measured;

        public ObstaclePlacement(string name, float centerX, float centerY, float discRadius,
                                 float bandAbove, float bandBelow, bool measured)
        {
            Name = name;
            CenterX = centerX;
            CenterY = centerY;
            DiscRadius = discRadius;
            BandAbove = bandAbove;
            BandBelow = bandBelow;
            Measured = measured;
        }

        /// <summary>x 구간에서 뜬 표본들을 <b>최솟값</b>으로 접어 배치 하나를 만든다.
        /// <para>반지름이 0 이하거나(콜라이더를 못 찾음) 표본이 하나도 없으면 "측정 안 됨"이다 —
        /// 그 경우를 0m 밴드로 찍으면 "재 보니 꽉 막혔다"로 읽혀 엉뚱한 데를 고치게 된다.</para></summary>
        public static ObstaclePlacement Measure(string name, float centerX, float centerY,
                                                float discRadius, IReadOnlyList<BandSample> samples)
        {
            if (discRadius <= 0f || samples == null || samples.Count == 0)
            {
                return new ObstaclePlacement(name, centerX, centerY, discRadius, 0f, 0f, measured: false);
            }

            float above = samples[0].Above;
            float below = samples[0].Below;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].Above < above)
                {
                    above = samples[i].Above;
                }
                if (samples[i].Below < below)
                {
                    below = samples[i].Below;
                }
            }
            return new ObstaclePlacement(name, centerX, centerY, discRadius, above, below, measured: true);
        }
    }

    /// <summary>배치 하나에 대한 판정.</summary>
    public readonly struct PlacementVerdict
    {
        public readonly bool Measured;
        /// <summary>어떤 위상에서도 통과 가능이 <b>보장</b>되는가.</summary>
        public readonly bool Guaranteed;
        /// <summary>둘 중 더 나은 밴드. 우회는 한쪽만 되면 되므로 이 값이 기준이다.</summary>
        public readonly float BestBand;
        /// <summary>기준에서 모자란 양(m). 보장되면 0이다.</summary>
        public readonly float Shortfall;
        /// <summary>
        /// <b>회랑 안에서 장애물을 한쪽 벽에 붙였을 때</b> 남는 모자람(m). 0이면 회랑도 팔도
        /// 안 건드리고 <b>자리만 옮기면</b> 된다.
        /// <para>가운데 두면 빈 자리가 위·아래로 갈려 어느 쪽도 충분하지 않을 수 있다. 한쪽으로
        /// 붙이면 그 둘이 반대쪽 한 덩어리가 되므로, 그때 쓸 수 있는 밴드는 위+아래다.</para>
        /// </summary>
        public readonly float ShortfallIfShifted;

        public PlacementVerdict(bool measured, bool guaranteed, float bestBand, float shortfall,
                                float shortfallIfShifted)
        {
            Measured = measured;
            Guaranteed = guaranteed;
            BestBand = bestBand;
            Shortfall = shortfall;
            ShortfallIfShifted = shortfallIfShifted;
        }
    }

    /// <summary>
    /// <b>배치만 보고</b> "이 장애물은 어떤 위상에 도착해도 지나갈 자리가 있는가"에 답한다 —
    /// 시뮬레이션이 아니라 산술이다.
    ///
    /// <para><b>불변식: 원판 바깥 한쪽 밴드 ≥ 날갯짓 아치 + 몸 높이.</b> 플래피의 날갯짓은
    /// 크기가 하나뿐이라, 아치보다 좁은 띠 안에서는 <b>아예 누를 수가 없고</b> 떨어지기만 한다.
    /// 그래서 그만한 밴드가 원판 위나 아래 한쪽에라도 있어야 원판을 지나는 동안 높이를 유지할
    /// 수 있다.</para>
    ///
    /// <para><b>이 검사가 보장하지 않는 것</b>: 밴드가 <i>있다</i>는 것과 새가 <i>거기 도달할 수
    /// 있다</i>는 것은 다르다. 도달 가능성은 봇 비행(①)이 답한다.</para>
    ///
    /// <para>이 판정이 "미달"이라고 해서 모든 궤적이 거기서 죽는다는 뜻은 아니다 — 운 좋은
    /// 위상 몇 개는 지나갈 수 있다. 불변식은 <b>보장된 통과</b>에 대한 것이지 <b>가능한
    /// 통과</b>에 대한 것이 아니다.</para>
    /// </summary>
    public static class ObstaclePlacementRule
    {
        /// <summary>밴드가 이만큼은 돼야 한다 — 날갯짓 한 번의 아치 + 몸 높이.
        /// <para>숫자를 박지 않는다: 아치는 실제 물리값으로 <see cref="BotPilot.FlapArc"/>가
        /// 내고, 몸 높이는 MasterData에서 온다. 물리가 바뀌면 기준도 따라 움직인다.</para></summary>
        public static float RequiredBand(float flapImpulse, float gravity, float tickSeconds, float bodyHeight)
            => BotPilot.FlapArc(flapImpulse, gravity, tickSeconds) + bodyHeight;

        public static PlacementVerdict Judge(in ObstaclePlacement placement, float requiredBand)
        {
            if (placement.Measured == false)
            {
                return new PlacementVerdict(measured: false, guaranteed: false, bestBand: 0f,
                                            shortfall: 0f, shortfallIfShifted: 0f);
            }
            float best = placement.BandAbove > placement.BandBelow ? placement.BandAbove : placement.BandBelow;
            //  딱 기준만큼이면 통과다 — 그 폭에서는 날갯짓 아치가 밴드 안에 정확히 들어간다.
            bool guaranteed = best >= requiredBand;
            //  한쪽 벽에 붙이면 위·아래로 갈려 있던 빈 자리가 반대쪽에 한 덩어리로 모인다.
            float shifted = placement.BandAbove + placement.BandBelow;
            return new PlacementVerdict(measured: true, guaranteed, best,
                                        guaranteed ? 0f : requiredBand - best,
                                        shifted >= requiredBand ? 0f : requiredBand - shifted);
        }

        /// <summary>리포트의 "②-b 장애물 배치" 절 전체(머리말 줄 포함, 끝에 줄바꿈 없음).</summary>
        public static string Section(IReadOnlyList<ObstaclePlacement> placements, float requiredBand)
        {
            var text = new StringBuilder();
            text.AppendLine("── ②-b 장애물 배치 ────────────────────");
            text.AppendLine($"  (돌아가는 장애물이 어떤 위상에서도 통과 가능한지 — 원판 바깥에 아치+몸({requiredBand:F2}m)이");
            text.AppendLine("   들어가는 밴드가 한쪽이라도 있으면 보장된다. 시뮬레이션이 아니라 산술이다.)");
            text.AppendLine("  (손잡이는 셋 — 팔 길이 L · 회랑 안에서의 세로 위치 · 회랑 폭. 가운데 두면 빈 자리가");
            text.AppendLine("   위·아래로 갈려 비용이 두 배다. 그래서 미달인 자리마다 '한쪽으로 붙였을 때'를 먼저 적는다.");
            text.AppendLine("   밴드가 음수면 그만큼 원판이 지형에 묻혀 있다는 뜻이다.)");

            int shortCount = 0;
            int unmeasured = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                PlacementVerdict verdict = Judge(placements[i], requiredBand);
                if (verdict.Measured == false)
                {
                    unmeasured++;
                }
                else if (verdict.Guaranteed == false)
                {
                    shortCount++;
                }
            }

            var summary = new StringBuilder();
            summary.Append($"  풍차 {placements.Count}개 중 {shortCount}개가 기준 미달");
            if (unmeasured > 0)
            {
                summary.Append($" · {unmeasured}개는 원판을 못 재 판정 못 함");
            }
            text.AppendLine(summary.ToString());

            for (int i = 0; i < placements.Count; i++)
            {
                ObstaclePlacement placement = placements[i];
                PlacementVerdict verdict = Judge(placement, requiredBand);
                string where = $"{placement.Name} (x={placement.CenterX:F1} y={placement.CenterY:F1})";
                if (verdict.Measured == false)
                {
                    text.AppendLine($"  ⛔ {where}  콜라이더를 못 찾아 원판 반지름을 못 쟀다 — 판정 못 함");
                    continue;
                }
                string body = $"{where}  팔 L={placement.DiscRadius:F2}"
                            + $"  위 {placement.BandAbove:F2}m / 아래 {placement.BandBelow:F2}m";
                if (verdict.Guaranteed)
                {
                    text.AppendLine($"  ✅ {body}");
                    continue;
                }
                text.AppendLine($"  ❌ {body}   — {verdict.Shortfall:F2}m 모자람");
                if (verdict.ShortfallIfShifted <= 0f)
                {
                    text.AppendLine("     위치만 한쪽으로 붙이면 → 만족 (팔도 회랑도 안 건드려도 된다)");
                    continue;
                }
                text.AppendLine($"     위치만 한쪽으로 붙이면 → {verdict.ShortfallIfShifted:F2}m 모자람");
                float targetRadius = placement.DiscRadius - verdict.ShortfallIfShifted;
                if (targetRadius <= 0f)
                {
                    //  팔을 그만큼 줄이면 팔이 없어진다 — 남은 손잡이는 회랑뿐이다.
                    text.AppendLine($"     회랑을 {verdict.ShortfallIfShifted:F2}m 넓혀야 한다 (팔만으로는 못 맞춘다)");
                    continue;
                }
                text.AppendLine($"     팔을 {verdict.ShortfallIfShifted:F2}m 줄이거나(L={targetRadius:F2})"
                              + $" 회랑을 {verdict.ShortfallIfShifted:F2}m 넓히면 만족");
            }

            text.AppendLine("  (주의: 밴드가 있다는 것과 새가 거기 도달할 수 있다는 것은 다르다 — 이 검사는 앞엣것만 본다.");
            text.Append("   도달 가능성은 ① 봇 비행이 답한다.)");
            return text.ToString();
        }
    }
}
