namespace LOP
{
    public enum ResultReaction { None, Happy, Sad }

    /// <summary>
    /// 시상대 선수 반응 — 기뻐하는 건 1위뿐. 시상대 몫(1~3위, 단 꼴찌는 빼고 = min(3, 인원−1)) 안의 2·3위는 덤덤,
    /// 그 뒤와 나간 선수는 아쉬워한다. 2명이면 2위가, 3명이면 3위가 아쉬워한다. 전원 무승부면 아무도 반응하지 않는다.
    /// </summary>
    public static class ResultReactionRule
    {
        public static ResultReaction For(int placement, int playerCount, bool isLeft, bool isDraw)
        {
            if (isDraw) return ResultReaction.None;
            if (isLeft) return ResultReaction.Sad;
            if (placement == 1) return ResultReaction.Happy;
            int podium = System.Math.Min(3, playerCount - 1);
            return placement <= podium ? ResultReaction.None : ResultReaction.Sad;
        }
    }
}
