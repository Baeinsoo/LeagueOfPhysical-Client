using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LOP.MapTools
{
    public readonly struct GuardWindow
    {
        public readonly string Label;
        public readonly float X;
        public readonly int Passed, Total;
        public readonly bool Measured;
        public readonly string Note;
        public readonly float Gap;
        public readonly bool GapMeasured;
        public GuardWindow(string label, float x, int passed, int total, bool measured, string note, float gap, bool gapMeasured)
        {
            Label = label; X = x; Passed = passed; Total = total; Measured = measured; Note = note; Gap = gap; GapMeasured = gapMeasured;
        }
    }

    /// <summary>
    /// 🚪 문지기 절(spec 2026-10-03 §3). 열린 창 = 통과한 도착 위상 / 전체 위상, 25% 이상이면 ✅.
    /// 기본 길 안전은 쓸고 지나가는 범위(부채꼴·원)와 "갈림길 없이" 경로의 최소 거리로 — 위상과 무관한 증명이다.
    /// </summary>
    public static class GuardRule
    {
        public const float MinOpenShare = 0.25f;

        public static bool Opens(int passed, int total) => total > 0 && passed >= MinOpenShare * total;

        public static float SectorDistance(Vector2 point, in GuardSector s)
        {
            Vector2 center = new Vector2(s.CenterX, s.CenterY);
            Vector2 d = point - center;
            float length = d.magnitude;
            if (length <= 1e-6f)
            {
                return 0f;
            }
            if (s.HalfAngleDegrees >= 180f || Vector2.Angle(Direction(s.AxisDegrees), d) <= s.HalfAngleDegrees)
            {
                return Mathf.Max(0f, length - s.Radius);
            }
            Vector2 a = center + Direction(s.AxisDegrees - s.HalfAngleDegrees) * s.Radius;
            Vector2 b = center + Direction(s.AxisDegrees + s.HalfAngleDegrees) * s.Radius;
            return Mathf.Min(SegmentDistance(point, center, a), SegmentDistance(point, center, b));
        }

        /// <summary>
        /// 경로(틱마다 몸 중심)가 범위에 가장 가까이 간 틈(m). 몸은 캡슐이라 아래·가운데·위 세 점을 보고,
        /// 틱 사이도 네 번 나눠 본다(한 틱 0.14m라 사이를 안 보면 모서리를 지나칠 수 있다).
        /// </summary>
        public static float Gap(IReadOnlyList<Vector3> centers, float bodyRadius, float bodyHeight, in GuardSector s)
        {
            if (centers == null || centers.Count == 0)
            {
                return float.PositiveInfinity;
            }
            float half = Mathf.Max(0f, bodyHeight * 0.5f - bodyRadius);
            float best = float.PositiveInfinity;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 from = centers[i];
                Vector3 to = i + 1 < centers.Count ? centers[i + 1] : centers[i];
                for (int k = 0; k < 4; k++)
                {
                    Vector3 c = Vector3.Lerp(from, to, k / 4f);
                    for (int j = -1; j <= 1; j++)
                    {
                        best = Mathf.Min(best, SectorDistance(new Vector2(c.x, c.y + j * half), s));
                    }
                }
            }
            return best - bodyRadius;
        }

        public static string Section(IReadOnlyList<GuardWindow> rows)
        {
            var text = new StringBuilder();
            text.AppendLine("── 🚪 문지기 ─────────────────────");
            if (rows == null || rows.Count == 0)
            {
                text.Append("  문지기 없음");
                return text.ToString();
            }
            foreach (GuardWindow r in rows)
            {
                if (r.Measured == false)
                {
                    text.AppendLine($"  x={r.X:F0}  {r.Label}  ⛔ 못 쟀다 — {r.Note}");
                    continue;
                }
                int percent = Mathf.FloorToInt(100f * r.Passed / Mathf.Max(1, r.Total));
                string window = Opens(r.Passed, r.Total) ? "✅" : "❌ 25% 미만 — 자리·진폭부터 조정";
                string safety = r.GapMeasured == false ? "⛔ 기본 길 거리 못 잼"
                              : r.Gap > 0f ? $"기본 길과 최소 {r.Gap:F1}m 🌀"
                              : "❌ 기본 길을 쓸고 지나간다";
                text.AppendLine($"  x={r.X:F0}  {r.Label}  열린 창 {percent}% ({r.Passed}/{r.Total})  {window}   {safety}");
            }
            return text.ToString().TrimEnd();
        }

        private static Vector2 Direction(float degrees)
            => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
