namespace FlappyRace
{
    /// <summary>
    /// 새 두 마리가 부딪혔을 때 세로 속도를 주고받는 계산(질량은 서로 같다고 본다).
    ///
    /// 부딪힌 속도를 0으로 지우면 위에 있는 새가 아래 새를 발판처럼 밟고 서게 되고,
    /// 중력이 곧바로 다시 붙여서 매 프레임 재충돌한다. 서로 속도를 주고받아야 갈라진다.
    ///
    /// 전진 속도는 상수로 고정돼 손댈 수 없으므로 세로 성분만 오간다.
    /// </summary>
    public static class FlappyBounce
    {
        /// <summary>이보다 느리게 다가온 충돌은 튕기지 않는다 — 얹혀 있을 때 미세하게 떠는 걸 막는다.</summary>
        public const float RestingSpeed = 1.5f;

        /// <summary>
        /// 충돌 후 self의 세로 속도.
        /// <paramref name="normalY"/>는 self를 상대 밖으로 밀어내는 방향의 세로 성분(-1~1)이다.
        /// 옆에서 스치면 0에 가까워져 세로 속도가 거의 안 바뀐다.
        /// </summary>
        public static float ResolveVy(float vySelf, float vyOther, float normalY, float restitution)
        {
            float closing = (vySelf - vyOther) * normalY;
            if (closing >= 0f) return vySelf;   // 이미 멀어지는 중이면 건드리지 않는다

            float e = -closing < RestingSpeed ? 0f : restitution;
            return vySelf - (1f + e) * closing * 0.5f * normalY;
        }
    }
}
