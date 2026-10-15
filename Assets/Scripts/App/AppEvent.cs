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
        //  결과 단계에서 확인을 눌렀다(또는 보여 줄 결과가 없었다) — 로비로.
        ResultClosed,
    }
}
