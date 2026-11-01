using System;

namespace LOP
{
    /// <summary>퀘스트 칸 하나(로비 GET /user/:id/quests). 숫자는 화면 단위로 온다 — unit이 "minutes"면 분.</summary>
    [Serializable]
    public class QuestItemDto
    {
        public string id;
        public int slot;
        //  1 쉬움 · 2 보통 · 3 어려움.
        public int tier;
        //  서버가 표 문구로 만든 제목 — 앱 새 빌드 없이 퀘스트를 고친다.
        public string title;
        public string unit;
        public int progress;
        public int target;
        public bool completed;
        public long rewardCoins;
        public long rewardXp;
        public int[] gameModeIds;
    }

    [Serializable]
    public class QuestDailyDto
    {
        public string periodKey;
        //  DateTime으로 받는다 — string이면 HttpJson이 날짜로 읽은 뒤 기기 문화권 모양으로 바꿔 넣는다.
        public DateTime endsAt;
        public int rerollsLeft;
        public QuestItemDto[] quests;
    }

    [Serializable]
    public class QuestWeeklyDto
    {
        public string periodKey;
        public DateTime endsAt;
        public QuestItemDto[] quests;
    }

    /// <summary>이번 판에 오른 퀘스트 칸(결과 화면). 숫자는 화면 단위.</summary>
    [Serializable]
    public class MatchQuestProgressDto
    {
        public string id;
        //  "daily" | "weekly"
        public string period;
        public string title;
        public string unit;
        public int before;
        public int after;
        public int target;
        public bool completed;
        public long rewardCoins;
        public long rewardXp;
    }
}
