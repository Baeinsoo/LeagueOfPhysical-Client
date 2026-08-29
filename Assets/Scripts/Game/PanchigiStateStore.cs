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
            return entityId != null && dropOutCounts.TryGetValue(entityId, out int count) ? count : 0;
        }

        public bool IsEliminated(string entityId)
        {
            return entityId != null && eliminated.Contains(entityId);
        }

        /// <summary>지금 조준을 받는 국면인가.</summary>
        public bool IsAiming => phase.CurrentValue == AimingPhase;

        /// <summary>이 사람이 지금 칠 차례인가 — 입력을 열지, 게이지를 띄울지가 같은 판단이어야 한다.</summary>
        public bool IsAimingTurnOf(string entityId)
        {
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
