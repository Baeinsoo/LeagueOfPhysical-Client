namespace LOP
{
    public enum CardRushGagKind { None, Hop, DoorRun, Splash, Slap, Finish }

    /// <summary>몸개그 자세 — 몸 뿌리(시뮬 자리)에서의 오프셋과 기울기. 시작·끝은 늘 원래 자세다.</summary>
    public readonly struct CardRushPose
    {
        public readonly UnityEngine.Vector3 Offset;     // 진행 방향 기준(x 오른쪽, y 위, z 앞)
        public readonly UnityEngine.Vector3 EulerDeg;   // 몸 기울기(x 앞뒤, z 좌우). y는 결승 몸짓의 제자리 돌기만
        public readonly float Squash;                   // 1 = 그대로
        public CardRushPose(UnityEngine.Vector3 offset, UnityEngine.Vector3 eulerDeg, float squash) { Offset = offset; EulerDeg = eulerDeg; Squash = squash; }
        public static readonly CardRushPose Rest = new CardRushPose(UnityEngine.Vector3.zero, UnityEngine.Vector3.zero, 1f);
    }

    /// <summary>
    /// 몸개그(spec §7.4)는 (상태, 그림 틱)의 순수 함수다. 시뮬 자리는 레인 위 한 점뿐이고, 몸개그는 그 점에서 떠나
    /// 그 점으로 돌아오는 장식이라 멈춤·이동이 끝나는 틱에 그림이 정확히 시뮬 자리에 있다(시간을 늘리지 않는다).
    /// </summary>
    public static class CardRushGag
    {
        public const float HopHeight = 0.8f;
        public const float SplashDepth = 1.1f;

        /// <summary>철썩(문 박치기 테마의 틀림) — 던지며 숙이는 각, 카드에 맞아 머리가 젖혀지는 각, 뒤로 벌러덩 눕는 각.</summary>
        public const float SlapLeanDegrees = 10f;
        public const float SlapSnapDegrees = 20f;
        public const float SlapFallDegrees = 80f;

        /// <summary>풍덩 첫머리에 앞 돌 쪽으로 반쯤 뛰어오르는 높이 — 폴짝(0.8 m)보다 낮게 "가다 만" 느낌.</summary>
        public const float SplashLeapHeight = 0.5f;

        /// <summary>허우적 동안 머리가 위아래로 까딱이는 폭 — 머리만 나온 채 허둥대는 느낌.</summary>
        public const float SplashBob = 0.1f;

        /// <summary>물에서 퐁 튀어나와 제 돌로 돌아오는 높이.</summary>
        public const float SplashPopHeight = 0.35f;

        /// <summary>결승 몸짓(두 번 뛰며 한 바퀴) 길이. 결승 뒤엔 시뮬 자리가 안 움직이니 자유다.</summary>
        public const float FinishSeconds = 1f;

        public static int FinishTicks => CardRushTiming.Ticks(FinishSeconds);

        /// <summary>그 틱에 어떤 몸개그가 몇 % 진행됐나 — 상태만 보고 정한다.</summary>
        public static (CardRushGagKind kind, float t01) Phase(CardRushTheme theme, in CardRushRunnerState s, double drawnTick, in CardRushTiming timing, int stepCount)
        {
            if (s.StunEndTick != 0 && drawnTick >= s.StunEndTick - timing.StunTicks && drawnTick < s.StunEndTick)
                return (theme == CardRushTheme.StoneBridge ? CardRushGagKind.Splash : CardRushGagKind.Slap,
                        (float)((drawnTick - (s.StunEndTick - timing.StunTicks)) / timing.StunTicks));
            if (s.MoveStartTick != 0 && drawnTick >= s.MoveStartTick && drawnTick < s.MoveStartTick + timing.MoveTicks)
                return (theme == CardRushTheme.StoneBridge ? CardRushGagKind.Hop : CardRushGagKind.DoorRun,
                        (float)((drawnTick - s.MoveStartTick) / timing.MoveTicks));
            //  완주 = 마지막으로 맞힌 이동이 결승 이동이다. 그 이동이 끝난 뒤 1초.
            if (s.IsFinished(stepCount) && s.MoveStartTick != 0)
            {
                double since = drawnTick - (s.MoveStartTick + timing.MoveTicks);
                if (since >= 0 && since < FinishTicks)
                    return (CardRushGagKind.Finish, (float)(since / FinishTicks));
            }
            return (CardRushGagKind.None, 0f);
        }

        /// <summary>몸개그 자세. 0과 1에서 Rest — 멈춤·이동이 끝나는 틱에 그림이 시뮬 자리로 돌아온다.</summary>
        public static CardRushPose PoseAt(CardRushGagKind kind, float t01, float stepSpacing)
        {
            t01 = UnityEngine.Mathf.Clamp01(t01);
            if (t01 <= 0f || t01 >= 1f) return CardRushPose.Rest;
            switch (kind)
            {
                case CardRushGagKind.Hop:
                {
                    float y = 4f * HopHeight * t01 * (1f - t01);   // 포물선, 시뮬이 앞으로 옮기니 높이만
                    return new CardRushPose(new UnityEngine.Vector3(0, y, 0), new UnityEngine.Vector3(-10f * (1f - 2f * t01), 0, 0), 1f);
                }
                case CardRushGagKind.DoorRun:
                    return new CardRushPose(UnityEngine.Vector3.zero, new UnityEngine.Vector3(15f * UnityEngine.Mathf.Sin(t01 * UnityEngine.Mathf.PI), 0, 0), 1f);
                case CardRushGagKind.Splash:
                {
                    // 0~0.25 앞 돌로 반쯤 뛰어오름 / 0.25~0.40 빠짐 / 0.40~0.75 허우적(머리만, 위아래·좌우) / 0.75~1 물에서 퐁 튀어나와 제 돌로.
                    // 빠지는 자리는 두 돌 사이(반 칸 앞) — 제자리로 빠지면 제 돌이 몸을 가른다. 앞으로 간 만큼은 돌아오며 되돌린다.
                    float fwd = Plateau(t01, 0f, 0.25f, 0.80f, 1f) * 0.5f * stepSpacing;
                    float leap = t01 < 0.25f ? 4f * SplashLeapHeight * (t01 / 0.25f) * (1f - t01 / 0.25f) : 0f;
                    float down = Plateau(t01, 0.25f, 0.40f, 0.75f, 0.88f) * SplashDepth;
                    float bob = (t01 > 0.40f && t01 < 0.75f) ? SplashBob * UnityEngine.Mathf.Sin((t01 - 0.40f) * 55f) : 0f;
                    float pop = t01 > 0.86f ? 4f * SplashPopHeight * ((t01 - 0.86f) / 0.14f) * (1f - (t01 - 0.86f) / 0.14f) : 0f;
                    float flail = (t01 > 0.40f && t01 < 0.75f) ? 20f * UnityEngine.Mathf.Sin((t01 - 0.40f) * 40f) : 0f;
                    return new CardRushPose(new UnityEngine.Vector3(0, leap - down + bob + pop, fwd), new UnityEngine.Vector3(25f * Plateau(t01, 0.2f, 0.35f, 0.75f, 0.9f), 0, flail), 1f);
                }
                case CardRushGagKind.Slap:
                {
                    // 0~0.10 던지며 앞으로 숙임 / 0.10~0.23 섬(카드가 돌아오는 걸 본다) / 0.23~0.30 철썩 — 머리가 뒤로 젖혀지고 찌그러짐 /
                    // 0.30~0.75 발은 제자리, 뒤로 벌러덩 / 0.75~1 벌떡(넘침 후 섬). 뒤로 미끄러지지 않는다(카메라 밖으로 나간다).
                    // 철썩 0.23 = 던짐 뒤 SlapTick(7틱) ÷ 멈춤 30틱 — 카드가 얼굴에 닿는 그 틱.
                    float lean = SlapLeanDegrees * Bump(t01, 0f, 0.05f, 0.10f);
                    float snap = SlapSnapDegrees * Plateau(t01, 0.23f, 0.26f, 0.30f, 0.34f);
                    float fall = Plateau(t01, 0.30f, 0.45f, 0.75f, 0.92f) * SlapFallDegrees;
                    float overshoot = t01 > 0.92f ? -8f * UnityEngine.Mathf.Sin((t01 - 0.92f) / 0.08f * UnityEngine.Mathf.PI) : 0f;
                    float squash = t01 > 0.23f && t01 < 0.30f ? 0.9f : 1f;
                    return new CardRushPose(UnityEngine.Vector3.zero, new UnityEngine.Vector3(lean - snap - fall + overshoot, 0, 0), squash);
                }
                case CardRushGagKind.Finish:
                {
                    // 두 번 뛰며(0.25·0.75에 꼭대기) 한 바퀴. 0·0.5·1에 땅.
                    float y = HopHeight * UnityEngine.Mathf.Abs(UnityEngine.Mathf.Sin(t01 * 2f * UnityEngine.Mathf.PI));
                    float yaw = 360f * UnityEngine.Mathf.SmoothStep(0f, 1f, t01);
                    return new CardRushPose(new UnityEngine.Vector3(0, y, 0), new UnityEngine.Vector3(0, yaw, 0), 1f);
                }
                default:
                    return CardRushPose.Rest;
            }
        }

        // a~b에서 0→1, b~c에서 1→0 (그 밖 0)
        private static float Bump(float t, float a, float b, float c)
            => t <= a || t >= c ? 0f : t < b ? (t - a) / (b - a) : 1f - (t - b) / (c - b);

        // a~b에서 0→1, b~c 유지, c~d에서 1→0 (그 밖 0)
        private static float Plateau(float t, float a, float b, float c, float d)
            => t <= a || t >= d ? 0f : t < b ? (t - a) / (b - a) : t <= c ? 1f : 1f - (t - c) / (d - c);
    }
}
