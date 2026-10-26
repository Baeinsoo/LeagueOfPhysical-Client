using System;
using System.Globalization;

namespace LOP
{
    /// <summary>
    /// 기록 카드 글자(spec §7). 화면 글꼴(블랙한산스·주아·키트)에 진짜 빼기(U+2212)가 없어 하이픈을 쓰고,
    /// 숫자는 문화권과 무관하게 점으로 찍는다.
    /// </summary>
    public static class RecordText
    {
        public const string FirstRecord = "첫 기록";

        public static string Value(RecordMetric metric, double value)
            => metric.Format == RecordFormat.Time ? RaceClock.Format(value) : Count(value).ToString(CultureInfo.InvariantCulture) + "회";

        /// <summary>이번 값 − 최고. 시간은 소수 둘째 자리 반올림("-0.36초"/"+0.60초"), 횟수는 "+3회"/"-2회". 같으면 "+0…".</summary>
        public static string Diff(RecordMetric metric, double value, double best)
        {
            if (metric.Format == RecordFormat.Time)
            {
                //  반올림한 뒤 부호를 정한다 — 같은 틱(차이 -0.0000015)이 "-0.00초"로 보이지 않게.
                double d = Math.Round(value - best, 2, MidpointRounding.AwayFromZero);
                if (d == 0d)
                {
                    d = 0d;   //  음수 영(-0.0)은 "-0.00"으로 찍힌다.
                }
                return d < 0d
                    ? "-" + (-d).ToString("F2", CultureInfo.InvariantCulture) + "초"
                    : "+" + d.ToString("F2", CultureInfo.InvariantCulture) + "초";
            }
            long c = Count(value) - Count(best);
            return c < 0
                ? "-" + (-c).ToString(CultureInfo.InvariantCulture) + "회"
                : "+" + c.ToString(CultureInfo.InvariantCulture) + "회";
        }

        private static long Count(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
