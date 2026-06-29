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

            // effect는 ability.Effects에 실려 있고, Active 창에서 executor가 타입별 핸들러로 디스패치한다.
            // 코어 결과를 그대로 반환 — 실제 발동(쿨다운/busy/자원 통과)됐을 때만 true(연출 발화 게이트로 쓰임).
            return abilitySystem.TryActivate(caster, ability, caster, currentTick);
        }
    }
}
