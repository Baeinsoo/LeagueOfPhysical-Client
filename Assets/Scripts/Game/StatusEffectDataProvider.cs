namespace LOP
{
    /// <summary>
    /// Luban <c>TbStatusEffect</c> 행을 LOP-Shared <see cref="StatusEffectData"/>로 매핑하는 side-local 어댑터.
    /// (LOP-Shared는 MasterData 비참조이므로 데이터 출처 매핑은 use-side가 소유.)
    /// </summary>
    public class StatusEffectDataProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public StatusEffectDataProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        // 재조정 등 넷코드 경로는 서버가 보낸 id를 그대로 조회한다 — 구버전 데이터 등으로 없는 id가 와도
        // 던지면 안 되므로(호출부가 "모르면 무시"를 전제) GetOrDefault로 null 반환.
        public StatusEffectData? Get(int effectId)
        {
            var r = md.Tables.TbStatusEffect.GetOrDefault(effectId);
            if (r == null)
            {
                return null;
            }

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
