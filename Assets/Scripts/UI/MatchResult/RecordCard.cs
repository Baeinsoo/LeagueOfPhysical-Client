using System;
using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>"내 기록" 카드의 한 줄 — 지표 이름, 이번 값, 최고와의 차이(처음이면 "첫 기록"), 경신했나(차이 글자만 초록).</summary>
    public readonly struct RecordLine
    {
        public readonly string Label;
        public readonly string ValueText;
        public readonly string DiffText;
        public readonly bool Improved;

        public RecordLine(string label, string valueText, string diffText, bool improved)
        {
            Label = label;
            ValueText = valueText;
            DiffText = diffText;
            Improved = improved;
        }
    }

    /// <summary>
    /// 결과판 "내 기록" 카드(spec §7) — 종목 공용. 실린 기록(<see cref="MatchResult.myRecords"/>)만 보고 줄을 만든다 — 어느 종목인지 모른다.
    /// </summary>
    public static class RecordCard
    {
        public static IReadOnlyList<RecordLine> Lines(IReadOnlyList<RecordOutcome> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                return Array.Empty<RecordLine>();
            }
            var lines = new List<RecordLine>(outcomes.Count);
            foreach (var o in outcomes)
            {
                lines.Add(new RecordLine(o.Metric.Label, o.ValueText, o.DiffText, o.IsNewBest));
            }
            return lines;
        }

        public static bool AnyNewBest(IReadOnlyList<RecordOutcome> outcomes)
        {
            if (outcomes == null)
            {
                return false;
            }
            foreach (var o in outcomes)
            {
                if (o.IsNewBest)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
