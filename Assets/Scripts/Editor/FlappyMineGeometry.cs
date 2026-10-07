using System;
using System.Collections.Generic;
using LOP.MapTools;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 광산 굽기(<see cref="FlappyMineCourseBuilder"/>)의 다각형 셈 — 씬 없이 테스트하려고 뺐다.
    ///
    /// <para>다각형은 전부 <b>반시계 볼록</b>이다 — 전통 굽기의 바닥·천장 조각과 같은 감기라
    /// <c>PrismMesh</c>가 앞면을 카메라 쪽으로 내고, 볼록 메시 콜라이더도 모양이 그대로 남는다.
    /// 양 끝 변은 세로로 곧다: 이웃 조각과 세로 변을 맞대므로 꺾인 자리에도 틈·턱이 없다.</para>
    ///
    /// <para>막을 높이가 없는 조각(굴 가장자리가 천장·바닥선에 닿았거나 넘었다)은 <c>null</c> — 굽지 않는다.</para>
    /// </summary>
    internal static class FlappyMineGeometry
    {
        //  이보다 얇은 높이는 없는 것으로 본다(float 셈 찌꺼기).
        const float MinHeight = 1e-4f;

        /// <summary>바닥 조각: 윗변이 바닥선 (x0,y0)–(x1,y1), 아래로 <paramref name="thickness"/>.</summary>
        internal static Vector2[] FloorQuad(float x0, float y0, float x1, float y1, float thickness)
        {
            if (!(x1 > x0)) { return null; }
            return new[]
            {
                new Vector2(x0, y0 - thickness),
                new Vector2(x1, y1 - thickness),
                new Vector2(x1, y1),
                new Vector2(x0, y0),
            };
        }

        /// <summary>천장 조각: 아랫변이 천장선 (x0,y0)–(x1,y1), 위로 <paramref name="thickness"/>.</summary>
        internal static Vector2[] CeilingQuad(float x0, float y0, float x1, float y1, float thickness)
        {
            if (!(x1 > x0)) { return null; }
            return new[]
            {
                new Vector2(x0, y0),
                new Vector2(x1, y1),
                new Vector2(x1, y1 + thickness),
                new Vector2(x0, y0 + thickness),
            };
        }

        /// <summary>
        /// 굴 지붕 조각 [x0, x1]: 아랫변 = 굴 위 가장자리(<c>Center + Gap/2</c>), 윗변 = <see cref="MineTube.High"/>
        /// (NaN이면 그 x의 통로 천장선 <paramref name="ceilingAt"/>).
        /// </summary>
        internal static Vector2[] TubeRoofStrip(MineTube tube, float x0, float x1, Func<float, float> ceilingAt)
        {
            float half = tube.Gap * 0.5f;
            return Band(x0, tube.Center(x0) + half, Edge(tube.High, ceilingAt, x0),
                        x1, tube.Center(x1) + half, Edge(tube.High, ceilingAt, x1));
        }

        /// <summary>
        /// 굴 바닥 조각 [x0, x1]: 윗변 = 굴 아래 가장자리(<c>Center − Gap/2</c>), 아랫변 = <see cref="MineTube.Low"/>
        /// (NaN이면 그 x의 통로 바닥선 <paramref name="floorAt"/>).
        /// </summary>
        internal static Vector2[] TubeFloorStrip(MineTube tube, float x0, float x1, Func<float, float> floorAt)
        {
            float half = tube.Gap * 0.5f;
            return Band(x0, Edge(tube.Low, floorAt, x0), tube.Center(x0) - half,
                        x1, Edge(tube.Low, floorAt, x1), tube.Center(x1) - half);
        }

        /// <summary>[from, to]를 <paramref name="step"/>씩 자른다. 마지막 조각만 짧고, 끝은 정확히 <paramref name="to"/>.</summary>
        internal static IEnumerable<(float x0, float x1)> Slices(float from, float to, float step)
        {
            if (step <= 0f) { throw new ArgumentOutOfRangeException(nameof(step)); }
            int n = Mathf.CeilToInt((to - from) / step - 1e-4f);
            for (int i = 0; i < n; i++)
            {
                float a = from + i * step;
                float b = i == n - 1 ? to : from + (i + 1) * step;
                yield return (a, b);
            }
        }

        static float Edge(float fixedY, Func<float, float> line, float x) => float.IsNaN(fixedY) ? line(x) : fixedY;

        //  세로 변이 곧은 띠. 한쪽 끝만 납작하면(가장자리가 선을 스치고 지나간다) 그 끝을 한 점으로 모아
        //  삼각형으로 둔다 — 높이를 음수로 두면 사각형이 꼬여 볼록이 깨진다. 양 끝 다 납작하면 null.
        static Vector2[] Band(float x0, float lo0, float hi0, float x1, float lo1, float hi1)
        {
            if (!(x1 > x0)) { return null; }
            bool flat0 = hi0 - lo0 <= MinHeight, flat1 = hi1 - lo1 <= MinHeight;
            if (flat0 && flat1) { return null; }
            if (flat0) { return new[] { new Vector2(x0, lo0), new Vector2(x1, lo1), new Vector2(x1, hi1) }; }
            if (flat1) { return new[] { new Vector2(x0, lo0), new Vector2(x1, lo1), new Vector2(x0, hi0) }; }
            return new[] { new Vector2(x0, lo0), new Vector2(x1, lo1), new Vector2(x1, hi1), new Vector2(x0, hi0) };
        }
    }
}
