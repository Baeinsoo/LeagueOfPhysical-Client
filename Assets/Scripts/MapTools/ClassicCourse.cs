using System;
using System.Collections.Generic;
using GameFramework.Rng;

namespace LOP.MapTools
{
    /// <summary>
    /// 한 x의 파이프 기둥 — 창이 어디에 뚫려 있는가.
    ///
    /// <para>대부분은 창이 하나다. <b>도전 구간</b>에서는 둘이 되어, 안전한 쪽(<see cref="GapCenter"/>)과
    /// 먼 쪽(<see cref="ChallengeCenter"/>) 중에 고르게 된다. 먼 쪽으로 뛰어들면 낙차만큼 대시
    /// 게이지를 벌지만 다음 관문까지 되돌아와야 한다 — 위험을 <b>고를 수 있게</b> 하는 것이 요점이다.</para>
    /// </summary>
    public readonly struct CoursePipe
    {
        public readonly float X;

        /// <summary>안전한 창의 한가운데 높이. 이전 관문에서 크게 안 벗어난다.</summary>
        public readonly float GapCenter;

        /// <summary>도전 창의 한가운데. <see cref="HasChallenge"/>가 false면 뜻이 없다.</summary>
        public readonly float ChallengeCenter;

        /// <summary>이 기둥에 창이 둘인가.</summary>
        public readonly bool HasChallenge;

        public CoursePipe(float x, float gapCenter)
        {
            X = x;
            GapCenter = gapCenter;
            ChallengeCenter = 0f;
            HasChallenge = false;
        }

        public CoursePipe(float x, float gapCenter, float challengeCenter)
        {
            X = x;
            GapCenter = gapCenter;
            ChallengeCenter = challengeCenter;
            HasChallenge = true;
        }

        /// <summary>두 창 사이의 낙차. 창이 하나면 0.</summary>
        public float ChallengeDrop => HasChallenge ? Math.Abs(ChallengeCenter - GapCenter) : 0f;
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
        /// <summary>도전 구간 하나가 몇 관문인가. 지그재그가 되려면 <b>연속</b>이어야 한다 —
        /// 하나만 떨어져 있으면 내려갔다 올라오는 일회성이지 리듬이 아니다.</summary>
        public const int ChallengeRunLength = 3;

        /// <summary>두 창이 물리적으로 들어가려면 필요한 최소 낙차(창 + 중간 기둥).</summary>
        public const float MinChallengeDrop = 5.87f;

        /// <summary>
        /// 파이프 쌍을 <paramref name="spacing"/> 간격으로 놓는다. 첫 파이프는 시작선에서
        /// 한 간격 뒤다 — 출발하자마자 관문이면 스폰 높이가 통과를 정해 버린다.
        /// </summary>
        /// <param name="maxStep">이웃한 두 창의 높이차 상한. 0 이하면 창이 안 움직인다.</param>
        /// <param name="centerAt">
        /// 그 x에서 회랑 중심이 얼마나 올라가 있나(<see cref="CourseElevation"/>). null이면 평평하다.
        /// 창은 이 값 위에서 랜덤워크하므로 <b>회랑 안에 들어간다는 보장은 그대로</b>다.
        /// </param>
        /// <param name="challengeRuns">
        /// 창이 둘인 <b>도전 구간</b>을 코스에 몇 군데 둘 것인가. 0이면 전부 창 하나(옛 동작).
        /// </param>
        public static List<CoursePipe> Layout(float startX, float courseLength, float spacing,
                                              float floorY, float ceilingY, float window,
                                              float maxStep, ulong seed,
                                              System.Func<float, float> centerAt = null,
                                              int challengeRuns = 0)
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

            //  도전 구간의 첫 관문 번호들을 먼저 뽑는다. 코스를 고르게 나눠 그 안에서 흔들어,
            //  "약 150m마다 한 번"이 되게 한다 — 몰려 있으면 나머지가 통째로 심심해진다.
            int totalGates = (int)((courseLength - 1e-4f) / spacing);
            var runStarts = new HashSet<int>();
            if (challengeRuns > 0 && totalGates > ChallengeRunLength * 2)
            {
                //  <b>다른 난수 줄기</b>를 쓴다. 같은 줄기를 쓰면 도전 구간을 켜는 순간 안전선의
                //  난수까지 밀려서 기본 맵이 통째로 달라진다 — "기존 느낌은 그대로"가 깨진다.
                var runRng = new DeterministicRandom(seed ^ 0x9E3779B97F4A7C15UL);
                int slot = totalGates / challengeRuns;
                for (int r = 0; r < challengeRuns; r++)
                {
                    int lo = r * slot + 1;
                    int hi = System.Math.Max(lo + 1, (r + 1) * slot - ChallengeRunLength);
                    runStarts.Add(runRng.Range(lo, hi));
                }
            }

            //  도전 창은 안전 창의 <b>반대쪽 끝</b>에 놓고, 구간 안에서 번갈아 뒤집는다.
            //  같은 쪽만 나오면 "내려갔다 올라오기"의 반복이지 지그재그가 아니다.
            int gateIndex = 0;
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
                gateIndex++;
                float safeCenter = center + lift;

                //  이 관문이 어느 도전 구간 안인가. 구간 안이면 두 번째 창을 시도한다.
                int inRun = -1;
                foreach (int start in runStarts)
                {
                    if (gateIndex >= start && gateIndex < start + ChallengeRunLength)
                    {
                        inRun = gateIndex - start;
                        break;
                    }
                }

                if (inRun < 0)
                {
                    pipes.Add(new CoursePipe(x, safeCenter));
                    continue;
                }

                //  안전 창에서 가장 먼 끝. 거기까지의 낙차가 최소치를 못 넘으면 두 창이 안 들어간다
                //  (안전 창이 밴드 한가운데일 때 그렇다) — 그런 자리는 창 하나로 둔다.
                float far = (inRun % 2 == 0)
                    ? ((center - low) > (high - center) ? low : high)
                    : ((center - low) > (high - center) ? high : low);
                if (System.Math.Abs(far - center) < MinChallengeDrop)
                {
                    far = (center - low) > (high - center) ? low : high;
                }
                if (System.Math.Abs(far - center) < MinChallengeDrop)
                {
                    pipes.Add(new CoursePipe(x, safeCenter));
                    continue;
                }
                pipes.Add(new CoursePipe(x, safeCenter, far + lift));
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
                    continue;   // 첫 관문은 앞이 없어 간격·높이차를 못 잰다
                }
                float gap = p.X - pipes[i - 1].X;
                if (Math.Abs(gap - spacing) > 1e-3f)
                {
                    return $"x={p.X:F1}의 간격이 {gap:F2}m다 (목표 {spacing:F2}m)";
                }
                //  높이차는 <b>절대값</b>으로 본다. 회랑이 움직인 것이든 창이 움직인 것이든
                //  새는 똑같이 날아야 한다 — 상대값으로 재면 가파른 구간에서 두 배로 벌어진
                //  높이차를 놓친다(실제로 그래서 봇이 x=204에서 막혔다).
                //  도전 창도 회랑 안에 온전히 들어가야 하고, 안전 창과 겹치면 안 된다.
                if (p.HasChallenge)
                {
                    float challengeRelative = p.ChallengeCenter - (centerAt != null ? centerAt(p.X) : 0f);
                    if (challengeRelative < low - 1e-3f || challengeRelative > high + 1e-3f)
                    {
                        return $"x={p.X:F1}의 도전 창이 회랑 밖으로 나갔다 (중심 {challengeRelative:F2})";
                    }
                    if (p.ChallengeDrop < MinChallengeDrop - 1e-3f)
                    {
                        return $"x={p.X:F1}의 두 창이 {p.ChallengeDrop:F2}m로 너무 가깝다"
                             + $" (최소 {MinChallengeDrop:F2}m — 중간 기둥이 안 들어간다)";
                    }
                }

                //  <b>안전선</b>의 높이차만 잰다. 도전 창은 고르는 사람만 가므로 통과 가능성의
                //  기준이 아니다 — 안전선이 끊기지 않는 것이 "누구나 깰 수 있다"의 뜻이다.
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
