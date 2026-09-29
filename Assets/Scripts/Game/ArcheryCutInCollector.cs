namespace LOP
{
    /// <summary>한 라운드 동안 컷인 후보를 모은다. 결과 때 꺼내면 비운다 — 판 중간에 들어온 사람은 들어온 뒤의 사건만 본다.</summary>
    public sealed class ArcheryCutInCollector
    {
        private string bullId;
        private int bullRank = int.MaxValue;
        private string robinHoodId;

        public void OnBull(string id, int rank)
        {
            if (rank < bullRank)
            {
                bullId = id;
                bullRank = rank;
            }
        }

        public void OnRobinHood(string id) => robinHoodId = robinHoodId ?? id;

        public ArcheryCutInCandidates Take(string comebackId, string lastPlaceId)
        {
            var c = new ArcheryCutInCandidates { ComebackId = comebackId, RobinHoodId = robinHoodId, BullId = bullId, LastPlaceId = lastPlaceId };
            bullId = null;
            bullRank = int.MaxValue;
            robinHoodId = null;
            return c;
        }
    }
}
