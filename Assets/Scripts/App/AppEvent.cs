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
        //  경기 중 끊겼다가 같은 판에 다시 들어간다 — 경기 씬을 새로 띄운다.
        MatchRejoin,
    }
}
