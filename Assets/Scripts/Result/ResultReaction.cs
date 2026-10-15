namespace LOP
{
    public enum ResultReaction { None, Happy, Sad }

    /// <summary>시상대 선수 반응 — 1위는 기뻐하고 나머지(나감 포함)는 아쉬워한다. 무승부면 아무도 반응하지 않는다.</summary>
    public static class ResultReactionRule
    {
        public static ResultReaction For(int placement, bool isDraw)
        {
            if (isDraw) return ResultReaction.None;
            return placement == 1 ? ResultReaction.Happy : ResultReaction.Sad;
        }
    }
}
