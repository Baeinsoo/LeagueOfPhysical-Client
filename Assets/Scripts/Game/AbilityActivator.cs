namespace LOP
{
    /// <summary>
    /// 어빌리티 id로 발동을 라우팅하는 side-local 서비스(런타임 식별=int id).
    /// id가 <c>TbAbility</c>에 있으면 <see cref="AbilitySystem.TryActivate"/>로 발동하고 true, 아니면 false.
    /// </summary>
    public class AbilityActivator
    {
        private readonly AbilitySystem abilitySystem;
        private readonly AbilityDataProvider abilityDataProvider;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;

        public AbilityActivator(
            AbilitySystem abilitySystem,
            AbilityDataProvider abilityDataProvider,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer worldEventBuffer)
        {
            this.abilitySystem = abilitySystem;
            this.abilityDataProvider = abilityDataProvider;
            this.entityRegistry = entityRegistry;
            this.worldEventBuffer = worldEventBuffer;
        }

        public bool TryActivate(string casterEntityId, int abilityId, long currentTick)
        {
            if (abilityDataProvider.TryGet(abilityId, out var ability) == false)
            {
                return false;
            }

            var caster = entityRegistry.Get(casterEntityId);
            if (caster == null)
            {
                return false;
            }

            // effect는 ability.Effects에 실려 있고, Active 창에서 executor가 타입별 핸들러로 디스패치한다.
            bool activated = abilitySystem.TryActivate(caster, ability, caster, currentTick);
            if (activated)
            {
                // 발동 연출 cue — 내 캐릭 예측 발동도 여기서 한 곳에 모아 송출(서버 사본은 핸들러가 자기 스킵).
                worldEventBuffer.Append(new GameFramework.World.AbilityActivatedEvent(casterEntityId, abilityId));
            }
            return activated;
        }

        /// <summary>슬롯에 장착된 어빌리티 id를 찾는다. 입력 캡처가 슬롯을 id로 풀 때 쓴다.</summary>
        public bool TryGetAbilityIdBySlot(string casterEntityId, int slot, out int abilityId)
        {
            abilityId = 0;
            var caster = entityRegistry.Get(casterEntityId);
            if (caster == null)
            {
                return false;
            }
            return abilitySystem.TryGetAbilityIdBySlot(caster, slot, out abilityId);
        }

        /// <summary>슬롯으로 발동. id를 푼 뒤 기존 <see cref="TryActivate"/> 경로로 합류한다.</summary>
        public bool TryActivateSlot(string casterEntityId, int slot, long currentTick)
        {
            if (TryGetAbilityIdBySlot(casterEntityId, slot, out int abilityId) == false)
            {
                return false;
            }
            return TryActivate(casterEntityId, abilityId, currentTick);
        }
    }
}
