using System;
using System.Collections.Generic;

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

        //  모드별 결과 지표. 이 필드가 생기기 전 확정된 옛 판은 서버가 빈 사전으로 채워 보낸다.
        public Dictionary<string, int> stats;
    }
}
