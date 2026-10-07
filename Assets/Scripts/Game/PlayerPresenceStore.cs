using System;
using System.Collections.Generic;
using R3;

namespace LOP
{
    /// <summary>판 도중 누가 끊겼다/돌아왔다 — 화면이 쓸 한 건.</summary>
    public readonly struct PresenceChange : IEquatable<PresenceChange>
    {
        public readonly string EntityId;
        /// <summary>명단 순번(1부터) — "2P 선수".</summary>
        public readonly int Slot;
        public readonly bool Away;

        public PresenceChange(string entityId, int slot, bool away)
        {
            EntityId = entityId;
            Slot = slot;
            Away = away;
        }

        public bool Equals(PresenceChange other) => EntityId == other.EntityId && Slot == other.Slot && Away == other.Away;
        public override bool Equals(object obj) => obj is PresenceChange other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(EntityId, Slot, Away);
        public override string ToString() => $"{EntityId}#{Slot} away={Away}";
    }

    public static class PresenceText
    {
        public static string For(PresenceChange change) => $"{change.Slot}P 선수 {(change.Away ? "연결 끊김" : "재접속")}";
    }

    /// <summary>
    /// 서버가 보낸 "판 도중 끊긴 사람" 목록(클라). 목록 전체가 오므로 앞 목록과 견줘 바뀐 사람만 알린다 —
    /// 다시 들어온 사람이 같은 목록을 한 번 더 받아도 알림이 겹치지 않는다. 늦게 온 옛 판본은 버린다.
    /// </summary>
    public class PlayerPresenceStore : IDisposable
    {
        private readonly Dictionary<string, int> away = new Dictionary<string, int>();
        private readonly Subject<PresenceChange> changes = new Subject<PresenceChange>();
        private int version = -1;

        public Observable<PresenceChange> Changes => changes;

        public bool IsAway(string entityId) => entityId != null && away.ContainsKey(entityId);

        public void Apply(PlayerPresenceToC message)
        {
            if (message.Version <= version) return;
            version = message.Version;

            var next = new Dictionary<string, int>();
            foreach (var w in message.Away) next[w.EntityId] = w.Slot;

            var events = new List<PresenceChange>();
            foreach (var (entityId, slot) in away)
            {
                if (next.ContainsKey(entityId) == false) events.Add(new PresenceChange(entityId, slot, false));
            }
            foreach (var (entityId, slot) in next)
            {
                if (away.ContainsKey(entityId) == false) events.Add(new PresenceChange(entityId, slot, true));
            }

            away.Clear();
            foreach (var (entityId, slot) in next) away[entityId] = slot;
            foreach (var e in events) changes.OnNext(e);
        }

        public void Dispose() => changes.Dispose();
    }
}
