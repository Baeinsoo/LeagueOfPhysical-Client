using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>구간별 예산 한 점 — 그 시점의 클린런 위치와, 허용/가능 스턴 횟수.</summary>
    public readonly struct StunBudgetPoint
    {
        public readonly float ElapsedSeconds;
        public readonly float CleanRunX;
        public readonly int AllowedStuns;
        public readonly int PossibleStuns;

        public StunBudgetPoint(float elapsedSeconds, float cleanRunX, int allowedStuns, int possibleStuns)
        {
            ElapsedSeconds = elapsedSeconds;
            CleanRunX = cleanRunX;
            AllowedStuns = allowedStuns;
            PossibleStuns = possibleStuns;
        }
    }

    /// <summary>가장 빨리 잡히는 경우. <see cref="Caught"/>가 false면 골인 전에 잡힐 수 없다.</summary>
    public readonly struct EarliestCatch
    {
        public readonly bool Caught;
        public readonly float Seconds;
        public readonly int StunCount;

        public EarliestCatch(bool caught, float seconds, int stunCount)
        {
            Caught = caught;
            Seconds = seconds;
            StunCount = stunCount;
        }
    }

    /// <summary>
    /// 추격자에게 잡히기 전까지 몇 번이나 스턴을 먹어도 되는가.
    ///
    /// <para>가정: 스턴이 아닌 시간엔 전진 속도로 온전히 나아간다. 실제로는 무적 중에도 벽에
    /// 막히면 못 나가므로 <b>실제는 이보다 나쁘다</b> — 즉 여기 나오는 횟수는 상한이다.
    /// "이 횟수를 넘으면 반드시 잡힌다"이지 "넘지 않으면 안 잡힌다"가 아니다.
    /// 막혀서 기는 경우는 낌 검사가 따로 잡는다.</para>
    /// </summary>
    public static class StunBudget
    {
        /// <summary>이 시점까지 멈춰 있어도 되는 총 시간. 음수면 이미 잡혔다는 뜻이다.</summary>
        public static float AllowedStallSeconds(in FlappyConfig config, float elapsedSeconds,
                                                float startX, float finishX)
        {
            float wallX = FlappyChaserCurve.XAt(config, elapsedSeconds, finishX);
            //  잡힘 판정이 "새 뒷면 ≤ 벽"이라 반지름만큼 더 가 있어야 한다.
            float needed = (wallX - startX + config.BodyRadius) / config.ForwardSpeed;
            return elapsedSeconds - needed;
        }

        public static int AllowedStuns(in FlappyConfig config, float elapsedSeconds, float startX, float finishX)
        {
            float allowed = AllowedStallSeconds(config, elapsedSeconds, startX, finishX);
            if (allowed <= 0f)
            {
                return 0;
            }
            return (int)(allowed / config.StunTime);
        }

        /// <summary>
        /// 이 시점까지 물리적으로 맞을 수 있는 최대 횟수. 스턴이 끝나면 무적이 붙어 그 사이엔
        /// 다시 안 걸리므로, n번 맞으려면 최소 (스턴+무적)×n − 무적 초가 든다.
        /// </summary>
        public static int PossibleStuns(in FlappyConfig config, float elapsedSeconds)
        {
            float cycle = config.StunTime + config.InvulnTime;
            if (cycle <= 0f)
            {
                return 0;
            }
            int count = (int)((elapsedSeconds + config.InvulnTime) / cycle);
            return count < 0 ? 0 : count;
        }

        public static EarliestCatch FindEarliestCatch(in FlappyConfig config, float startX, float finishX)
        {
            float cycle = config.StunTime + config.InvulnTime;
            for (int n = 1; n <= 10000; n++)
            {
                //  n번째 스턴이 끝나는 가장 이른 시각. 그 순간 총 정지시간은 스턴×n이다.
                float t = cycle * n - config.InvulnTime;
                float stalled = config.StunTime * n;

                //  그 시각에 이미 골인했으면 더 볼 것이 없다 — 완주자는 판정에서 빠진다.
                float x = startX + config.ForwardSpeed * (t - stalled);
                if (x >= finishX)
                {
                    return new EarliestCatch(false, 0f, 0);
                }
                if (stalled >= AllowedStallSeconds(config, t, startX, finishX))
                {
                    return new EarliestCatch(true, t, n);
                }
            }
            return new EarliestCatch(false, 0f, 0);
        }

        /// <summary>출발부터 클린런 골인 시각까지 <paramref name="stepSeconds"/> 간격으로 훑는다.</summary>
        public static List<StunBudgetPoint> Curve(in FlappyConfig config, float startX, float finishX,
                                                  float stepSeconds)
        {
            var points = new List<StunBudgetPoint>();
            float cleanRunSeconds = (finishX - startX) / config.ForwardSpeed;
            for (float t = stepSeconds; t < cleanRunSeconds; t += stepSeconds)
            {
                points.Add(new StunBudgetPoint(t, startX + config.ForwardSpeed * t,
                                               AllowedStuns(config, t, startX, finishX),
                                               PossibleStuns(config, t)));
            }
            //  골인 시각은 간격에 안 걸려도 반드시 넣는다 — 마지막 여유가 얼마인지가 필요하다.
            points.Add(new StunBudgetPoint(cleanRunSeconds, finishX,
                                           AllowedStuns(config, cleanRunSeconds, startX, finishX),
                                           PossibleStuns(config, cleanRunSeconds)));
            return points;
        }
    }
}
