using System;

namespace LOP
{
    public class GetMatchHistoryResponse : HttpResponse
    {
        public MatchHistoryEntryDto[] matches;
    }

    [Serializable]
    public class MatchHistoryEntryDto
    {
        public string matchId;
        public int queueId;
        public string endedAt;
        public MatchHistoryRoundDto[] rounds;
        public MatchHistoryParticipantDto[] participants;
    }

    [Serializable]
    public class MatchHistoryRoundDto
    {
        public int index;
        public int gameModeId;
        public int mapId;
    }

    [Serializable]
    public class MatchHistoryParticipantDto
    {
        public string userId;
        //  확정 시점에 박힌 이름이다. 계정 이름이 바뀌어도 과거 전적은 그대로다.
        public string displayName;
        public int placement;
        public int mmrBefore;
        public int mmrAfter;
    }
}
