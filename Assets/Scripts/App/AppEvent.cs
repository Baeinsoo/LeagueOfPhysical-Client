namespace LOP
{
    public enum AppEvent
    {
        BootCompleted,
        MatchFound,
        MatchEnded,
        //  사람이 판을 떠났다. 목적지는 MatchEnded와 같지만 이름이 사실을 말해야 한다 —
        //  판은 남은 사람들끼리 계속 돌아간다.
        MatchLeft,
    }
}
