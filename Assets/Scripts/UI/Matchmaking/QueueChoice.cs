namespace LOP.UI
{
    /// <summary>로비에서 고르는 큐 종류. id는 마스터데이터(TbQueue.Code)로 찾는다.</summary>
    public enum QueueKind
    {
        Casual,
        Ranked,
    }

    public static class QueueChoice
    {
        /// <summary>TbQueue에서 그 종류의 id. 없으면 -1.</summary>
        public static int QueueId(QueueKind kind, LOP.MasterData.TbQueue queues)
        {
            string code = kind == QueueKind.Ranked ? "Ranked" : "Casual";
            foreach (var q in queues.DataList)
            {
                if (q.Code == code) return q.Id;
            }
            return -1;
        }

        /// <summary>요청에 실을 값. 랭크는 게임·맵을 0으로 — 서버가 모은 뒤 무작위로 고른다.</summary>
        public static (int queueId, int gameModeId, int mapId) Request(QueueKind kind, int gameModeId, int mapId, LOP.MasterData.TbQueue queues)
        {
            int queueId = QueueId(kind, queues);
            return kind == QueueKind.Ranked ? (queueId, 0, 0) : (queueId, gameModeId, mapId);
        }

        /// <summary>화면에 보일 큐 이름. 마스터데이터 한 곳에서 읽는다 — 로비·대기·프로필·전적이 같은 말을 쓰게.</summary>
        public static string Name(QueueKind kind, LOP.MasterData.TbQueue queues)
        {
            var row = queues.GetOrDefault(QueueId(kind, queues));
            return row?.Name ?? string.Empty;
        }
    }
}
