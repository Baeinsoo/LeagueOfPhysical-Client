using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 지금 누구를 보고 있나. 살아 있으면 내 새, 아니면 <b>아직 달리는 사람 중 꼴찌</b>다 —
    /// 다음에 잡힐 사람이라 추격자가 같은 화면 안에 있다(선두를 보면 벽이 화면 밖이라
    /// 아무 일도 안 일어난다). 완주·탈락한 뒤에는 사람이 <see cref="Next"/>/<see cref="Prev"/>로
    /// 직접 고를 수 있다.
    ///
    /// <para>카메라와 추격자 벽이 <b>같은 답</b>을 봐야 해서 주인을 하나로 둔다. 주인이 둘이면
    /// 자동 추적이 매 틱 수동 선택을 덮어쓴다.</para>
    /// </summary>
    public class FlappySpectate
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;

        //  매 틱 도는 코드라 목록을 새로 만들지 않고 비워서 다시 쓴다.
        private readonly List<Ranked> scratch = new List<Ranked>();
        private readonly List<string> candidates = new List<string>();

        private static readonly Comparison<Ranked> LeaderFirst = CompareLeaderFirst;

        private struct Ranked
        {
            public string Id;
            public float X;
        }

        /// <summary>지금 볼 수 있는 사람. 선두가 0번이다.</summary>
        public IReadOnlyList<string> Candidates => candidates;

        /// <summary>지금 보는 사람. 볼 사람이 없으면 null.</summary>
        public string Current { get; private set; }

        public FlappySpectate(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore)
        {
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
        }

        /// <summary>후보를 다시 만들고, 보던 사람이 사라졌으면 다시 고른다. 매 틱 부른다.</summary>
        public void Refresh()
        {
            Rebuild();

            if (candidates.Count == 0)
            {
                Current = null;
                return;
            }

            //  보던 사람이 그대로면 손대지 않는다 — 수동 선택이 살아남는 자리가 여기다.
            if (Current != null && candidates.Contains(Current))
            {
                return;
            }

            string mine = gameDataStore.userEntityId;
            Current = string.IsNullOrEmpty(mine) == false && candidates.Contains(mine)
                ? mine
                : candidates[candidates.Count - 1];   // 꼴찌 — 정렬 규칙 덕에 마지막이 곧 그 사람이다
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

        private void Rebuild()
        {
            scratch.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }

                var body = entity.Get<GameFramework.World.Transform>();
                if (body == null || Finished(entity))
                {
                    continue;
                }

                scratch.Add(new Ranked { Id = entity.Id, X = body.Position.X });
            }

            scratch.Sort(LeaderFirst);

            candidates.Clear();
            for (int i = 0; i < scratch.Count; i++)
            {
                candidates.Add(scratch[i].Id);
            }
        }

        //  결승선을 넘었나. 답이 두 갈래인 이유: 클라 시뮬은 내 새만 굴리므로(Simulated),
        //  FinishState는 내 새에만 붙는다. 남의 통과는 서버가 스냅샷에 실어 준 등수로만 안다.
        private static bool Finished(GameFramework.World.Entity entity)
        {
            return (entity.Get<FinishState>()?.Finished ?? false)
                || (entity.Get<FinishPlacement>()?.Value ?? 0) > 0;
        }

        //  x 내림차순(선두가 0번). 같은 자리면 id 내림차순이라 <b>마지막이 "x 최소·id 최소"</b>가
        //  된다 — 꼴찌 폴백이 candidates[^1] 한 줄로 끝나고, 목록 순서와 폴백 규칙이 어긋날 수 없다.
        private static int CompareLeaderFirst(Ranked a, Ranked b)
        {
            int byX = b.X.CompareTo(a.X);
            return byX != 0 ? byX : string.CompareOrdinal(b.Id, a.Id);
        }
    }
}
