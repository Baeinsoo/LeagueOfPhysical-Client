namespace LOP
{
    /// <summary>함성 크기 — 랭크 세기 × (기본 0.22 + 전환 직후 덧붙는 0.5). 상한 0.6. 음량 설정이 생기기 전까지 작게 둔다.</summary>
    public static class CrowdMix
    {
        public const float Base = 0.22f;
        public const float SwellGain = 0.5f;
        public const float Max = 0.6f;
        public const float SwellDecayPerSecond = 0.35f;
        public const float FollowSeconds = 0.25f;

        public static float Target(float weight, float swell) =>
            System.Math.Min(Max, weight * (Base + SwellGain * System.Math.Max(0f, swell)));
    }
}
