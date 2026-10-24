using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 말 활쏘기 관전 대상. 살아 있는 기수 중 나를 뺀 사람이고, 나를 쓰러뜨린 사람이 맨 앞이다
    /// (누가 날 맞혔는지 따라가 볼 수 있게). 나머지는 id 순이다.
    /// </summary>
    public class MountedSpectate : ISpectateTargets
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly MountedArcheryClientState state;
        private readonly List<string> candidates = new List<string>();
        private string killerId;

        public IReadOnlyList<string> Candidates => candidates;

        public string Current { get; private set; }

        public MountedSpectate(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
            MountedArcheryClientState state)
        {
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
            this.state = state;
            state.Eliminated += OnEliminated;
        }

        private void OnEliminated(MountedArcheryClientState.Elimination e)
        {
            if (e.VictimId == gameDataStore.userEntityId)
            {
                killerId = e.KillerId;
            }
        }

        public void Refresh()
        {
            string mine = gameDataStore.userEntityId;
            candidates.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Id == mine || entity.Get<Mount>() == null || state.IsEliminated(entity.Id))
                {
                    continue;
                }
                candidates.Add(entity.Id);
            }

            candidates.Sort(CompareKillerFirst);

            if (candidates.Count == 0)
            {
                Current = null;
            }
            else if (Current == null || candidates.Contains(Current) == false)
            {
                Current = candidates[0];
            }
        }

        public void Next() => Step(1);

        public void Prev() => Step(-1);

        private void Step(int delta)
        {
            int index = Current == null ? -1 : candidates.IndexOf(Current);
            if (index < 0)
            {
                return;
            }

            int count = candidates.Count;
            Current = candidates[((index + delta) % count + count) % count];
        }

        private int CompareKillerFirst(string a, string b)
        {
            bool aKiller = a == killerId;
            bool bKiller = b == killerId;
            if (aKiller != bKiller)
            {
                return aKiller ? -1 : 1;
            }
            return string.CompareOrdinal(a, b);
        }
    }
}
