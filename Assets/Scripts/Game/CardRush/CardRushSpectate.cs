using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 지금 누구를 보고 있나(spec §7.6). 완주 전엔 나. 완주 뒤 <see cref="FlappySpectate.FinishHoldSeconds"/>(2.2초,
    /// 결승 도장이 사라지는 시간과 같은 값) 머문 뒤엔 아직 못 들어온 사람 중 <b>가장 앞선(진행이 큰) 사람</b>으로
    /// 넘어간다 — 다음 결승이 바로 보이게(Flappy의 "꼴찌를 본다"와 반대 — 쫓아오는 추격자가 없는 모드라
    /// 다음 완주자를 보여 주는 쪽이 자연스럽다). 사람이 <see cref="Next"/>/<see cref="Prev"/>로 직접 고를 수 있다
    /// (<see cref="FlappySpectate"/>와 같은 통로).
    /// </summary>
    public class CardRushSpectate : ISpectateTargets
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;

        //  매 틱 도는 코드라 목록을 새로 만들지 않고 비워서 다시 쓴다.
        private readonly List<Ranked> scratch = new List<Ranked>();
        private readonly List<string> candidates = new List<string>();

        private static readonly Comparison<Ranked> LeaderFirst = CompareLeaderFirst;

        //  결승 붙잡기 타이머 — 내가 완주한 채로 이어지는 동안 흐른 시간을 쌓는다.
        private float elapsedSinceFinish;
        private bool wasMineFinished;

        private struct Ranked
        {
            public string Id;
            public int Step;
        }

        /// <summary>지금 볼 수 있는 사람. 선두(가장 앞선 사람)가 0번이다.</summary>
        public IReadOnlyList<string> Candidates => candidates;

        /// <summary>지금 보는 사람. 볼 사람이 없으면 null.</summary>
        public string Current { get; private set; }

        public CardRushSpectate(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore)
        {
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
        }

        /// <summary>후보를 다시 만들고, 보던 사람이 사라졌으면 다시 고른다. 매 틱 부른다.</summary>
        public void Refresh() => Refresh(UnityEngine.Time.deltaTime);

        //  시험 이음매 — 결승 붙잡기 타이머를 실제 UnityEngine.Time 없이 결정적으로 돌린다(FlappySpectate와 같은 자리).
        internal void Refresh(float deltaTime)
        {
            Rebuild();

            string mine = gameDataStore.userEntityId;
            var mineEntity = string.IsNullOrEmpty(mine) ? null : entityRegistry.Get(mine);
            bool mineFinishedNow = mineEntity != null && Finished(mineEntity);

            //  완주 상태가 이어지는 동안만 흐른 시간을 쌓는다. 탈락 같은 중간 리셋은 카드 러시엔 없지만,
            //  엔티티가 사라지면(나가기 등) mineFinishedNow가 false가 되어 바로 0으로 돌아간다.
            elapsedSinceFinish = mineFinishedNow && wasMineFinished ? elapsedSinceFinish + deltaTime : 0f;
            wasMineFinished = mineFinishedNow;

            if (mineFinishedNow && elapsedSinceFinish < FlappySpectate.FinishHoldSeconds)
            {
                Current = mine;
                return;
            }

            if (candidates.Count == 0)
            {
                Current = null;
                return;
            }

            //  내가 아직 달리고 있으면 무조건 나를 본다 — 관전 조작은 내가 빠진 뒤에만 연다.
            if (string.IsNullOrEmpty(mine) == false && candidates.Contains(mine))
            {
                Current = mine;
                return;
            }

            //  보던 사람이 그대로면 손대지 않는다 — 수동 선택이 살아남는 자리가 여기다.
            if (Current != null && candidates.Contains(Current))
            {
                return;
            }

            Current = candidates[0];   // 가장 앞선 사람 — 정렬 규칙 덕에 0번이 곧 그 사람
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

                var runner = entity.Get<CardRushRunner>();
                if (runner == null || Finished(entity))
                {
                    continue;
                }

                scratch.Add(new Ranked { Id = entity.Id, Step = runner.State.Step });
            }

            scratch.Sort(LeaderFirst);

            candidates.Clear();
            for (int i = 0; i < scratch.Count; i++)
            {
                candidates.Add(scratch[i].Id);
            }
        }

        //  완주했나. 답이 두 갈래인 이유: 클라 시뮬은 내 몸만 굴리므로(Simulated), FinishState는 내 몸에만
        //  정확히 적힌다. 남의 완주는 서버가 스냅샷에 실어 준 등수(FinishPlacement)로만 안다.
        private static bool Finished(GameFramework.World.Entity entity)
        {
            return (entity.Get<FinishState>()?.Finished ?? false)
                || (entity.Get<FinishPlacement>()?.Value ?? 0) > 0;
        }

        //  칸 수 내림차순(선두가 0번). 같은 칸이면 id 오름차순 — 동률에서도 숫자가 안 깜빡인다.
        private static int CompareLeaderFirst(Ranked a, Ranked b)
        {
            int byStep = b.Step.CompareTo(a.Step);
            return byStep != 0 ? byStep : string.CompareOrdinal(a.Id, b.Id);
        }
    }
}
