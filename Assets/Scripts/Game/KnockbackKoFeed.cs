using System.Collections.Generic;

namespace LOP
{
    public readonly struct KoPlayer
    {
        public readonly string Id;
        public readonly long EliminatedTick;
        public readonly string KoBy;
        public readonly bool KoChain;
        public readonly int Team;

        public KoPlayer(string id, long eliminatedTick, string koBy, bool koChain, int team)
        {
            Id = id;
            EliminatedTick = eliminatedTick;
            KoBy = koBy;
            KoChain = koChain;
            Team = team;
        }
    }

    public readonly struct KoEvent
    {
        public readonly string VictimId;
        /// <summary>장외시킨 사람. 자멸이면 null.</summary>
        public readonly string KillerId;
        public readonly bool Chain;
        public readonly long Tick;
        /// <summary>장외시킨 사람의 연속 장외 횟수(1 = 단발). 자멸은 1.</summary>
        public readonly int Streak;

        public KoEvent(string victimId, string killerId, bool chain, long tick, int streak)
        {
            VictimId = victimId;
            KillerId = killerId;
            Chain = chain;
            Tick = tick;
            Streak = streak;
        }
    }

    /// <summary>
    /// 중계 자막의 판단. 서버 상태에서 "새로 탈락한 사람"을 골라 문장과 연속 장외를 셈한다. Unity 비의존.
    /// </summary>
    public class KnockbackKoFeed
    {
        //  3초 = 50틱/초 × 3. 틱 간격이 바뀌면 같이 고친다.
        public const int StreakWindowTicks = 150;

        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly Dictionary<string, (long tick, int count)> streaks = new Dictionary<string, (long, int)>();
        private bool first = true;

        /// <summary>
        /// 새로 탈락한 사람만 탈락 틱 순서로 돌려준다. 처음 받은 상태의 탈락자는 "이미 본 것"으로만 적는다 —
        /// 중간에 들어오거나 다시 접속한 사람에게 지난 장외를 다시 방송하지 않으려고.
        /// </summary>
        public List<KoEvent> Observe(IReadOnlyList<KoPlayer> players)
        {
            var fresh = new List<KoPlayer>();
            foreach (var p in players)
            {
                if (p.EliminatedTick < 0 || seen.Contains(p.Id)) continue;
                seen.Add(p.Id);
                if (!first) fresh.Add(p);
            }
            first = false;

            fresh.Sort((a, b) =>
            {
                int byTick = a.EliminatedTick.CompareTo(b.EliminatedTick);
                return byTick != 0 ? byTick : string.CompareOrdinal(a.Id, b.Id);
            });

            var events = new List<KoEvent>();
            foreach (var p in fresh)
            {
                string killer = string.IsNullOrEmpty(p.KoBy) || p.KoBy == p.Id ? null : p.KoBy;
                int streak = 1;
                if (killer != null)
                {
                    if (streaks.TryGetValue(killer, out var last) && p.EliminatedTick - last.tick <= StreakWindowTicks)
                    {
                        streak = last.count + 1;
                    }
                    streaks[killer] = (p.EliminatedTick, streak);
                }
                events.Add(new KoEvent(p.Id, killer, p.KoChain, p.EliminatedTick, streak));
            }
            return events;
        }

        public static string Line(string killerName, string victimName, bool chain, int streak)
        {
            if (killerName == null) return $"{victimName} 선수, 스스로 장외";
            string text = $"{killerName} ▶ {victimName} {(chain ? "연쇄 " : "")}장외!";
            if (streak >= 3) return text + " 트리플 장외!";
            if (streak == 2) return text + " 더블 장외!";
            return text;
        }

        /// <summary>이 상태에서 판이 끝났는가: 개인전은 참가자 2명 이상에 살아남은 사람 ≤ 1, 팀전은 한 팀 전원 탈락.</summary>
        public static bool IsFinish(IReadOnlyList<KoPlayer> players, int teamCount)
        {
            if (teamCount >= 2)
            {
                var alive = new Dictionary<int, int>();
                foreach (var p in players)
                {
                    if (p.Team == 0) continue;
                    alive.TryGetValue(p.Team, out int n);
                    alive[p.Team] = n + (p.EliminatedTick < 0 ? 1 : 0);
                }
                foreach (var n in alive.Values)
                {
                    if (n == 0) return true;
                }
                return false;
            }

            int living = 0;
            foreach (var p in players)
            {
                if (p.EliminatedTick < 0) living++;
            }
            return players.Count >= 2 && living <= 1;
        }
    }
}
