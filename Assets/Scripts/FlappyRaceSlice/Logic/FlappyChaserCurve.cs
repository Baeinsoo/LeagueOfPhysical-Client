namespace FlappyRace
{
    /// <summary>
    /// 추격자 속도 곡선. 초기속도에서 선형으로 빨라지다 상한에서 평평해진다.
    /// 상한은 플레이어 전진속도보다 낮아야 한다 — 완벽하게 난 사람은 끝까지 잡히지 않는 것이 원칙.
    /// </summary>
    public sealed class FlappyChaserCurve
    {
        public float InitialSpeed = 7f;
        public float Acceleration = 0.075f;
        public float MaxSpeed = 10f;

        public float SpeedAt(float elapsed)
        {
            if (elapsed <= 0f) return InitialSpeed;
            float s = InitialSpeed + Acceleration * elapsed;
            return s < MaxSpeed ? s : MaxSpeed;
        }

        /// <summary>상한에 도달하는 시각. 이 뒤로는 실수 여유가 더 늘지 않는다 = 클라이맥스 시작.</summary>
        public float PressureOnsetTime()
        {
            float gap = MaxSpeed - InitialSpeed;
            if (gap <= 0f) return 0f;
            if (Acceleration <= 0f) return float.PositiveInfinity;
            return gap / Acceleration;
        }
    }
}
