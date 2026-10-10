using System;

namespace LOP
{
    /// <summary>이번 판에 적립된 보상(코인·XP). 보상이 없던 사람(나감·너무 짧음·동결·지급 꺼짐)은
    /// 이 필드 자체가 안 온다.</summary>
    [Serializable]
    public class MatchRewardDto
    {
        public long coins;
        public long xp;
        public bool firstWin;
        public int levelBefore;
        public int levelAfter;
        public long xpAfter;
    }
}
