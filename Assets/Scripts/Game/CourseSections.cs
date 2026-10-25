using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 맵에 구운 구간 하나(<see cref="FlappyCourseSection"/>에서 읽은 값). HUD는 이 값만 본다 — 시험이 씬 없이 돈다.
    /// </summary>
    public readonly struct CourseSection
    {
        public readonly string Title;
        public readonly int Act;
        public readonly bool Final;
        public readonly float X;

        public CourseSection(string title, int act, bool final, float x)
        {
            Title = title;
            Act = act;
            Final = final;
            X = x;
        }
    }

    /// <summary>
    /// 맵의 구간 표식을 읽는다(spec §5). 맵도구(<c>MineCourse</c>)는 에디터 전용이라 런타임은 굽힌 표식만 본다.
    /// 결과는 늘 x 오름차순이다 — 구간 진입 판정(<see cref="CourseSectionTracker"/>)이 이 순서를 믿는다.
    /// </summary>
    public static class CourseSections
    {
        public static List<CourseSection> From(IEnumerable<FlappyCourseSection> markers)
        {
            var list = new List<CourseSection>();
            foreach (var marker in markers)
            {
                if (marker == null)
                {
                    continue;
                }
                list.Add(new CourseSection(marker.Title ?? string.Empty, marker.Act, marker.Final, marker.X));
            }
            list.Sort((a, b) => a.X.CompareTo(b.X));
            return list;
        }

        /// <summary>올라온 맵 전체에서 찾는다. 비싸므로 판마다 한 번만 부른다(<see cref="LOP.UI.RaceHudSource"/>).</summary>
        public static List<CourseSection> FindInScene()
        {
            return From(UnityEngine.Object.FindObjectsByType<FlappyCourseSection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }
    }
}
