using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 서버가 보낸 넉백 판 상태(클라). 누가 탈락했는지·피해 %·공개된 원 단계는 여기서만 읽는다 —
    /// 클라는 맞음이나 탈락을 스스로 판단하지 않는다.
    /// </summary>
    public class KnockbackClientState
    {
        private readonly Dictionary<string, (long eliminatedTick, int damagePercent)> players =
            new Dictionary<string, (long, int)>();
        private readonly List<ZoneStage> zoneStages = new List<ZoneStage>();

        public int Version { get; private set; }

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

        public bool IsEliminated(string entityId)
            => entityId != null && players.TryGetValue(entityId, out var p) && p.eliminatedTick >= 0;

        public int DamageOf(string entityId)
            => entityId != null && players.TryGetValue(entityId, out var p) ? p.damagePercent : 0;

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
                players[w.EntityId] = (w.EliminatedTick, w.DamagePercent);
            }

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
