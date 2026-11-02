using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>화면에 띄울 장외 한 건. 이름은 몸이 사라지기 전에 기억해 둔 것이다.</summary>
    public readonly struct KoAnnouncement
    {
        public readonly string KillerName;   // 자멸이면 null
        public readonly Color? KillerColor;
        public readonly string VictimName;
        public readonly Color? VictimColor;
        public readonly bool Chain;
        public readonly int Streak;

        public KoAnnouncement(string killerName, Color? killerColor, string victimName, Color? victimColor, bool chain, int streak)
        {
            KillerName = killerName;
            KillerColor = killerColor;
            VictimName = victimName;
            VictimColor = victimColor;
            Chain = chain;
            Streak = streak;
        }
    }

    /// <summary>
    /// 서버 상태에서 새 탈락을 골라 중계 자막(<see cref="Ko"/>)과 피니시 신호(<see cref="Finished"/>)로 내보낸다.
    /// 탈락한 몸은 곧 치워지므로, 이름과 "원판을 벗어난 마지막 자리"는 살아 있는 동안 매 틱 기억해 둔다.
    /// </summary>
    public class KnockbackKoBroadcaster : ITickable, IDisposable
    {
        //  KnockbackSpectate와 같은 기준 — 이보다 아래면 떨어지는 중이다.
        private const float FallingY = -0.05f;

        private readonly KnockbackClientState state;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly KnockbackKoFeed feed = new KnockbackKoFeed();
        private readonly Dictionary<string, string> names = new Dictionary<string, string>();
        private readonly Dictionary<string, Vector3> lastOnFloor = new Dictionary<string, Vector3>();

        public event Action<KoAnnouncement> Ko;

        /// <summary>판이 끝나는 탈락이 났다. 인자는 마지막으로 탈락한 몸이 원판을 벗어난 자리.</summary>
        public event Action<Vector3> Finished;

        public KnockbackKoBroadcaster(KnockbackClientState state, GameFramework.World.EntityRegistry entityRegistry,
                                      IGameDataStore gameDataStore)
        {
            this.state = state;
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
            state.Changed += OnStateChanged;
        }

        public void Dispose() => state.Changed -= OnStateChanged;

        public void Tick() => Remember();

        private void Remember()
        {
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character) continue;

                string name = entity.Get<PlayerLook>()?.DisplayName;
                if (!string.IsNullOrEmpty(name)) names[entity.Id] = name;

                var t = entity.Get<GameFramework.World.Transform>();
                if (t != null && t.Position.Y >= FallingY)
                {
                    lastOnFloor[entity.Id] = new Vector3(t.Position.X, t.Position.Y, t.Position.Z);
                }
            }
        }

        private string NameOf(string id) => id != null && names.TryGetValue(id, out var n) ? n : "선수";

        private void OnStateChanged()
        {
            Remember();
            var players = state.SnapshotKoPlayers();
            var events = feed.Observe(players);
            if (events.Count == 0) return;

            int myTeam = state.TeamCount >= 2
                ? state.MyTeamOf(gameDataStore?.userEntityId, gameDataStore?.formerEntityId) : 0;
            Color? ColorOf(string id) => state.TeamCount >= 2 ? KnockbackTeamRingView.RingColor(myTeam, state.TeamOf(id)) : null;

            foreach (var e in events)
            {
                Ko?.Invoke(new KoAnnouncement(
                    e.KillerId == null ? null : NameOf(e.KillerId), e.KillerId == null ? null : ColorOf(e.KillerId),
                    NameOf(e.VictimId), ColorOf(e.VictimId), e.Chain, e.Streak));
            }

            if (KnockbackKoFeed.IsFinish(players, state.TeamCount))
            {
                string last = events[events.Count - 1].VictimId;
                Finished?.Invoke(lastOnFloor.TryGetValue(last, out var pos) ? pos : Vector3.zero);
            }
        }
    }
}
