using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 회랑 중심의 높낮이. <b>끊기지 않는가</b>와 <b>뒤로 갈수록 커지는가</b>를 못박는다 —
    /// 구간 경계에서 튀면 통과 불가능한 자리가 생긴다.
    /// </summary>
    public class CourseElevationTests
    {
        const float StartX = 0f;
        const float Length = 612f;

        [Test]
        public void 시작점의_높이는_0이다()
        {
            //  스폰 자리가 흔들리면 안 된다.
            Assert.AreEqual(0f, CourseElevation.CenterY(StartX, StartX, Length), 1e-4f);
        }

        [Test]
        public void 기울기가_어디서도_상한을_넘지_않는다()
        {
            //  파장 90m·진폭 7m이면 최대 기울기는 amp·2π/λ = 0.49 m/m다. 0.7을 넘으면 파장이
            //  줄었거나 진폭이 커진 것이고, 그만큼 회랑이 가파르게 오르내려 "따라갈 수 있는
            //  경사"라는 전제가 깨진다.
            //
            //  ⚠️ 이 테스트는 원래 "끊기지 않는가"를 본다고 적어 뒀는데, 그건 거짓이었다 —
            //  파장을 x에 따라 바꿔도 값은 연속이라 절대 안 잡힌다(변이를 넣어 확인했다).
            //  실제로 지킬 수 있는 것은 <b>기울기 상한</b>이라 그것으로 바꿨다.
            const float MaxSlope = 0.7f;
            float previous = CourseElevation.CenterY(StartX, StartX, Length);
            for (float x = StartX + 1f; x <= StartX + Length; x += 1f)
            {
                float current = CourseElevation.CenterY(x, StartX, Length);
                Assert.Less(System.Math.Abs(current - previous), MaxSlope, $"x={x}에서 너무 가파르다");
                previous = current;
            }
        }

        [Test]
        public void 뒤로_갈수록_높낮이가_커진다()
        {
            float front = Swing(StartX, StartX + Length / 3f);
            float back = Swing(StartX + Length * 2f / 3f, StartX + Length);

            Assert.Greater(back, front * 1.5f, "구간 3이 구간 1보다 확실히 커야 한다");
        }

        [Test]
        public void 회랑이_탐색_대역을_벗어나지_않는다()
        {
            //  진폭이 너무 크면 바닥·천장이 봇의 탐색 대역(±27.3m) 밖으로 나가 검사가 거짓말한다.
            for (float x = StartX; x <= StartX + Length; x += 2f)
            {
                Assert.Less(System.Math.Abs(CourseElevation.CenterY(x, StartX, Length)), 10f);
            }
        }

        [Test]
        public void 길이가_0이면_평평하다()
        {
            Assert.AreEqual(0f, CourseElevation.CenterY(100f, StartX, 0f), 1e-4f);
        }

        static float Swing(float from, float to)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (float x = from; x <= to; x += 1f)
            {
                float v = CourseElevation.CenterY(x, StartX, Length);
                if (v < lo) { lo = v; }
                if (v > hi) { hi = v; }
            }
            return hi - lo;
        }
    }
}
