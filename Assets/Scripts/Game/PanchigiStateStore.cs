using System.Collections.Generic;
using R3;

namespace LOP
{
    /// <summary>
    /// 최신 판치기 턴 상태(클라). 메시지가 UI보다 먼저 도착해도 잃지 않도록 여기 담아 둔다 —
    /// reliable은 *도착*을 보장하지만 받을 준비까지 보장하지 않는다.
    /// </summary>
    public class PanchigiStateStore
    {
        private const int AimingPhase = 1;

        private readonly ReactiveProperty<int> phase = new(0);
        private readonly ReactiveProperty<string> currentEntityId = new(string.Empty);
        private readonly ReactiveProperty<long> aimDeadlineTick = new(0);
        private readonly ReactiveProperty<int> turnCount = new(0);

        //  낙 횟수와 탈락자는 매 프레임 읽히기만 하므로(구독 없음) 평범한 컬렉션으로 둔다.
        private readonly Dictionary<string, int> dropOutCounts = new();
        private readonly HashSet<string> eliminated = new();

        public ReadOnlyReactiveProperty<int> Phase => phase;
        public ReadOnlyReactiveProperty<string> CurrentEntityId => currentEntityId;
        public ReadOnlyReactiveProperty<long> AimDeadlineTick => aimDeadlineTick;

        /// <summary>지금까지 지나간 턴 수. 판치기는 시간이 아니라 이 수로 끝난다.</summary>
        public ReadOnlyReactiveProperty<int> TurnCount => turnCount;

        public int GetDropOutCount(string entityId)
        {
            RequireEntityId(entityId);
            return dropOutCounts.TryGetValue(entityId, out int count) ? count : 0;
        }

        public bool IsEliminated(string entityId)
        {
            RequireEntityId(entityId);
            return eliminated.Contains(entityId);
        }

        //  id 없이 물으면 조용히 false·0이 나오던 자리다. 그 침묵이 실제로 버그를 감췄다 —
        //  부르는 쪽이 "정체" 대신 "지금 몸이 있나"를 넘기고 있었는데 두 값이 우연히 같아서
        //  아무도 몰랐다. 이제 터뜨린다: 이 질문은 참가자를 아는 쪽만 할 수 있다.
        private static void RequireEntityId(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                throw new System.ArgumentException(
                    "엔티티 id 없이 판치기 상태를 물었다 — 내가 누구인지 안 뒤에 물어야 한다.",
                    nameof(entityId));
            }
        }

        /// <summary>지금 조준을 받는 국면인가.</summary>
        public bool IsAiming => phase.CurrentValue == AimingPhase;

        /// <summary>이 사람이 지금 칠 차례인가 — 입력을 열지, 게이지를 띄울지가 같은 판단이어야 한다.</summary>
        public bool IsAimingTurnOf(string entityId)
        {
            //  IsEliminated가 검사하지만 여기서 먼저 한다 — 조준 국면이 아니면 단축평가로
            //  거기까지 안 가서, id 없는 질문이 조용히 false로 빠져나간다.
            RequireEntityId(entityId);

            return IsAiming
                && currentEntityId.CurrentValue == entityId
                && IsEliminated(entityId) == false;
        }

        public void Set(int phase, string currentEntityId, long aimDeadlineTick, int turnCount,
            IReadOnlyDictionary<string, int> dropOutCounts, IEnumerable<string> eliminated)
        {
            this.phase.Value = phase;
            this.currentEntityId.Value = currentEntityId;
            this.aimDeadlineTick.Value = aimDeadlineTick;
            this.turnCount.Value = turnCount;

            //  서버가 매번 전부 보내므로 통째로 갈아 끼운다 — 지운 뒤 채우지 않으면 옛 값이 남는다.
            this.dropOutCounts.Clear();
            foreach (var pair in dropOutCounts) { this.dropOutCounts[pair.Key] = pair.Value; }

            this.eliminated.Clear();
            foreach (string id in eliminated) { this.eliminated.Add(id); }
        }
    }
}
