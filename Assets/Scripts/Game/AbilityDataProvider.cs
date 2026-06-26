namespace LOP
{
    /// <summary>
    /// 어빌리티 설정 제공(side-local 어댑터).
    /// TEMP(3d): 하드코딩 헤이스트. 3e에서 내부를 Luban <c>TbAbility</c> 조회로 교체(호출부 불변).
    /// </summary>
    public class AbilityDataProvider
    {
        public AbilityData Get(int abilityId)
        {
            // TEMP(3d): 하드코딩 헤이스트(effect 1 생성). 3e에서 TbAbility로 교체.
            return new AbilityData(abilityId, cooldownTicks: 0, mpCost: 0, castTimeTicks: 0,
                TargetingMode.Self, range: 0f, producesEffectIds: new[] { 1 });
        }
    }
}
