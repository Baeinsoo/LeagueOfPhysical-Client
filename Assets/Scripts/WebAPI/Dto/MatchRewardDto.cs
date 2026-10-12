using System;

namespace LOP
{
    /// <summary>이번 판에 적립된 보상(코인·XP). 코인·XP가 0이어도 <see cref="reason"/>에 사유
    /// ("ok"/"left"/"noPlayedSeconds"/"tooShort")가 실려 온다.</summary>
    [Serializable]
    public class MatchRewardDto
    {
        public long coins;
        public long xp;
        public bool firstWin;
        public int levelBefore;
        public int levelAfter;
        public long xpAfter;
        public string reason;
    }
}
