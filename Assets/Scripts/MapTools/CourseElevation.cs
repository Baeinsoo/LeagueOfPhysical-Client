using FlappyRace;

namespace LOP.MapTools
{
    /// <summary>
    /// 회랑 중심을 진행률에 따라 흔든다. 구간 1은 완만하고 구간 3은 뾰족하다.
    ///
    /// <para><b>파장을 고정한 이유</b>: x에 따라 파장을 바꾸면 위상이 <c>(x-x0)/λ(x)</c>가 되어
    /// <b>실제 국소 파장이 넣은 값과 달라진다</b>(λ의 변화율이 위상에 섞여 든다). 값이 끊기지는
    /// 않지만 "여기 파장 60m"라고 적어 둔 것이 사실이 아니게 되고, 뒤로 갈수록 기울기가
    /// 의도보다 가팔라진다. 그래서 파장은 한 값으로 두고 <b>진폭과 뾰족함만</b> 진행률을 따라
    /// 바꾼다 — 의도한 감각("뒤로 갈수록 아슬아슬하다")은 그대로 나온다.</para>
    ///
    /// <para><b>고저차만으로는 다이브가 깊어지지 않는다.</b> 이 파도는 평균 2 m/s 남짓으로
    /// 내려가므로 낙하 시간을 0.2초도 못 늘린다 — 회랑이 14.56m인 한 최대 다이브는 0.74초로
    /// 묶여 있다. 깊은 다이브는 갈림길의 아래 길이 준다. 이 곡선이 하는 일은 <i>리듬</i>이지
    /// 경제가 아니다.</para>
    /// </summary>
    public static class CourseElevation
    {
        /// <summary>한 번 오르내리는 거리. 전진 6.8 m/s에서 약 13초에 한 주기다.</summary>
        public const float Wavelength = 90f;

        public const float AmpStart = 2f;
        public const float AmpEnd = 7f;

        /// <summary>그 x에서 회랑 중심이 얼마나 올라가 있나(0 = 평평).</summary>
        public static float CenterY(float x, float startX, float courseLength)
        {
            if (courseLength <= 0f)
            {
                return 0f;
            }
            float t = CourseSectionRule.Progress(x, startX, courseLength);
            float amp = AmpStart + (AmpEnd - AmpStart) * t;
            return FlappyElevation.Blend(x, amp, startX, Wavelength, sharpness: t);
        }
    }
}
