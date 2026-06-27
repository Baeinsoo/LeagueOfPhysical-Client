using VContainer;

namespace LOP
{
    /// <summary>
    /// 어빌리티 id로 발동을 라우팅하는 side-local 서비스(런타임 식별=int id).
    /// id가 <c>TbAbility</c>에 있으면 <see cref="AbilitySystem.TryActivate"/>로 발동하고 true, 아니면 false.
    /// </summary>
    public class AbilityActivator
    {
        [Inject]
        private AbilitySystem abilitySystem;

        [Inject]
        private AbilityDataProvider abilityDataProvider;

        [Inject]
        private StatusEffectDataProvider statusEffectDataProvider;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

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

            var effects = new StatusEffectData[ability.ProducesEffectIds.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = statusEffectDataProvider.Get(ability.ProducesEffectIds[i]);
            }

            abilitySystem.TryActivate(caster, ability, caster, effects, currentTick);
            return true;
        }
    }
}
