using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 내 새가 새 구간에 들어섰나(spec §5). 배너·LAST STRETCH!·음악 올리기가 이 답 하나를 본다.
    /// <para>규칙: (1) 처음 볼 때(구간 목록을 처음 받은 장 포함) 이미 지난 구간은 알리지 않는다 — 판 중간에 HUD가 열려도
    /// 지난 순간을 다시 터뜨리지 않는다. (2) 출발 전·결승 뒤(<c>racing</c> false)엔 자리만 따라가고 알리지 않는다.
    /// (3) 한 번에 여러 표식을 넘으면 마지막 것만. (4) 뒤로 밀렸다 다시 넘어도 다시 안 알린다.</para>
    /// <para>목록은 x 오름차순이어야 한다(<see cref="CourseSections"/>가 그렇게 준다).</para>
    /// </summary>
    public sealed class CourseSectionTracker
    {
        public const int None = -1;

        private int passed = None;
        private bool seen;

        /// <param name="x">내 새의 그려진 x(내 새는 늘 실제 자리에 그린다 — 사용자 결정 10-25).</param>
        /// <param name="racing">출발했고 아직 결승하지 않았나.</param>
        /// <returns>새로 들어선 구간 번호, 없으면 <see cref="None"/>.</returns>
        public int Observe(IReadOnlyList<CourseSection> sections, float x, bool racing)
        {
            if (sections == null || sections.Count == 0)
            {
                return None;
            }

            int at = None;
            for (int i = 0; i < sections.Count; i++)
            {
                if (x >= sections[i].X)
                {
                    at = i;
                }
            }

            bool first = seen == false;
            seen = true;
            if (at <= passed)
            {
                return None;
            }
            passed = at;
            return first || racing == false ? None : at;
        }
    }
}
