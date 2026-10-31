using System;

namespace LOP
{
    /// <summary>내 퀘스트(로비 GET /user/:id/quests). serverNow는 남은 시간을 기기 시계가 아니라 서버 시각으로 세라고 온다.</summary>
    public class GetQuestsResponse : HttpResponse
    {
        public DateTime serverNow;
        public QuestDailyDto daily;
        public QuestWeeklyDto weekly;
    }
}
