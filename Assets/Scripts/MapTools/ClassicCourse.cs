using System;
using System.Collections.Generic;
using GameFramework.Rng;

namespace LOP.MapTools
{
    /// <summary>파이프 한 쌍 — 그 x에서 창이 어디에 뚫려 있는가.</summary>
    public readonly struct CoursePipe
    {
        public readonly float X;
        /// <summary>창의 한가운데 높이. 위아래 파이프는 여기서 창 절반씩 떨어진 곳부터 시작한다.</summary>
        public readonly float GapCenter;

        public CoursePipe(float x, float gapCenter)
        {
            X = x;
            GapCenter = gapCenter;
        }
    }

    /// <summary>
    /// <b>전통 플래피 코스</b>의 배치 — 바닥과 천장이 평평한 회랑에 파이프 쌍을 일정 간격으로
    /// 놓고, 창 높이만 매번 바꾼다. 원본에 있는 것만 있다: 갈림길도, 도는 장애물도, 고저차도 없다.
    ///
    /// <para><b>왜 순수 계층에 두나</b>: 이 배치가 지켜야 할 것(창이 회랑 안에 온전히 들어간다 ·
    /// 간격이 정확하다 · 관문 사이 높이차가 갈 수 있는 만큼이다)은 씬 없이 잴 수 있고, 씬을 굽고
    /// 나서 검사기로 확인하는 것보다 훨씬 싸게 잡힌다.</para>
    ///
    /// <para><b>높이차에 상한을 두는 이유</b>: 다음 창이 아무 데나 있으면 1.67초 안에 못 가는
    /// 자리가 생긴다. 그런 관문은 운으로만 통과되므로 배치 단계에서 막는다.</para>
    /// </summary>
    public static class ClassicCourseRule
    {
        /// <summary>
        /// 파이프 쌍을 <paramref name="spacing"/> 간격으로 놓는다. 첫 파이프는 시작선에서
        /// 한 간격 뒤다 — 출발하자마자 관문이면 스폰 높이가 통과를 정해 버린다.
        /// </summary>
        /// <param name="maxStep">이웃한 두 창의 높이차 상한. 0 이하면 창이 안 움직인다.</param>
        /// <param name="centerAt">
        /// 그 x에서 회랑 중심이 얼마나 올라가 있나(<see cref="CourseElevation"/>). null이면 평평하다.
        /// 창은 이 값 위에서 랜덤워크하므로 <b>회랑 안에 들어간다는 보장은 그대로</b>다.
        /// </param>
        public static List<CoursePipe> Layout(float startX, float courseLength, float spacing,
                                              float floorY, float ceilingY, float window,
                                              float maxStep, ulong seed,
                                              System.Func<float, float> centerAt = null)
        {
            var pipes = new List<CoursePipe>();
            if (spacing <= 0f || courseLength <= 0f)
            {
                return pipes;
            }
            //  창이 회랑 안에 온전히 들어가야 하므로 중심이 갈 수 있는 범위는 위아래로 창 절반씩 좁다.
            //  고저차를 주면 이 범위가 통째로 따라 움직인다 — 폭은 그대로라 통과 난이도가 안 변한다.
            float low = floorY + window * 0.5f;
            float high = ceilingY - window * 0.5f;
            if (high < low)
            {
                throw new ArgumentOutOfRangeException(nameof(window), window,
                    $"window {window} does not fit in the corridor [{floorY}, {ceilingY}].");
            }

            var rng = new DeterministicRandom(seed);
            float center = (low + high) * 0.5f;
            float previousLift = centerAt != null ? centerAt(startX) : 0f;
            for (float x = startX + spacing; x <= startX + courseLength + 1e-4f; x += spacing)
            {
                //  <b>새가 실제로 날아야 하는 거리는 절대값</b>이다 — 회랑이 내려간 것이든 창이
                //  내려간 것이든 똑같이 날아야 한다. 그래서 고저차가 먹은 만큼 워크 예산을 줄인다.
                //  안 줄이면 가파른 구간에서 관문 사이 높이차가 워크(6m) + 고저차(최대 5.6m)로
                //  두 배가 되어 따라갈 수 없는 자리가 생긴다.
                float lift = centerAt != null ? centerAt(x) : 0f;
                float spent = Math.Abs(lift - previousLift);
                float budget = maxStep - spent;
                if (budget < 0f) { budget = 0f; }
                previousLift = lift;

                //  범위 밖으로 나가면 <b>되튄다</b>(접는다). 잘라 버리면 벽에 붙은 창이 연달아
                //  나와 "위만 보고 가면 되는" 구간이 생긴다.
                float next = center + rng.Range(-budget, budget);
                if (next < low) { next = low + (low - next); }
                if (next > high) { next = high - (next - high); }
                center = next < low ? low : (next > high ? high : next);
                //  랜덤워크는 <b>평평한 기준선 위에서</b> 돌고, 고저차는 마지막에 더한다.
                //  워크 자체에 더하면 되튀기(low/high 접기)가 움직이는 벽을 상대하게 되어
                //  진폭이 클 때 창이 회랑 밖으로 샌다.
                pipes.Add(new CoursePipe(x, center + lift));
            }
            return pipes;
        }

        /// <summary>
        /// 배치가 지켜야 할 것을 한 번에 확인한다. 어긋난 첫 자리를 말로 돌려준다.
        ///
        /// <para><paramref name="centerAt"/>는 <see cref="Layout"/>에 넘긴 것과 <b>같은 것</b>을
        /// 줘야 한다. 회랑이 오르내리면 창도 같이 오르내리므로, 평평한 범위와 비교하면 멀쩡한
        /// 배치가 전부 "회랑 밖"으로 나온다(실제로 그래서 빌더가 멈춰 섰다).</para>
        /// </summary>
        public static string Validate(IReadOnlyList<CoursePipe> pipes, float floorY, float ceilingY,
                                      float window, float spacing, float maxStep,
                                      System.Func<float, float> centerAt = null)
        {
            if (pipes == null || pipes.Count == 0)
            {
                return "파이프가 하나도 없다";
            }
            float low = floorY + window * 0.5f;
            float high = ceilingY - window * 0.5f;
            for (int i = 0; i < pipes.Count; i++)
            {
                CoursePipe p = pipes[i];
                //  회랑 중심을 빼고 본다 — 남는 것이 "회랑 안 어디에 뚫려 있나"다.
                float relative = p.GapCenter - (centerAt != null ? centerAt(p.X) : 0f);
                if (relative < low - 1e-3f || relative > high + 1e-3f)
                {
                    return $"x={p.X:F1}의 창이 회랑 밖으로 나갔다 (중심 {relative:F2}, 허용 {low:F2}~{high:F2})";
                }
                if (i == 0)
                {
                    continue;
                }
                float gap = p.X - pipes[i - 1].X;
                if (Math.Abs(gap - spacing) > 1e-3f)
                {
                    return $"x={p.X:F1}의 간격이 {gap:F2}m다 (목표 {spacing:F2}m)";
                }
                //  높이차는 <b>절대값</b>으로 본다. 회랑이 움직인 것이든 창이 움직인 것이든
                //  새는 똑같이 날아야 한다 — 상대값으로 재면 가파른 구간에서 두 배로 벌어진
                //  높이차를 놓친다(실제로 그래서 봇이 x=204에서 막혔다).
                float step = Math.Abs(p.GapCenter - pipes[i - 1].GapCenter);
                if (step > maxStep + 1e-3f)
                {
                    return $"x={p.X:F1}에서 창이 {step:F2}m 움직였다 (상한 {maxStep:F2}m)";
                }
            }
            return null;
        }
    }
}
