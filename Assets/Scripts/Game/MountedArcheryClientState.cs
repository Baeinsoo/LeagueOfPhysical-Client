using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 서버가 보낸 말 타고 활쏘기 판 상태(클라). 누가 탈락했고 누가 처치했는지는 여기서만 읽는다 —
    /// 클라는 맞음을 스스로 판단하지 않는다.
    /// </summary>
    public class MountedArcheryClientState
    {
        public struct Elimination
        {
            public string VictimId;
            public string KillerId;
            public string VictimName;
            public string KillerName;
            public long Tick;
        }

        private readonly Dictionary<string, (string name, long eliminatedTick, string killerId)> players =
            new Dictionary<string, (string, long, string)>();
        private readonly HashSet<string> announced = new HashSet<string>();

        private bool first = true;

        public int Version { get; private set; }

        /// <summary>
        /// 새로 탈락한 사람마다 한 번. <b>처음 받은 상태에 이미 들어 있는 탈락은 알리지 않는다</b> —
        /// 재접속하면 지난 탈락이 한꺼번에 오는데, 킬 피드에 쏟으면 안 된다. 그래도 <see cref="IsEliminated"/>는 본다.
        /// </summary>
        public event Action<Elimination> Eliminated;

        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var p in players.Values)
                {
                    if (p.eliminatedTick < 0) n++;
                }
                return n;
            }
        }

        public bool IsEliminated(string entityId)
            => entityId != null && players.TryGetValue(entityId, out var p) && p.eliminatedTick >= 0;

        public string NameOf(string entityId)
            => entityId != null && players.TryGetValue(entityId, out var p) ? p.name : "";

        public bool Apply(MountedArcheryStateToC message)
        {
            if (message.Version <= Version)
            {
                return false;
            }
            Version = message.Version;

            players.Clear();
            foreach (var w in message.Players)
            {
                players[w.EntityId] = (w.DisplayName, w.EliminatedTick, w.KillerEntityId);
            }

            bool silent = first;
            first = false;
            foreach (var w in message.Players)
            {
                if (w.EliminatedTick < 0 || announced.Add(w.EntityId) == false || silent)
                {
                    continue;
                }
                Eliminated?.Invoke(new Elimination
                {
                    VictimId = w.EntityId,
                    KillerId = w.KillerEntityId,
                    VictimName = w.DisplayName,
                    KillerName = string.IsNullOrEmpty(w.KillerEntityId) ? "" : NameOf(w.KillerEntityId),
                    Tick = w.EliminatedTick,
                });
            }
            return true;
        }
    }
}
