using System;

namespace LOP
{
    /// <summary>레벨·경험치 진행도(로비 GET economy).</summary>
    [Serializable]
    public class ProgressDto
    {
        public int level;
        //  누적 경험치
        public long xp;
        //  이번 레벨에서 쌓은 경험치
        public long xpIntoLevel;
        //  이번 레벨을 채우는 데 필요한 경험치
        public long xpToNext;
    }
}
