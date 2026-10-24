using System;
using System.Collections.Generic;
using UnityEngine;

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

        private readonly List<ZoneStage> zoneStages = new List<ZoneStage>();

        /// <summary>서버가 공개한 원 단계(순서대로). 비어 있으면 아직 줍기 시간이다.</summary>
        public IReadOnlyList<ZoneStage> ZoneStages => zoneStages;

        /// <summary>화면에 그리는 시각(소수 틱). 원 그림과 문구가 같은 시각을 보게 한다.</summary>
        public double RenderTick { get; private set; }
        public float TickInterval { get; private set; } = 0.02f;

        /// <summary>첫 원이 공개될 틱. 판이 아직 시작하지 않았으면 long.MaxValue.</summary>
        public long FirstRevealTick { get; private set; } = long.MaxValue;
        public int LastStageIndex { get; private set; }
        public float InitialRadius { get; private set; }

        /// <summary>마지막 원이 다 줄어든 뒤 초마다 더해지는 구역 피해. 서버와 같은 값으로 경고 여부를 잰다.</summary>
        public float OvertimeDpsGrowth { get; set; }

        public void SetZoneClock(double renderTick, float tickInterval, long firstRevealTick, int lastStageIndex, float initialRadius)
        {
            RenderTick = renderTick;
            TickInterval = tickInterval;
            FirstRevealTick = firstRevealTick;
            LastStageIndex = lastStageIndex;
            InitialRadius = initialRadius;
        }

        /// <summary>
        /// 소수 틱의 원. 식은 정수 틱만 받으므로 앞뒤 두 틱에서 구해 사이를 잇는다 —
        /// 안 그러면 원이 틱마다 계단으로 줄어든다.
        /// </summary>
        public (Vector2 center, float radius) CircleAtRender(double tick)
        {
            long lo = (long)System.Math.Floor(tick);
            float frac = (float)(tick - lo);
            var a = MountedArcheryZone.CircleAt(zoneStages, lo, Vector2.zero, InitialRadius);
            var b = MountedArcheryZone.CircleAt(zoneStages, lo + 1, Vector2.zero, InitialRadius);
            return (Vector2.Lerp(a.center, b.center, frac), Mathf.Lerp(a.radius, b.radius, frac));
        }

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

        public IEnumerable<string> AliveIds
        {
            get
            {
                foreach (var pair in players)
                {
                    if (pair.Value.eliminatedTick < 0) yield return pair.Key;
                }
            }
        }

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

            zoneStages.Clear();
            foreach (var z in message.ZoneStages)
            {
                zoneStages.Add(new ZoneStage(z.Index, z.RevealTick, z.ShrinkStartTick, z.ShrinkEndTick,
                    new Vector2(z.FromX, z.FromZ), z.FromRadius, new Vector2(z.ToX, z.ToZ), z.ToRadius, z.DamagePerSecond));
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
