using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>바닥에 예고됐거나 떨어져 있는 보급 상자 하나. Position은 바닥 (x, z).</summary>
    public readonly struct KnockbackCrateInfo
    {
        public readonly int Id;
        public readonly long LandTick;
        public readonly Vector2 Position;

        public KnockbackCrateInfo(int id, long landTick, Vector2 position)
        {
            Id = id;
            LandTick = landTick;
            Position = position;
        }
    }

    /// <summary>
    /// 서버가 보낸 넉백 판 상태(클라). 누가 탈락했는지·피해 %·공개된 원 단계는 여기서만 읽는다 —
    /// 클라는 맞음이나 탈락을 스스로 판단하지 않는다.
    /// </summary>
    public class KnockbackClientState
    {
        private readonly Dictionary<string, (long eliminatedTick, int damagePercent, int team, string koBy, bool koChain)> players =
            new Dictionary<string, (long, int, int, string, bool)>();
        private readonly List<ZoneStage> zoneStages = new List<ZoneStage>();

        public int Version { get; private set; }

        /// <summary>지금 있는 상자. 없으면 null(아직 안 나왔거나 주워졌다).</summary>
        public KnockbackCrateInfo? Crate { get; private set; }

        /// <summary>서버가 공개한 원 단계(순서대로). 비어 있으면 아직 원이 줄기 전이다.</summary>
        public IReadOnlyList<ZoneStage> ZoneStages => zoneStages;

        /// <summary>적용될 때마다 한 번.</summary>
        public event Action Changed;

        /// <summary>이미 공개된 가장 최근 단계의 다음 원. 아직 공개 전이면 false.</summary>
        public bool TryGetNextCircle(long tick, out (Vector2 center, float radius) circle)
        {
            for (int i = zoneStages.Count - 1; i >= 0; i--)
            {
                if (zoneStages[i].revealTick <= tick)
                {
                    circle = (zoneStages[i].toCenter, zoneStages[i].toRadius);
                    return true;
                }
            }
            circle = default;
            return false;
        }

        public int PlayerCount => players.Count;

        /// <summary>중계 자막·피니시 판단용 사람별 기록.</summary>
        public List<KoPlayer> SnapshotKoPlayers()
        {
            var list = new List<KoPlayer>(players.Count);
            foreach (var kv in players)
            {
                list.Add(new KoPlayer(kv.Key, kv.Value.eliminatedTick, kv.Value.koBy, kv.Value.koChain, kv.Value.team));
            }
            return list;
        }

        /// <summary>상태에 실린 사람 중 탈락 틱이 없는 수.</summary>
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

        public int DamageOf(string entityId)
            => entityId != null && players.TryGetValue(entityId, out var p) ? p.damagePercent : 0;

        /// <summary>그 몸의 팀(0 = 개인전·모르는 몸).</summary>
        public int TeamOf(string entityId)
            => entityId != null && players.TryGetValue(entityId, out var p) ? p.team : 0;

        private int myTeam;

        /// <summary>
        /// 내 팀. 한 번 알면 기억한다 — 탈락해 내 몸이 사라진 뒤에도 관전 중 "우리 팀"은 내 팀이다.
        /// 탈락 뒤 재접속하면 내 몸 id가 비므로, 그땐 서버가 알려 준 옛 몸 id로 찾는다. 모르면 0.
        /// </summary>
        public int MyTeamOf(string myEntityId, string formerEntityId = null)
        {
            if (myTeam == 0) myTeam = TeamOf(myEntityId);
            if (myTeam == 0) myTeam = TeamOf(formerEntityId);
            return myTeam;
        }

        /// <summary>그 팀(1·2…)의 살아 있는 수.</summary>
        public int AliveCountOfTeam(int team)
        {
            int n = 0;
            foreach (var p in players.Values)
            {
                if (p.eliminatedTick < 0 && p.team == team) n++;
            }
            return n;
        }

        /// <summary>내 팀이 아닌 팀(팀 번호 0 제외)의 살아 있는 수.</summary>
        public int AliveCountOfOtherTeams(int team)
        {
            int n = 0;
            foreach (var p in players.Values)
            {
                if (p.eliminatedTick < 0 && p.team != 0 && p.team != team) n++;
            }
            return n;
        }

        /// <summary>팀 수(0 = 개인전, 첫 상태를 받기 전에도 0).</summary>
        public int TeamCount { get; private set; }

        /// <summary>판본이 지금보다 낮거나 같으면 버린다(늦게 온 옛 상태가 새 상태를 덮지 않게).</summary>
        public bool Apply(KnockbackStateToC message)
        {
            if (message.Version <= Version)
            {
                return false;
            }
            Version = message.Version;

            players.Clear();
            foreach (var w in message.Players)
            {
                players[w.EntityId] = (w.EliminatedTick, w.DamagePercent, w.Team, w.KoBy, w.KoChain);
            }
            TeamCount = message.TeamCount;
            Crate = message.Crate == null
                ? (KnockbackCrateInfo?)null
                : new KnockbackCrateInfo(message.Crate.Id, message.Crate.LandTick, new Vector2(message.Crate.X, message.Crate.Z));

            zoneStages.Clear();
            foreach (var z in message.ZoneStages)
            {
                zoneStages.Add(new ZoneStage(z.Index, z.RevealTick, z.ShrinkStartTick, z.ShrinkEndTick,
                    new Vector2(z.FromX, z.FromZ), z.FromRadius, new Vector2(z.ToX, z.ToZ), z.ToRadius, z.DamagePerSecond));
            }

            Changed?.Invoke();
            return true;
        }
    }
}
