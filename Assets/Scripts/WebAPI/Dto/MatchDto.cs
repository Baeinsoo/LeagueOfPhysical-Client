using System;

namespace LOP
{
    [Serializable]
    public class MatchDto
    {
        public string id;
        public int queueId;
        public int targetMmr;
        public string[] playerList;
        public MatchRoundDto[] rounds;
    }
}
