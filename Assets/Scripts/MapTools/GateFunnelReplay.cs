using System;
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>조작열 하나를 커널로 되굴린 결과. 깔때기가 "지난다"고 한 것을 <b>같은 자로</b>
    /// 다시 재기 위한 것이다 — 깔때기는 "어떤 조작열이 있나"만 답하므로, 그 조작열이 정말
    /// 지나는지는 이 재생만이 말한다.</summary>
    public readonly struct FunnelReplay
    {
        /// <summary>관문 끝을 지났나.</summary>
        public readonly bool Passed;
        /// <summary>끝난 틱 — 지났으면 나간 틱, 막혔으면 막힌 틱. 둘 다 0부터 센다.</summary>
        public readonly int Tick;
        public readonly float X;
        public readonly float Y;
        public readonly float VerticalSpeed;
        /// <summary>막은 면의 콜라이더 이름. 모르면 null.</summary>
        public readonly string Hit;
        /// <summary>창 천장을 넘어 막혔나(아니면 창 바닥 아래로 빠져 막혔다). 지났으면 뜻 없다.</summary>
        public readonly bool AboveCeiling;
        /// <summary>그 창을 얼마나 벗어났나(m). 지났으면 0이다.</summary>
        public readonly float Overshoot;
        /// <summary>막힌 자리가 <b>출발 열</b>인가(아니면 도착 열이다). 지났으면 뜻 없다.</summary>
        public readonly bool AtStartColumn;

        public FunnelReplay(bool passed, int tick, float x, float y, float verticalSpeed,
                            string hit = null, bool aboveCeiling = false, float overshoot = 0f,
                            bool atStartColumn = false)
        {
            Passed = passed;
            Tick = tick;
            X = x;
            Y = y;
            VerticalSpeed = verticalSpeed;
            Hit = hit;
            AboveCeiling = aboveCeiling;
            Overshoot = overshoot;
            AtStartColumn = atStartColumn;
        }
    }

    /// <summary>깔때기가 고른 조작열을 그대로 되굴린다. <see cref="GateFunnelRule.TryRolls"/>가
    /// 내는 열과 이 재생의 답은 <b>반드시 같아야 한다</b> — 다르면 둘 중 하나가 물리를 달리
    /// 세고 있다는 뜻이라, 그 불일치 자체가 도구의 고장이다.</summary>
    public static class GateFunnelReplayRule
    {
        /// <summary>조작열 하나를 <paramref name="kernel"/>로 굴려 관문을 지나는지 본다.
        /// 검사는 <see cref="GateFunnelRule.Rolls"/>와 같다 — 한 틱이 지나간 두 열 모두에서
        /// 몸이 한 창 안에 있어야 한다.</summary>
        public static FunnelReplay Replay(float entryY, float entryVerticalSpeed,
                                          IReadOnlyList<bool> flaps,
                                          IReadOnlyList<GateColumn> columns, in FlightKernel kernel)
        {
            if (columns == null || columns.Count < 2)
            {
                return new FunnelReplay(false, 0, 0f, entryY, entryVerticalSpeed);
            }
            float startX = columns[0].X;
            float endX = columns[columns.Count - 1].X;
            float x = startX;
            float y = entryY;
            float vy = entryVerticalSpeed;
            int count = flaps == null ? 0 : flaps.Count;
            for (int t = 0; t < count; t++)
            {
                vy = kernel.NextVerticalSpeed(vy, flaps[t]);
                float nextY = y + vy * kernel.TickSeconds;
                float nextX = x + kernel.ForwardSpeed * kernel.TickSeconds;
                float low = Math.Min(y, nextY);
                float high = Math.Max(y, nextY);
                if (GateFunnelRule.SpanFits(low, high, kernel.BodyHeight,
                                            GateFunnelRule.ColumnAt(columns, x).Windows) == false)
                {
                    return Blocked(t, x, nextY, vy, low, high, kernel.BodyHeight,
                                   GateFunnelRule.ColumnAt(columns, x).Windows, atStartColumn: true);
                }
                if (GateFunnelRule.SpanFits(low, high, kernel.BodyHeight,
                                            GateFunnelRule.ColumnAt(columns, nextX).Windows) == false)
                {
                    return Blocked(t, nextX, nextY, vy, low, high, kernel.BodyHeight,
                                   GateFunnelRule.ColumnAt(columns, nextX).Windows, atStartColumn: false);
                }
                x = nextX;
                y = nextY;
                if (x > endX)
                {
                    return new FunnelReplay(true, t, x, y, vy);
                }
            }
            //  조작열이 관문 끝까지 데려다주지 못했다 — 지난 것이 아니다.
            return new FunnelReplay(false, count, x, y, vy);
        }

        static FunnelReplay Blocked(int tick, float x, float y, float vy,
                                    float low, float high, float bodyHeight,
                                    IReadOnlyList<GateWindow> windows, bool atStartColumn)
        {
            //  가장 적게 벗어난 창을 골라 "어느 쪽으로 얼마나" 벗어났는지 적는다.
            float bestPenalty = float.MaxValue;
            bool aboveCeiling = false;
            string hit = null;
            for (int i = 0; windows != null && i < windows.Count; i++)
            {
                float below = windows[i].Bottom - low;
                float above = high + bodyHeight - windows[i].Top;
                float penalty = Math.Max(below, 0f) + Math.Max(above, 0f);
                if (penalty >= bestPenalty)
                {
                    continue;
                }
                bestPenalty = penalty;
                aboveCeiling = above > below;
                hit = aboveCeiling ? windows[i].Ceiling : windows[i].Floor;
            }
            return new FunnelReplay(false, tick, x, y, vy, hit, aboveCeiling,
                                    bestPenalty == float.MaxValue ? 0f : bestPenalty, atStartColumn);
        }
    }
}
