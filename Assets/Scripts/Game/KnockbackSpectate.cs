using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 넉백 관전 대상. 캐릭터 중 나·탈락한 사람·이미 떨어지는 몸을 뺀 사람이고 id 순이다.
    /// </summary>
    public class KnockbackSpectate : ISpectateTargets
    {
        //  월드의 몸 모으기(KnockbackWorld)와 같은 기준 — 이보다 아래면 떨어지는 중이다.
        private const float FallingY = -0.05f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly KnockbackClientState state;
        private readonly List<string> candidates = new List<string>();

        public IReadOnlyList<string> Candidates => candidates;

        public string Current { get; private set; }

        public KnockbackSpectate(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
            KnockbackClientState state)
        {
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
            this.state = state;
        }

        public void Refresh()
        {
            string mine = gameDataStore.userEntityId;
            candidates.Clear();
            foreach (var entity in entityRegistry.All)
            {
                var kind = entity.Get<EntityKind>();
                var transform = entity.Get<GameFramework.World.Transform>();
                if (entity.Id == mine || kind == null || kind.Kind != EntityType.Character || transform == null
                    || state.IsEliminated(entity.Id) || transform.Position.Y < FallingY)
                {
                    continue;
                }
                candidates.Add(entity.Id);
            }

            // 팀전이면 우리 팀을 먼저 보여 준다. 개인전(내 팀 0)은 id 순 그대로.
            int myTeam = state.TeamCount >= 2 ? state.MyTeamOf(mine) : 0;
            candidates.Sort((a, b) =>
            {
                if (myTeam != 0)
                {
                    int byTeam = (state.TeamOf(a) == myTeam ? 0 : 1).CompareTo(state.TeamOf(b) == myTeam ? 0 : 1);
                    if (byTeam != 0) return byTeam;
                }
                return string.CompareOrdinal(a, b);
            });

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
    }
}
