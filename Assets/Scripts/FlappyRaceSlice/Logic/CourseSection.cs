namespace FlappyRace
{
    /// <summary>코스가 얼마나 무너졌는가. 순서가 곧 진행 방향이다.</summary>
    public enum CourseSection
    {
        /// <summary>아직 서 있는 사무실.</summary>
        Intact = 0,
        /// <summary>반쯤 무너진 중층 — 유리가 깨지고 철골이 드러났다.</summary>
        Exposed = 1,
        /// <summary>붕괴 한가운데 — 그을린 철골과 잔불.</summary>
        Charred = 2,
    }

    /// <summary>
    /// 코스의 어디쯤인가를 구간으로 바꾼다.
    ///
    /// <para><b>왜 비율인가</b>: 경계를 미터로 박으면 코스 길이를 바꿨을 때 조용히 틀려진다
    /// (회랑 하한 4.912가 물리 변경 뒤에도 문서에 남아 있던 사고와 같은 종류다). 길이가
    /// 60초에서 90초로 늘어도 "앞 3분의 1은 멀쩡하다"는 뜻은 그대로여야 한다.</para>
    ///
    /// <para><b>왜 MapTools가 아니라 여기 사는가</b>: 맵 검사·빌더(에디터)와 런타임 대기
    /// (<see cref="LOP.FlappyAtmosphere"/>)가 <b>둘 다</b> 쓴다. <c>LOP.MapTools</c>는
    /// 에디터 전용 어셈블리라 런타임이 참조할 수 없다 — 에디터에서는 멀쩡히 컴파일되고
    /// <b>플레이어 빌드에서만</b> 깨진다(실제로 콘텐츠 빌드가 그렇게 실패했다).</para>
    /// </summary>
    public static class CourseSectionRule
    {
        /// <summary>구간 개수. 삼등분이라는 사실이 여기 한 곳에만 있다.</summary>
        public const int Count = 3;

        /// <summary>코스를 0(시작)~1(끝)로 잰다. 코스 밖은 잘린다.</summary>
        public static float Progress(float x, float startX, float courseLength)
        {
            if (courseLength <= 0f)
            {
                return 0f;
            }
            float t = (x - startX) / courseLength;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        /// <summary>경계값은 <b>뒤 구간</b>에 속한다(204m는 2구간의 첫 미터다).</summary>
        public static CourseSection Of(float x, float startX, float courseLength)
        {
            float t = Progress(x, startX, courseLength);
            int index = (int)(t * Count);
            if (index >= Count)
            {
                index = Count - 1;   // t == 1(결승선)은 마지막 구간이다
            }
            return (CourseSection)index;
        }
    }
}
