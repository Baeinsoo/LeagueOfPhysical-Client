namespace LOP
{
    /// <summary>
    /// 스턴/무적 값을 화면 상태 하나로 정리한다. 세 보간기(내 새·남의 새·정적 스냅)가 같은 규칙을
    /// 쓰도록 한곳에 둔다 — 흩어 두면 새마다 다르게 보이는 걸 아무도 못 잡는다.
    /// </summary>
    public static class StunVisuals
    {
        /// <summary>남의 새 — 서버 스냅샷이 진실원본이다.</summary>
        public static StunVisual Of(EntitySnap snap)
        {
            if (snap == null)
            {
                return StunVisual.None;
            }
            return Resolve(snap.stunned, snap.invulnerable);
        }

        /// <summary>내 새 — 스냅을 기다리지 않고 예측 결과를 그 자리에서 읽는다.</summary>
        public static StunVisual Of(FlappyStun stun)
        {
            if (stun == null)
            {
                return StunVisual.None;
            }
            return Resolve(stun.StunRemaining > 0f, stun.InvulnRemaining > 0f);
        }

        //  두 구간은 겹치지 않게 굴러가지만(스턴이 끝나는 틱에 무적이 채워진다), 순서를 정해 두면
        //  혹시 겹쳐 들어와도 "멈춰 있다"가 이긴다 — 못 움직이는 게 더 급한 정보다.
        private static StunVisual Resolve(bool stunned, bool invulnerable)
        {
            if (stunned)
            {
                return StunVisual.Stunned;
            }
            return invulnerable ? StunVisual.Invulnerable : StunVisual.None;
        }
    }
}
