using System.Collections.Generic;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;

namespace LOP.EditorTools
{
    /// <summary>
    /// 레이저 묶음(미션 임파서블·젤다의 연속 빔) — 판정 코드는 그대로 두고 박자(Phase)·각도만 다른 빔 여러 개를 만든다.
    /// 빔은 피벗에서 각도 방향(cos, 0, sin)으로 뻗는다(<see cref="LOP.SkydiveLaserView"/>와 같은 규칙).
    /// </summary>
    internal static class SkydiveLaserPatterns
    {
        private const float BeamRadius = 0.6f;

        /// <summary>평행 고정 빔. gapIndex 줄은 비운다 — 그 틈이 지나가는 길이다.</summary>
        public static LaserSpec[] Comb(string name, Vector3 center, float angleDeg, int count, float spacing, float length, int gapIndex)
        {
            var list = new List<LaserSpec>();
            for (int i = 0; i < count; i++)
            {
                if (i == gapIndex) { continue; }
                list.Add(Bar($"{name}_Comb{i}", center, angleDeg, Offset(i, count, spacing), length, 0, 0, 0));
            }
            return list.ToArray();
        }

        /// <summary>평행 점멸 빔 — 줄마다 박자를 period/count씩 밀어 차례로 켜진다(물결).</summary>
        public static LaserSpec[] Wave(string name, Vector3 center, float angleDeg, int count, float spacing, float length, int period, int onTicks)
        {
            var list = new LaserSpec[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = Bar($"{name}_Wave{i}", center, angleDeg, Offset(i, count, spacing), length, period, onTicks, i * period / count);
            }
            return list;
        }

        /// <summary>한 피벗에서 고르게 뻗어 함께 도는 갈래들.</summary>
        public static LaserSpec[] Fan(string name, Vector3 pivot, int arms, float length, float degPerTick, float radius)
        {
            var list = new LaserSpec[arms];
            for (int i = 0; i < arms; i++)
            {
                list[i] = new LaserSpec($"{name}_Fan{i}", pivot, length, radius, i * 360f / arms, degPerTick, 0f, 0, 0, 0);
            }
            return list;
        }

        /// <summary>두 겹(아래 0°, 위 90°) 빗살 — 둘 다 가운데 줄을 비워 틈이 위아래로 겹친다.</summary>
        public static LaserSpec[] Grid(string name, Vector3 center, int count, float spacing, float length, float layerGap)
        {
            var lower = Comb($"{name}_Lo", center, 0f, count, spacing, length, count / 2);
            var upper = Comb($"{name}_Hi", center + Vector3.up * layerGap, 90f, count, spacing, length, count / 2);
            var all = new List<LaserSpec>(lower);
            all.AddRange(upper);
            return all.ToArray();
        }

        /// <summary>양쪽에서 마주 보고 왕복하며 가운데를 쓰는 두 빔(조이는 문).</summary>
        public static LaserSpec[] Closing(string name, Vector3 center, float halfWidth, float length, float sweepDeg, float degPerTick)
        {
            return new[]
            {
                new LaserSpec($"{name}_CloseL", center + Vector3.left * halfWidth, length, BeamRadius, 0f, degPerTick, sweepDeg, 0, 0, 0),
                new LaserSpec($"{name}_CloseR", center + Vector3.right * halfWidth, length, BeamRadius, 180f, degPerTick, sweepDeg, 0, 0, 0),
            };
        }

        private static float Offset(int i, int count, float spacing) => (i - (count - 1) * 0.5f) * spacing;

        //  가운데가 center를 지나고 angleDeg 방향으로 놓인 빔 하나 — 옆으로 offset만큼 비켜서.
        private static LaserSpec Bar(string name, Vector3 center, float angleDeg, float offset, float length, int period, int onTicks, int phase)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var perp = new Vector3(-dir.z, 0f, dir.x);
            var pivot = center - dir * (length * 0.5f) + perp * offset;
            return new LaserSpec(name, pivot, length, BeamRadius, angleDeg, 0f, 0f, period, onTicks, phase);
        }
    }
}
