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

        public CardRushHeadCardFace(bool visible, bool faceUp, CardColor color, bool dim, float flipT01)
        {
            Visible = visible; FaceUp = faceUp; Color = color; Dim = dim; FlipT01 = flipT01;
        }
    }

    public static class CardRushHeadCard
    {
        /// <summary>
        /// 색이 바뀐 장이 가로로 0.6 → 1 펴지는 틱 수 — 한 장(23틱)의 앞 12%(시안 <c>sq</c>). 틱 단위라 그림 틱이 같으면 늘 같은 모습이다.
        /// </summary>
        public const int FlipTicks = 3;
        public const float FlipStartWidth = 0.6f;

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
            if (CardRushCard.CardAt(s, tick, gameplayStartTick, lane, track, out var color) == false)
            {
                //  쓰고 쉬는 동안 — 뒷면, 흐리게.
                return new CardRushHeadCardFace(true, false, default, true, 1f);
            }
            long spinBase = CardRushCard.SpinBase(s, gameplayStartTick);
            long sinceCard = (tick - spinBase) % CardRushRules.CardDwellTicks;
            float flip = UnityEngine.Mathf.Clamp01((float)sinceCard / FlipTicks);
            return new CardRushHeadCardFace(true, true, color, stunned, flip);
        }

        /// <summary>
        /// 기절이 시작한 틱 — 꽈당(장애물 결과)·으악(구역 결과) 중 늦은 것. 남의 최신 상태엔 아직 안 그려진 기절이 있어
        /// 그 틱부터만 흐리게 한다(리뷰 m2). 결과를 모르면 0(기절 끝 틱만 본다).
        /// </summary>
        public static long StunStart(in CardRushRunnerState s)
        {
            long tumble = s.ObstacleEvent == (int)CardRushObstacleEvent.Tumbled ? s.ObstacleEventTick : 0;
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
            return Compute(s, drawnTick, gameplayStartTick, lane, track);
        }
    }
}
