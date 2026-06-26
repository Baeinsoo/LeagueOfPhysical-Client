using VContainer;

namespace LOP
{
    /// <summary>
    /// Luban <c>TbStatusEffect</c> 행을 LOP-Shared <see cref="StatusEffectData"/>로 매핑하는 side-local 어댑터.
    /// (LOP-Shared는 MasterData 비참조이므로 데이터 출처 매핑은 use-side가 소유.)
    /// </summary>
    public class StatusEffectDataProvider
    {
        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public StatusEffectData Get(int effectId)
        {
            var r = md.Tables.TbStatusEffect.Get(effectId);

            StatusModifierSpec[] modifiers;
            if (string.IsNullOrEmpty(r.ModStatType))
            {
                modifiers = System.Array.Empty<StatusModifierSpec>();
            }
            else
            {
                int statType = (int)(GameFramework.World.EntityStatType)
                    System.Enum.Parse(typeof(GameFramework.World.EntityStatType), r.ModStatType);
                var modType = (GameFramework.World.ModifierType)
                    System.Enum.Parse(typeof(GameFramework.World.ModifierType), r.ModType);
                modifiers = new[] { new StatusModifierSpec(statType, r.ModValue, modType) };
            }

            var durationPolicy = (DurationPolicy)System.Enum.Parse(typeof(DurationPolicy), r.DurationPolicy);
            var stackPolicy = (StatusStackPolicy)System.Enum.Parse(typeof(StatusStackPolicy), r.StackPolicy);

            return new StatusEffectData(r.Id, durationPolicy, r.DurationTicks, modifiers, stackPolicy, r.MaxStacks);
        }
    }
}
