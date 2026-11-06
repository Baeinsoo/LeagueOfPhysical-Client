namespace LOP
{
    public enum CardRushGagKind { None, Tumble, Squash, Block, Dash, Smash, Wait, Finish }

    /// <summary>몸개그 자세 — 몸 뿌리(시뮬 자리)에서의 오프셋과 기울기. 시작·끝은 늘 원래 자세다.</summary>
    public readonly struct CardRushPose
    {
        public readonly UnityEngine.Vector3 Offset;     // 진행 방향 기준(x 오른쪽, y 위, z 앞)
        public readonly UnityEngine.Vector3 EulerDeg;   // 몸 기울기(x 앞(+)뒤(−), z 좌우). y는 결승 몸짓의 제자리 돌기만
        public readonly float Squash;                   // 1 = 그대로
        public CardRushPose(UnityEngine.Vector3 offset, UnityEngine.Vector3 eulerDeg, float squash) { Offset = offset; EulerDeg = eulerDeg; Squash = squash; }
        public static readonly CardRushPose Rest = new CardRushPose(UnityEngine.Vector3.zero, UnityEngine.Vector3.zero, 1f);
    }

    /// <summary>
    /// 몸개그(spec 2026-11-05 §11.3)는 (상태, 그림 틱)의 순수 함수다. 시뮬 자리는 레인 위 한 점뿐이고(밀림은 시뮬에서 즉시),
    /// 몸개그는 그 점에서 떠나 그 점으로 돌아오는 자식 <c>Model</c>의 장식이라 기절이 끝나는 틱에 그림이 정확히 시뮬 자리에 있다.
    /// </summary>
    public static class CardRushGag
    {
        //  꽈당(spec §11.3 표): 0~0.15 뒤로 젖혀짐 → 0.15~0.6 뒤로 한 바퀴 반 구르며 위로 0.6 m → 0.6~0.85 엉덩방아 + 별 → 0.85~1 벌떡.
        public const float TumbleLeanEnd = 0.15f;
        public const float TumbleRollEnd = 0.6f;
        public const float TumbleSitEnd = 0.85f;
        public const float TumbleLeanDegrees = 30f;
        public const float TumbleTurns = 1.5f;
        public const float TumbleRiseHeight = 0.6f;
        /// <summary>엉덩방아 — 뒤로 기운 채 주저앉은 각과 눌림.</summary>
        public const float TumbleSitDegrees = 20f;
        public const float TumbleSitSquash = 0.8f;
        /// <summary>구르는 축(몸 가운데 높이) — 발목이 아니라 배를 축으로 돌아야 구르기로 읽힌다.</summary>
        public const float BodyCenterHeight = 0.55f;

        //  으악: 0~0.1 바위에 눌려 납작(높이 0.35) → 0.1~0.7 납작한 채 부들 + 별 → 0.7~0.85 퐁 튀어 오름(넘침) → 섬.
        public const float SquashPressEnd = 0.1f;
        public const float SquashFlatEnd = 0.7f;
        public const float SquashPopEnd = 0.85f;
        public const float SquashFlat = 0.35f;
        public const float SquashOvershoot = 1.15f;
        public const float SquashPopHeight = 0.4f;
        public const float SquashShiverDegrees = 6f;

        /// <summary>방패 — 두 팔을 앞으로 막는 자세(작게 뒤로 버팀).</summary>
        public const float BlockBraceDegrees = 8f;
        /// <summary>대시 — 앞으로 20° 숙임.</summary>
        public const float DashLeanDegrees = 20f;
        /// <summary>부수기 — 망치를 내리치며 앞으로 숙임. 결과 뒤 10틱.</summary>
        public const int SmashTicks = 10;
        public const float SmashLeanDegrees = 15f;
        /// <summary>기다림 — 바리케이드 앞 제자리 발 구르기(작게).</summary>
        public const float WaitStompHeight = 0.07f;
        public const int WaitStomps = 6;

        /// <summary>결승 몸짓(두 번 뛰며 한 바퀴) 길이. 결승 뒤엔 시뮬 자리가 안 움직이니 자유다.</summary>
        public const float FinishSeconds = 1f;
        public const float HopHeight = 0.8f;

        public static int FinishTicks => CardRushTiming.Ticks(FinishSeconds);

        /// <summary>
        /// 그 틱에 어떤 몸개그가 몇 % 진행됐나 — 상태만 보고 정한다. 기절(꽈당·으악)이 먼저, 그다음 결승·부수기·방패·대시·기다림.
        /// <paramref name="track"/>은 기다림(지금 장애물이 바리케이드인가)에만 쓴다.
        /// </summary>
        public static (CardRushGagKind kind, float t01) Phase(in CardRushRunnerState s, double d, in CardRushTuning t, CardRushTrack track = null)
        {
            //  둘이 겹치면 나중 것 — 마지막으로 맞은 것이 지금 기절이다.
            bool tumble = s.ObstacleEvent == (int)CardRushObstacleEvent.Tumbled && In(d, s.ObstacleEventTick, t.RollStunTicks);
            bool squash = s.ZoneEvent == (int)CardRushZoneEvent.Hit && In(d, s.ZoneEventTick, t.ZoneStunTicks);
            if (tumble && (!squash || s.ObstacleEventTick >= s.ZoneEventTick))
                return (CardRushGagKind.Tumble, Of(d, s.ObstacleEventTick, t.RollStunTicks));
            if (squash)
                return (CardRushGagKind.Squash, Of(d, s.ZoneEventTick, t.ZoneStunTicks));

            if (s.FinishTick != 0 && In(d, s.FinishTick, FinishTicks))
                return (CardRushGagKind.Finish, Of(d, s.FinishTick, FinishTicks));

            long swing = SwingTick(s);
            if (In(d, swing, SmashTicks))
                return (CardRushGagKind.Smash, Of(d, swing, SmashTicks));

            if (s.BlockEndTick != 0 && In(d, s.BlockEndTick - CardRushRules.BlockTicks, CardRushRules.BlockTicks))
                return (CardRushGagKind.Block, Of(d, s.BlockEndTick - CardRushRules.BlockTicks, CardRushRules.BlockTicks));

            if (s.DashStartTick != 0 && In(d, s.DashStartTick, CardRushRules.DashTicks))
                return (CardRushGagKind.Dash, Of(d, s.DashStartTick, CardRushRules.DashTicks));

            if (track != null && s.ObstacleTick != 0 && s.ObstacleIndex < track.Obstacles.Count
                && track.Obstacles[s.ObstacleIndex].Kind == CardRushObstacleKind.Barricade && In(d, s.ObstacleTick, t.BarricadeWaitTicks))
                return (CardRushGagKind.Wait, Of(d, s.ObstacleTick, t.BarricadeWaitTicks));

            return (CardRushGagKind.None, 0f);
        }

        /// <summary>마지막으로 망치를 휘두른 틱 — 와장창(장애물 결과)과 헛방(헛방 틱) 중 늦은 것. 0 = 없음.</summary>
        public static long SwingTick(in CardRushRunnerState s)
        {
            long smashed = s.ObstacleEvent == (int)CardRushObstacleEvent.Smashed ? s.ObstacleEventTick : 0;
            return System.Math.Max(smashed, s.WhiffTick);
        }

        private static bool In(double d, long start, int length) => start != 0 && length > 0 && d >= start && d < start + length;

        private static float Of(double d, long start, int length) => (float)((d - start) / length);

        /// <summary>몸개그 자세. 0과 1에서 Rest — 기절·대시가 끝나는 틱에 그림이 시뮬 자리로 돌아온다.</summary>
        public static CardRushPose PoseAt(CardRushGagKind kind, float t01)
        {
            t01 = UnityEngine.Mathf.Clamp01(t01);
            if (t01 <= 0f || t01 >= 1f) return CardRushPose.Rest;
            switch (kind)
            {
                case CardRushGagKind.Tumble:
                {
                    float x;
                    float rise = 0f;
                    float squash = 1f;
                    float roll = -(TumbleLeanDegrees + 360f * TumbleTurns);
                    //  한 바퀴 반 구른 뒤 엉덩방아로 내려앉으며 나머지 반 바퀴를 마저 돌아 뒤로 기운 채 앉는다(−740° = −20°).
                    float sit = -(720f + TumbleSitDegrees);
                    if (t01 < TumbleLeanEnd)
                    {
                        x = -TumbleLeanDegrees * (t01 / TumbleLeanEnd);
                    }
                    else if (t01 < TumbleRollEnd)
                    {
                        float u = (t01 - TumbleLeanEnd) / (TumbleRollEnd - TumbleLeanEnd);
                        x = UnityEngine.Mathf.Lerp(-TumbleLeanDegrees, roll, UnityEngine.Mathf.SmoothStep(0f, 1f, u));
                        rise = TumbleRiseHeight * UnityEngine.Mathf.Sin(u * UnityEngine.Mathf.PI);
                    }
                    else if (t01 < TumbleSitEnd)
                    {
                        float u = (t01 - TumbleRollEnd) / (TumbleSitEnd - TumbleRollEnd);
                        //  앞 30%에 내려앉고 나머지는 앉아 있다(별이 돈다).
                        x = UnityEngine.Mathf.Lerp(roll, sit, UnityEngine.Mathf.SmoothStep(0f, 1f, UnityEngine.Mathf.Clamp01(u / 0.3f)));
                        squash = UnityEngine.Mathf.Lerp(1f, TumbleSitSquash, UnityEngine.Mathf.Clamp01(u / 0.3f));
                    }
                    else
                    {
                        float u = (t01 - TumbleSitEnd) / (1f - TumbleSitEnd);
                        x = UnityEngine.Mathf.Lerp(sit, -720f, UnityEngine.Mathf.SmoothStep(0f, 1f, u));
                        squash = UnityEngine.Mathf.Lerp(TumbleSitSquash, 1f, u);
                    }
                    return AroundCenter(x, new UnityEngine.Vector3(0f, rise, 0f), squash);
                }
                case CardRushGagKind.Squash:
                {
                    float squash;
                    float y = 0f;
                    float shiver = 0f;
                    if (t01 < SquashPressEnd)
                    {
                        squash = UnityEngine.Mathf.Lerp(1f, SquashFlat, t01 / SquashPressEnd);
                    }
                    else if (t01 < SquashFlatEnd)
                    {
                        squash = SquashFlat;
                        shiver = SquashShiverDegrees * UnityEngine.Mathf.Sin((t01 - SquashPressEnd) * 90f);
                    }
                    else if (t01 < SquashPopEnd)
                    {
                        float u = (t01 - SquashFlatEnd) / (SquashPopEnd - SquashFlatEnd);
                        squash = UnityEngine.Mathf.Lerp(SquashFlat, SquashOvershoot, u);
                        y = SquashPopHeight * UnityEngine.Mathf.Sin(u * UnityEngine.Mathf.PI * 0.5f);
                    }
                    else
                    {
                        float u = (t01 - SquashPopEnd) / (1f - SquashPopEnd);
                        squash = UnityEngine.Mathf.Lerp(SquashOvershoot, 1f, u);
                        y = SquashPopHeight * UnityEngine.Mathf.Cos(u * UnityEngine.Mathf.PI * 0.5f) * (1f - u);
                    }
                    return new CardRushPose(new UnityEngine.Vector3(0f, y, 0f), new UnityEngine.Vector3(0f, 0f, shiver), squash);
                }
                case CardRushGagKind.Block:
                    return new CardRushPose(UnityEngine.Vector3.zero, new UnityEngine.Vector3(-BlockBraceDegrees * Plateau(t01, 0f, 0.06f, 0.9f, 1f), 0f, 0f), 1f);
                case CardRushGagKind.Dash:
                    return new CardRushPose(UnityEngine.Vector3.zero, new UnityEngine.Vector3(DashLeanDegrees * Plateau(t01, 0f, 0.15f, 0.7f, 1f), 0f, 0f), 1f);
                case CardRushGagKind.Smash:
                    return new CardRushPose(UnityEngine.Vector3.zero, new UnityEngine.Vector3(SmashLeanDegrees * Bump(t01, 0f, 0.35f, 1f), 0f, 0f), 1f);
                case CardRushGagKind.Wait:
                {
                    float y = WaitStompHeight * UnityEngine.Mathf.Abs(UnityEngine.Mathf.Sin(t01 * WaitStomps * UnityEngine.Mathf.PI));
                    return new CardRushPose(new UnityEngine.Vector3(0f, y, 0f), UnityEngine.Vector3.zero, 1f);
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

        //  앞뒤 기울기 x(도)를 몸 가운데 높이를 축으로 준다 — 모델은 발을 축으로 돌므로, 가운데가 제자리에 있게 오프셋을 더한다.
        //  x 양수 = 머리가 앞으로(Quaternion.Euler(x, 0, 0) * up = (0, cos x, sin x)).
        private static CardRushPose AroundCenter(float xDeg, UnityEngine.Vector3 extra, float squash)
        {
            float r = xDeg * UnityEngine.Mathf.Deg2Rad;
            float h = BodyCenterHeight;
            var keep = new UnityEngine.Vector3(0f, h - h * UnityEngine.Mathf.Cos(r), -h * UnityEngine.Mathf.Sin(r));
            return new CardRushPose(keep + extra, new UnityEngine.Vector3(xDeg, 0f, 0f), squash);
        }

        // a~b에서 0→1, b~c에서 1→0 (그 밖 0)
        private static float Bump(float t, float a, float b, float c)
            => t <= a || t >= c ? 0f : t < b ? (t - a) / (b - a) : 1f - (t - b) / (c - b);

        // a~b에서 0→1, b~c 유지, c~d에서 1→0 (그 밖 0)
        private static float Plateau(float t, float a, float b, float c, float d)
            => t <= a || t >= d ? 0f : t < b ? (t - a) / (b - a) : t <= c ? 1f : 1f - (t - c) / (d - c);
    }
}
