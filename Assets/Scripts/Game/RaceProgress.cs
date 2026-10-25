namespace LOP
{
    /// <summary>진행 막대 비율. 출발 x → 결승선 앞면 x(<c>FinishLineBounds</c>의 <c>min.x</c>) 사이 어디쯤인가.</summary>
    public static class RaceProgress
    {
        public static float Ratio(float x, float startX, float finishX)
        {
            float span = finishX - startX;
            //  결승선을 아직 모르거나 출발보다 뒤에 있으면 막대가 거짓말을 하느니 0에 둔다.
            if (span <= 0f || float.IsNaN(x))
            {
                return 0f;
            }
            float ratio = (x - startX) / span;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }
    }
}
