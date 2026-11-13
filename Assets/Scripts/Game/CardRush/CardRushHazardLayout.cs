namespace LOP
{
    public enum CardRushObstaclePhase { Standing, Lifting, Rolling, Smashed, Lifted, Jumped, Tumbled, Pit, PitFell, Gone }

    public enum CardRushShotPhase { None, Flying, Fizzle }

    /// <summary>날아가는 빨간 카드 투사체의 그림 틱 모습. 꺼짐(Fizzle) 땐 AlongMm = 사거리 끝, T01 = 연기·떨어짐 진행.</summary>
    public readonly struct CardRushShotLook
    {
        public readonly CardRushShotPhase Phase;
        public readonly float AlongMm;
        public readonly float T01;

        public CardRushShotLook(CardRushShotPhase phase, float alongMm, float t01)
        {
            Phase = phase; AlongMm = alongMm; T01 = t01;
        }

        public static readonly CardRushShotLook None = new CardRushShotLook(CardRushShotPhase.None, 0f, 0f);
    }

    /// <summary>구덩이 그림 자리(레인 축 mm) — 어두운 구멍과 그 앞 경고 줄무늬.</summary>
    public readonly struct CardRushPitRect
    {
        public readonly int HoleFromMm;
        public readonly int HoleToMm;
        public readonly int StripeFromMm;
        public readonly int StripeToMm;

        public CardRushPitRect(int holeFromMm, int holeToMm, int stripeFromMm, int stripeToMm)
        {
            HoleFromMm = holeFromMm; HoleToMm = holeToMm; StripeFromMm = stripeFromMm; StripeToMm = stripeToMm;
        }
    }

    public enum CardRushZonePhase { Hidden, Warning, Impact, Gone }

    /// <summary>장애물 하나의 그림 틱 모습. AlongMm = 레인 출발선에서 앞으로 간 거리(그 레인 몸과 같은 축).</summary>
    public readonly struct CardRushObstacleLook
    {
        public readonly CardRushObstaclePhase Phase;
        public readonly float T01;          // 올라감 = 기다린 몫, 결과(와장창·올라감·막음·꽈당) = 그 그림의 진행
        public readonly float AlongMm;
        public readonly int Variant;
        public readonly float SecondsLeft;  // 올라감 중 남은 초. 그 밖 0

        public CardRushObstacleLook(CardRushObstaclePhase phase, float t01, float alongMm, int variant, float secondsLeft)
        {
            Phase = phase; T01 = t01; AlongMm = alongMm; Variant = variant; SecondsLeft = secondsLeft;
        }
    }

    /// <summary>구역 하나의 그림 틱 모습.</summary>
    public readonly struct CardRushZoneLook
    {
        public readonly CardRushZonePhase Phase;
        public readonly int FromMm;
        public readonly int ToMm;
        public readonly float SecondsLeft;  // 경고 중 터지기까지 남은 초
        public readonly float Warn01;       // 경고 진행(0 켜짐 → 1 터짐)
        public readonly float RockHeight;   // 떨어지는 바위의 바닥 위 높이(m). 음수 = 아직 없음
        public readonly float T01;          // 터진 뒤 먼지 진행(0 → 1)

        public CardRushZoneLook(CardRushZonePhase phase, int fromMm, int toMm, float secondsLeft, float warn01, float rockHeight, float t01)
        {
            Phase = phase; FromMm = fromMm; ToMm = toMm; SecondsLeft = secondsLeft; Warn01 = warn01; RockHeight = rockHeight; T01 = t01;
        }
    }

    /// <summary>
    /// 장애물·구역 그림의 순수 식(spec §11.2) — (상태, 코스, 그림 틱)에서 모습만 낸다. 뷰와 미리보기는 이것을 그리기만 한다.
    /// <para>남의 상태는 최신 스냅이라 그려지는 몸보다 앞선다. 그래서 마지막으로 치운 장애물(번호 − 1)의 결과 틱이 그림 틱보다 뒤면
    /// 그 장애물은 그림에선 아직 살아 있다 — 바리케이드는 제자리, 굴러오는 것은 "결과 틱에 접촉 거리였다"에서 거꾸로 잡은 자리.
    /// 구역도 같다. 그래서 남의 와장창이 남의 몸 그림과 같은 순간에 보인다.</para>
    /// </summary>
    public static class CardRushHazardLayout
    {
        public const float TickSeconds = CardRushTiming.TickInterval;

        /// <summary>
        /// 와장창·막음·꽈당 그림 길이(0.35초 = 17.5틱)와 다 올라간 바리케이드가 사라지는 길이(0.3초 = 15틱). 틱으로 박는다 —
        /// 0.3 / 0.02를 부동소수로 나누면 14.999…가 나와 끝 틱에 한 번 더 그려졌다.
        /// </summary>
        public const float ResultTicks = 17.5f;
        public const float LiftedTicks = 15f;

        /// <summary>구덩이 앞 경고 줄무늬 길이(spec §9: 앞 1 m).</summary>
        public const int PitStripeMm = 1000;

        /// <summary>풍덩 먼지 길이 0.3초(15틱), 투사체가 꺼질 때 연기·떨어짐 0.3초.</summary>
        public const float FellDustTicks = 15f;
        public const float FizzleTicks = 15f;

        /// <summary>사거리 끝까지 나는 틱 수 — 꺼진 틱(WhiffTick)에서 거꾸로 던진 틱을 잡는다(8000 / 300 올림 = 27).</summary>
        public const int WhiffFlightTicks = (CardRushRules.ShotRangeMm + CardRushRules.ShotMmPerTick - 1) / CardRushRules.ShotMmPerTick;

        /// <summary>투사체가 지나는 높이(m).</summary>
        public const float ShotHeight = 1.0f;

        /// <summary>바리케이드가 기다리는 동안 올라가는 높이(m, 시안 <c>lift</c>).</summary>
        public const float BarricadeLiftHeight = 0.5f;

        /// <summary>바위는 터지기 이 틱 전에 이 높이(m)에서 떨어지기 시작해 터지는 틱에 바닥에 닿는다(가속 낙하).</summary>
        public const int RockFallTicks = 15;
        public const float RockDropHeight = 6f;

        /// <summary>터진 뒤 먼지가 퍼지는 시간 0.5초(바위도 이때까지 남는다).</summary>
        public const float ImpactTicks = 25f;

        /// <summary>
        /// 장애물 <paramref name="index"/>의 그림 틱 <paramref name="d"/> 모습. <paramref name="runnerDrawnMm"/>은 그 레인 몸이 그려진 자리(레인 축 mm) —
        /// 남의 레인에서 아직 살아 있는 굴러오는 것의 자리를 거꾸로 잡을 때 쓴다.
        /// </summary>
        public static CardRushObstacleLook ObstacleLook(in CardRushRunnerState s, int index, double d, float runnerDrawnMm,
                                                        CardRushTrack track, in CardRushTuning tuning)
        {
            if (track == null || index < 0 || index >= track.Obstacles.Count) return Gone(0);
            var o = track.Obstacles[index];

            if (o.Kind == CardRushObstacleKind.Pit) return PitLook(s, index, d, o);
            if (index > s.ObstacleIndex) return Standing(o);
            if (index == s.ObstacleIndex) return Current(o, s.ObstacleTick, d, tuning);
            if (index < s.ObstacleIndex - 1) return Gone(o.Variant);

            //  바로 앞에 치운 것 — 결과 칸은 늘 이 장애물의 결과다(헛방은 따로 WhiffTick).
            var ev = (CardRushObstacleEvent)s.ObstacleEvent;
            long at = s.ObstacleEventTick;
            if (ev == CardRushObstacleEvent.None) return Gone(o.Variant);
            //  Blocked·Whiff는 은퇴한 값이다 — 4차엔 이 결과가 상태에 안 나온다.
            if (ev != CardRushObstacleEvent.Smashed && ev != CardRushObstacleEvent.Lifted
                && ev != CardRushObstacleEvent.Tumbled && ev != CardRushObstacleEvent.Jumped)
                return Gone(o.Variant);

            if (at > d)
            {
                //  그림에선 아직 살아 있다(남의 레인 — 상태가 그림보다 앞섰다).
                if (o.Kind == CardRushObstacleKind.Barricade)
                {
                    //  기다리다 부순 바리케이드는 닿은 틱을 모른다(치우면 0) — 남의 레인에선 결과 전 ≤5틱 동안 바닥에 서 있게 그린다(리뷰 m1, 받아들임).
                    //  올라감으로 치웠으면 닿은 틱을 안다(결과 틱 − 기다림) — 올라가는 모습까지 그린다.
                    return ev == CardRushObstacleEvent.Lifted
                        ? Current(o, at - tuning.BarricadeWaitTicks, d, tuning)
                        : Standing(o);
                }
                //  투사체에 맞은 굴러오는 것은 맞은 자리에서 만난다 — 접촉 거리가 아니다.
                float along = ev == CardRushObstacleEvent.Smashed
                    ? SmashHit(s, o) + (float)(CardRushRules.RollerMmPerTick * (at - d))
                    : runnerDrawnMm + CardRushRules.RollerContactMm + (float)(CardRushRules.RollerMmPerTick * (at - d));
                return along >= o.AtMm ? Standing(o) : new CardRushObstacleLook(CardRushObstaclePhase.Rolling, 0f, along, o.Variant, 0f);
            }

            double since = d - at;
            switch (ev)
            {
                case CardRushObstacleEvent.Lifted:
                {
                    float t = (float)(since / LiftedTicks);
                    return t >= 1f ? Gone(o.Variant) : new CardRushObstacleLook(CardRushObstaclePhase.Lifted, t, o.AtMm, o.Variant, 0f);
                }
                default:
                {
                    float t = (float)(since / ResultTicks);
                    if (t >= 1f) return Gone(o.Variant);
                    var phase = ev == CardRushObstacleEvent.Smashed ? CardRushObstaclePhase.Smashed
                              : ev == CardRushObstacleEvent.Jumped ? CardRushObstaclePhase.Jumped
                              : CardRushObstaclePhase.Tumbled;
                    return new CardRushObstacleLook(phase, t, ResultAnchorMm(s, o, ev, since, runnerDrawnMm, tuning), o.Variant, 0f);
                }
            }
        }

        //  결과 그림이 시작하는 자리. 굴러오는 것은 치운 순간의 자리를 상태가 안 갖고 있어 어림한다 —
        //  부순 것은 투사체가 맞힌 자리, 넘은 것은 몸이 앞으로 가고 굴러오는 것이 몸 쪽으로 오니 둘이 서로 지나치는 만큼 뒤,
        //  꽈당은 몸이 밀려났으니 밀린 만큼 앞.
        private static float ResultAnchorMm(in CardRushRunnerState s, in CardRushObstacle o, CardRushObstacleEvent ev, double since, float runnerDrawnMm, in CardRushTuning tuning)
        {
            if (o.Kind == CardRushObstacleKind.Barricade) return o.AtMm;
            switch (ev)
            {
                case CardRushObstacleEvent.Smashed:
                    return SmashHit(s, o);
                case CardRushObstacleEvent.Jumped:
                    return runnerDrawnMm - (float)((CardRushRules.JumpMmPerTick + CardRushRules.RollerMmPerTick) * since) + CardRushRules.RollerContactMm;
                case CardRushObstacleEvent.Tumbled:
                    return runnerDrawnMm + tuning.RollKnockMm + CardRushRules.RollerContactMm;
                default:
                    return runnerDrawnMm + CardRushRules.RollerContactMm;
            }
        }

        /// <summary>
        /// 투사체가 장애물을 맞힌 자리(mm). 바리케이드 = 그 자리. 굴러오는 것은 맞은 순간 자리를 상태가 안 갖고 있어
        /// 던진 틱(쓴 뒤 쿨다운 틱 − 20)에서 투사체가 간 거리로 어림한다(굴러오는 것이 38 mm/틱이라 오차 0.34 m 안).
        /// </summary>
        public static float SmashHit(in CardRushRunnerState s, in CardRushObstacle o)
        {
            SmashSpot(s, o, out int hit, out _);
            return hit;
        }

        private static void SmashSpot(in CardRushRunnerState s, in CardRushObstacle o, out int hitMm, out long thrownTick)
        {
            long at = s.ObstacleEventTick;
            long t = s.SpinStartTick - CardRushRules.CooldownTicks;
            bool known = t >= 1 && t <= at && at - t <= WhiffFlightTicks;
            long reached = s.ShotFromMm + (long)CardRushRules.ShotMmPerTick * (at - t);
            if (o.Kind == CardRushObstacleKind.Barricade) hitMm = o.AtMm;
            else if (known) hitMm = (int)System.Math.Min(o.AtMm, reached);
            else hitMm = System.Math.Min(o.AtMm, s.ShotFromMm + CardRushRules.ShotRangeMm / 2);
            if (known)
            {
                thrownTick = t;
            }
            else
            {
                int flight = System.Math.Max(0, hitMm - s.ShotFromMm);
                thrownTick = at - (flight + CardRushRules.ShotMmPerTick - 1) / CardRushRules.ShotMmPerTick;
            }
        }

        /// <summary>구덩이 그림 자리 — 구멍은 [At, At + 폭], 경고 줄무늬는 그 앞 1 m.</summary>
        public static CardRushPitRect PitRect(in CardRushObstacle o)
            => new CardRushPitRect(o.AtMm, o.AtMm + o.WidthMm, o.AtMm - PitStripeMm, o.AtMm);

        //  구덩이는 넘어도 그대로 남는다(spec §9). 빠짐(Fell)은 번호가 안 올라 지금 장애물에 결과가 적힌다 — 그 틱부터 0.3초 먼지.
        private static CardRushObstacleLook PitLook(in CardRushRunnerState s, int index, double d, in CardRushObstacle o)
        {
            if (index < s.ObstacleIndex - 1) return Gone(o.Variant);
            if (index == s.ObstacleIndex && s.ObstacleEvent == (int)CardRushObstacleEvent.Fell)
            {
                long at = s.ObstacleEventTick;
                if (at != 0 && d >= at && d - at < FellDustTicks)
                    return new CardRushObstacleLook(CardRushObstaclePhase.PitFell, (float)((d - at) / FellDustTicks), o.AtMm, o.Variant, 0f);
            }
            return new CardRushObstacleLook(CardRushObstaclePhase.Pit, 0f, o.AtMm, o.Variant, 0f);
        }

        /// <summary>
        /// 투사체의 그림 틱 모습. 날고 있으면 상태 그대로, 이미 맞히거나 꺼진 것은(남의 레인은 상태가 그림보다 앞선다)
        /// 던진 틱을 거꾸로 잡아 그림 틱까지는 날고 있는 것으로 그린다 — 남의 와장창이 투사체가 닿는 순간과 같이 보인다.
        /// </summary>
        public static CardRushShotLook ShotLook(in CardRushRunnerState s, double d, CardRushTrack track)
        {
            if (s.ShotTick != 0 && d >= s.ShotTick)
            {
                float far = s.ShotFromMm + CardRushRules.ShotRangeMm;
                float along = System.Math.Min(far, s.ShotFromMm + CardRushRules.ShotMmPerTick * (float)(d - s.ShotTick));
                return new CardRushShotLook(CardRushShotPhase.Flying, along, 0f);
            }

            if (track != null && s.ObstacleEvent == (int)CardRushObstacleEvent.Smashed && s.ObstacleIndex >= 1
                && s.ObstacleIndex - 1 < track.Obstacles.Count && d < s.ObstacleEventTick)
            {
                var o = track.Obstacles[s.ObstacleIndex - 1];
                SmashSpot(s, o, out int hit, out long thrown);
                if (d >= thrown)
                {
                    float along = System.Math.Min(hit, s.ShotFromMm + CardRushRules.ShotMmPerTick * (float)(d - thrown));
                    return new CardRushShotLook(CardRushShotPhase.Flying, along, 0f);
                }
            }

            if (s.WhiffTick != 0)
            {
                float end = s.ShotFromMm + CardRushRules.ShotRangeMm;
                double thrown = s.WhiffTick - WhiffFlightTicks;
                if (d >= thrown && d < s.WhiffTick)
                    return new CardRushShotLook(CardRushShotPhase.Flying, System.Math.Min(end, s.ShotFromMm + CardRushRules.ShotMmPerTick * (float)(d - thrown)), 0f);
                double since = d - s.WhiffTick;
                if (since >= 0 && since < FizzleTicks)
                    return new CardRushShotLook(CardRushShotPhase.Fizzle, end, (float)(since / FizzleTicks));
            }
            return CardRushShotLook.None;
        }

        private static CardRushObstacleLook Current(in CardRushObstacle o, long obstacleTick, double d, in CardRushTuning tuning)
        {
            if (obstacleTick == 0 || obstacleTick > d) return Standing(o);
            if (o.Kind == CardRushObstacleKind.Barricade)
            {
                int wait = System.Math.Max(1, tuning.BarricadeWaitTicks);
                double since = d - obstacleTick;
                float t = UnityEngine.Mathf.Clamp01((float)(since / wait));
                float left = (float)System.Math.Max(0d, (obstacleTick + wait - d) * TickSeconds);
                return new CardRushObstacleLook(CardRushObstaclePhase.Lifting, t, o.AtMm, o.Variant, left);
            }
            float along = (float)(o.AtMm - CardRushRules.RollerMmPerTick * (d - obstacleTick));
            return new CardRushObstacleLook(CardRushObstaclePhase.Rolling, 0f, along, o.Variant, 0f);
        }

        private static CardRushObstacleLook Standing(in CardRushObstacle o)
            => new CardRushObstacleLook(CardRushObstaclePhase.Standing, 0f, o.AtMm, o.Variant, 0f);

        private static CardRushObstacleLook Gone(int variant)
            => new CardRushObstacleLook(CardRushObstaclePhase.Gone, 1f, 0f, variant, 0f);

        /// <summary>구역 <paramref name="index"/>의 그림 틱 <paramref name="d"/> 모습.</summary>
        public static CardRushZoneLook ZoneLook(in CardRushRunnerState s, int index, double d, CardRushTrack track)
        {
            if (track == null || index < 0 || index >= track.Zones.Count) return default;
            var z = track.Zones[index];

            if (index > s.ZoneIndex) return Hidden(z);
            //  경고 중에 완주하면 시뮬은 멈춰 이 구역이 영영 안 터진다 — 결승 틱부터는 걷는다("0.0초"·바위·화난 술래가 남지 않게, 최종 리뷰 m1).
            if (index == s.ZoneIndex) return s.FinishTick != 0 && d >= s.FinishTick ? Hidden(z) : Warning(z, s.ZoneTick, d);
            if (index < s.ZoneIndex - 1) return new CardRushZoneLook(CardRushZonePhase.Gone, z.FromMm, z.ToMm, 0f, 1f, -1f, 1f);

            long impact = s.ZoneEventTick;
            if (impact > d)
            {
                //  그림에선 아직 경고 중(남의 레인). 켜진 틱 = 터진 틱 − 경고.
                return Warning(z, impact - z.WarnTicks, d);
            }
            float t = (float)((d - impact) / ImpactTicks);
            return t >= 1f
                ? new CardRushZoneLook(CardRushZonePhase.Gone, z.FromMm, z.ToMm, 0f, 1f, -1f, 1f)
                : new CardRushZoneLook(CardRushZonePhase.Impact, z.FromMm, z.ToMm, 0f, 1f, 0f, t);
        }

        private static CardRushZoneLook Warning(in CardRushZone z, long zoneTick, double d)
        {
            if (zoneTick == 0 || zoneTick > d) return Hidden(z);
            long impact = zoneTick + z.WarnTicks;
            double left = impact - d;
            float warn01 = UnityEngine.Mathf.Clamp01((float)((d - zoneTick) / System.Math.Max(1, z.WarnTicks)));
            return new CardRushZoneLook(CardRushZonePhase.Warning, z.FromMm, z.ToMm,
                (float)System.Math.Max(0d, left * TickSeconds), warn01, RockHeightAt(left), 0f);
        }

        /// <summary>터지기까지 남은 틱 → 바위 높이(m). 마지막 15틱 동안 6 m에서 가속해 떨어져 터지는 틱에 0. 그 전엔 −1(없음).</summary>
        public static float RockHeightAt(double ticksLeft)
        {
            if (ticksLeft > RockFallTicks) return -1f;
            if (ticksLeft <= 0) return 0f;
            double u = 1d - ticksLeft / RockFallTicks;   //  떨어진 몫(시간)
            return (float)(RockDropHeight * (1d - u * u));
        }

        private static CardRushZoneLook Hidden(in CardRushZone z)
            => new CardRushZoneLook(CardRushZonePhase.Hidden, z.FromMm, z.ToMm, 0f, 0f, -1f, 0f);
    }
}
