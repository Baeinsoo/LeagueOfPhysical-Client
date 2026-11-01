namespace LOP
{
    /// <summary>일일 칸 바꾸기 응답 — 바뀐 일일 묶음 전체.</summary>
    public class RerollQuestResponse : HttpResponse
    {
        public QuestDailyDto daily;
    }
}
