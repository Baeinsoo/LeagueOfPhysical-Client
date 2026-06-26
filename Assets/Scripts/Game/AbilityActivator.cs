using VContainer;

namespace LOP
{
    /// <summary>
    /// 입력 actionCode를 어빌리티 발동으로 라우팅하는 side-local 서비스.
    /// 코드가 어빌리티면 <see cref="AbilitySystem.TryActivate"/>로 발동하고 true, 아니면 false(레거시 액션 폴백).
    /// TEMP(3d): 단일 "haste"만 인식. 3e에서 Luban TbAbility 조회로 일반화(호출부 불변).
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

        public bool TryActivate(string casterEntityId, string code, long currentTick)
        {
            if (code != "haste")        // TEMP(3d): 단일 헤이스트. 3e에서 TbAbility 키 매칭으로 일반화.
            {
                return false;
            }

            var caster = entityRegistry.Get(casterEntityId);
            if (caster == null)
            {
                return false;
            }

            abilitySystem.TryActivate(caster, abilityDataProvider.Get(1), caster,
                new[] { statusEffectDataProvider.Get(1) }, currentTick);
            return true;
        }
    }
}
