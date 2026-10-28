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

        /// <summary>
        /// 판 보상 말고 따로 들어온 코인 줄(장부 사유가 따로다) — 지금은 연속 날갯짓 고비 코인(<c>"combo"</c>)뿐.
        /// 없으면 null(옛 서버·콤보 0). <see cref="coins"/>에는 안 들어 있다 — 합계는 화면이 낸다.
        /// </summary>
        public MatchRewardExtraDto[] extras;
    }

    [Serializable]
    public class MatchRewardExtraDto
    {
        public string kind;
        public long coins;
    }
}
