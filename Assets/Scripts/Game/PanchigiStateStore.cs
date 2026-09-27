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

        //  타격 기록은 매 프레임 읽히기만 하므로(구독 없음) 평범한 컬렉션으로 둔다.
        private readonly List<string> playerEntityIds = new();
        private readonly Dictionary<string, IReadOnlyList<PanchigiRoll>> rolls = new();

        public ReadOnlyReactiveProperty<int> Phase => phase;
        public ReadOnlyReactiveProperty<string> CurrentEntityId => currentEntityId;
        public ReadOnlyReactiveProperty<long> AimDeadlineTick => aimDeadlineTick;

        /// <summary>참가 순서 — 점수판 줄 순서이자 동전 색의 번호다.</summary>
        public IReadOnlyList<string> PlayerEntityIds => playerEntityIds;

        public IReadOnlyList<PanchigiRoll> Rolls(string entityId)
        {
            RequireEntityId(entityId);
            return rolls.TryGetValue(entityId, out var list) ? list : System.Array.Empty<PanchigiRoll>();
        }

        public bool IsFinished(string entityId, int frameCount, int pinCount)
        {
            return PanchigiBowlingScore.Locate(Rolls(entityId), frameCount, pinCount).Complete;
        }

        /// <summary>
        /// 지금 화면의 판이 누구 것인가(골프식은 사람마다 판이 따로다). 조준 중이면 그 사람, 동전이 구르는
        /// 중이면 방금 친 사람 — 서버가 그동안 차례를 비워 보내므로 마지막 조준자를 기억해 둔다.
        /// </summary>
        public string BoardOwnerEntityId { get; private set; } = string.Empty;

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
            RequireEntityId(entityId);

            return IsAiming
                && currentEntityId.CurrentValue == entityId;
        }

        public void Set(int phase, string currentEntityId, long aimDeadlineTick,
            IReadOnlyList<(string entityId, IReadOnlyList<PanchigiRoll> rolls)> players)
        {
            this.phase.Value = phase;
            this.currentEntityId.Value = currentEntityId;
            this.aimDeadlineTick.Value = aimDeadlineTick;

            if (string.IsNullOrEmpty(currentEntityId) == false)
            {
                BoardOwnerEntityId = currentEntityId;
            }

            //  서버가 매번 전부 보내므로 통째로 갈아 끼운다 — 지운 뒤 채우지 않으면 옛 값이 남는다.
            playerEntityIds.Clear();
            rolls.Clear();
            foreach (var (entityId, list) in players)
            {
                playerEntityIds.Add(entityId);
                rolls[entityId] = list;
            }
        }
    }
}
