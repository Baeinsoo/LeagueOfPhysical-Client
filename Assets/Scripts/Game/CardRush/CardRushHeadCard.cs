namespace LOP
{
    /// <summary>머리 위 카드 한 장의 모습(spec 2026-11-05 §11.4). 뷰는 이것을 그리기만 한다.</summary>
    public readonly struct CardRushHeadCardFace
    {
        public readonly bool Visible;       // 거짓이면 숨김 — 완주 뒤
        public readonly bool FaceUp;        // 참이면 앞면(지금 쓸 수 있는 카드의 색). 거짓이면 뒷면(출발 전·쿨다운)
        public readonly CardColor Color;
        public readonly bool Dim;           // 흐리게 — 기절 중(돌지만 못 쓴다)·쿨다운 중
        public readonly float FlipT01;      // 이 장이 뜬 뒤 가로로 펴지는 몫(0 → 1). 다 펴졌으면 1
        public readonly bool Held;          // 잡은 카드 — 멈추고 커지며 5초 링이 줄어든다
        public readonly float RingT01;      // 잡은 카드의 남은 시간 몫(1 → 0). 안 잡았으면 0
        public readonly bool RingBlink;     // 링 마지막 1초 중 꺼지는 깜빡 구간
        public readonly float Vanish01;     // 사라지는 중이면 진행(0 초과 ~ 1 미만). 아니면 0

        public CardRushHeadCardFace(bool visible, bool faceUp, CardColor color, bool dim, float flipT01,
                                    bool held = false, float ringT01 = 0f, bool ringBlink = false, float vanish01 = 0f)
        {
            Visible = visible; FaceUp = faceUp; Color = color; Dim = dim; FlipT01 = flipT01;
            Held = held; RingT01 = ringT01; RingBlink = ringBlink; Vanish01 = vanish01;
        }
    }

    public static class CardRushHeadCard
    {
        /// <summary>
        /// 색이 바뀐 장이 가로로 0.6 → 1 펴지는 틱 수 — 한 장(23틱)의 앞 12%(시안 <c>sq</c>). 틱 단위라 그림 틱이 같으면 늘 같은 모습이다.
        /// </summary>
        public const int FlipTicks = 3;
        public const float FlipStartWidth = 0.6f;
        /// <summary>사라짐 0.3초(50틱/초 → 15틱).</summary>
        public const int VanishTicks = 15;
        /// <summary>링 마지막 1초 — 이 안에서 5틱마다 깜빡인다.</summary>
        public const int BlinkTicks = 50;
        public const int BlinkPeriodTicks = 5;

        /// <summary>
        /// 내 카드 — 반드시 <c>PlayerInputManager.NextInputTick</c>으로 부른다. 그 틱의 카드가 지금 누르면 쓰는 카드다
        /// (그림 틱으로 칠하면 색 경계에서 "보고 누른 색 ≠ 쓴 색"이 된다).
        /// </summary>
        public static CardRushHeadCardFace Compute(in CardRushRunnerState s, long tick, long gameplayStartTick, int lane, CardRushTrack track)
        {
            if (track == null) return default;
            if (s.FinishTick != 0 && tick >= s.FinishTick) return default;
            //  출발 전엔 뒷면으로 띄워 무엇을 누를지 자리를 보여 준다.
            if (gameplayStartTick == long.MaxValue || tick < gameplayStartTick) return new CardRushHeadCardFace(true, false, default, false, 1f);
            bool stunned = tick < s.StunEndTick && tick >= StunStart(s);
            //  잡은 카드가 사라지는 중 — 몸에서는 이미 풀렸더라도(ExpireTick) 이 틱엔 작아지며 사라진다.
            long vanishFrom = 0;
            if (s.LockTick != 0 && CardRushCard.LockExpired(s, tick)) vanishFrom = s.LockTick + CardRushRules.LockHoldTicks;
            else if (s.LockTick == 0 && s.ExpireTick != 0 && s.SpinStartTick == s.ExpireTick + CardRushRules.CooldownTicks) vanishFrom = s.ExpireTick;
            if (vanishFrom != 0 && tick >= vanishFrom && tick - vanishFrom < VanishTicks)
            {
                return new CardRushHeadCardFace(true, false, default, true, 1f, vanish01: UnityEngine.Mathf.Max(0.001f, (float)(tick - vanishFrom) / VanishTicks));
            }
            if (CardRushCard.CardAt(s, tick, gameplayStartTick, lane, track, out var color) == false)
            {
                //  쓰고 쉬는 동안 — 뒷면, 흐리게.
                return new CardRushHeadCardFace(true, false, default, true, 1f);
            }
            long spinBase = CardRushCard.SpinBase(s, gameplayStartTick);
            long sinceCard = (tick - spinBase) % CardRushRules.CardDwellTicks;
            float flip = UnityEngine.Mathf.Clamp01((float)sinceCard / FlipTicks);
            if (s.LockTick != 0)
            {
                //  잡았다 — 멈춘 카드. 공중에선 노랑·파랑을 못 쓰니 흐리게(탭이 버려진다는 신호).
                long left = s.LockTick + CardRushRules.LockHoldTicks - tick;
                bool airDead = s.JumpStartTick != 0 && color != CardColor.Red;
                bool blink = left <= BlinkTicks && (tick / BlinkPeriodTicks) % 2 == 0;
                float ring = UnityEngine.Mathf.Clamp01((float)left / CardRushRules.LockHoldTicks);
                return new CardRushHeadCardFace(true, true, color, stunned || airDead, 1f, true, ring, blink);
            }
            return new CardRushHeadCardFace(true, true, color, stunned, flip);
        }

        /// <summary>
        /// 기절이 시작한 틱 — 꽈당·풍덩(장애물 결과)·으악(구역 결과) 중 늦은 것. 남의 최신 상태엔 아직 안 그려진 기절이 있어
        /// 그 틱부터만 흐리게 한다(리뷰 m2). 결과를 모르면 0(기절 끝 틱만 본다).
        /// </summary>
        public static long StunStart(in CardRushRunnerState s)
        {
            long tumble = s.ObstacleEvent == (int)CardRushObstacleEvent.Tumbled || s.ObstacleEvent == (int)CardRushObstacleEvent.Fell ? s.ObstacleEventTick : 0;
            long hit = s.ZoneEvent == (int)CardRushZoneEvent.Hit ? s.ZoneEventTick : 0;
            return System.Math.Max(tumble, hit);
        }

        /// <summary>
        /// 남의 카드(그림 틱). 남의 상태는 최신 스냅이라 그려지는 몸보다 앞선다 — 그림 틱이 마지막 사용 틱보다 이르면 그 전 카드는
        /// 상태에 없으므로 뒷면으로 둔다(사라지지 않는다).
        /// </summary>
        public static CardRushHeadCardFace ComputeOther(in CardRushRunnerState s, long drawnTick, long gameplayStartTick, int lane, CardRushTrack track)
        {
            if (track == null) return default;
            if (s.SpinStartTick != 0 && drawnTick < s.SpinStartTick - CardRushRules.CooldownTicks)
            {
                return new CardRushHeadCardFace(true, false, default, false, 1f);
            }
            //  남의 상태는 최신 스냅이라 그려지는 틱엔 아직 안 잡은 카드가 이미 잡혀 있을 수 있다 — 잡기 전엔 도는 카드로 그린다.
            var shown = s;
            if (shown.LockTick != 0 && drawnTick < shown.LockTick) shown.LockTick = 0;
            return Compute(shown, drawnTick, gameplayStartTick, lane, track);
        }
    }
}
