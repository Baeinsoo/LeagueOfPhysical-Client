using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 서버가 보낸 피하기 상태(클라). 그림은 여기 패턴을 같은 식에 렌더 틱을 넣어 그리고,
    /// 목숨은 여기서만 읽는다 — 클라는 맞음을 스스로 판단하지 않는다(스펙 §5.4).
    /// </summary>
    public class DodgeClientState
    {
        private readonly List<DodgePattern> patterns = new List<DodgePattern>();
        private readonly Dictionary<string, (int lives, long eliminatedTick, long invulnerableUntilTick)> players =
            new Dictionary<string, (int, long, long)>();
        private readonly List<string> playerIds = new List<string>();

        public IReadOnlyList<DodgePattern> Patterns => patterns;
        public int Version { get; private set; }
        public IReadOnlyList<string> PlayerIds => playerIds;

        public bool Apply(DodgeStateToC message)
        {
            if (message.Version <= Version)
            {
                return false;
            }
            Version = message.Version;

            patterns.Clear();
            foreach (var w in message.Patterns)
            {
                float P(int i) => i < w.P.Count ? w.P[i] : 0f;
                patterns.Add(new DodgePattern(w.Id, (DodgePatternKind)w.Kind, w.StartTick, w.Seed,
                                              P(0), P(1), P(2), P(3), w.WarnTicks));
            }

            players.Clear();
            playerIds.Clear();
            foreach (var p in message.Players)
            {
                players[p.EntityId] = (p.Lives, p.EliminatedTick, p.InvulnerableUntilTick);
                playerIds.Add(p.EntityId);
            }
            return true;
        }

        /// <summary>무적이 끝나는 틱(서버 틱). 모르는 사람이면 -1 — 깜빡이지 않는다.</summary>
        public long InvulnerableUntil(string entityId)
            => entityId != null && players.TryGetValue(entityId, out var v) ? v.invulnerableUntilTick : -1;

        public bool TryGetLife(string entityId, out int lives, out long eliminatedTick)
        {
            if (entityId != null && players.TryGetValue(entityId, out var v))
            {
                lives = v.lives;
                eliminatedTick = v.eliminatedTick;
                return true;
            }
            lives = 0;
            eliminatedTick = -1;
            return false;
        }
    }
}
