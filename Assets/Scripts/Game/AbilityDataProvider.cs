using VContainer;

namespace LOP
{
    /// <summary>
    /// Luban <c>TbAbility</c> 행을 LOP-Shared <see cref="AbilityData"/>로 매핑하는 side-local 어댑터.
    /// 런타임 식별은 int <c>id</c>. (테이블의 string <c>code</c>는 데이터/에디터 별칭으로, 런타임 경로엔 쓰지 않는다.)
    /// </summary>
    public class AbilityDataProvider
    {
        [Inject]
        private LOP.MasterData.LOPMasterData md;

        /// <summary>어빌리티 id로 설정을 조회(런타임 식별=int id). 없으면 false.</summary>
        public bool TryGet(int abilityId, out AbilityData data)
        {
            var row = md.Tables.TbAbility.GetOrDefault(abilityId);
            if (row == null)
            {
                data = default;
                return false;
            }

            data = Map(row);
            return true;
        }

        private static AbilityData Map(LOP.MasterData.Ability r)
        {
            var targeting = (TargetingMode)System.Enum.Parse(typeof(TargetingMode), r.TargetingMode);
            int[] effects = r.ProducesEffectId == 0
                ? System.Array.Empty<int>()
                : new[] { r.ProducesEffectId };
            return new AbilityData(r.Id, r.CooldownTicks, r.MpCost, r.CastTimeTicks, targeting, r.Range, effects);
        }
    }
}
