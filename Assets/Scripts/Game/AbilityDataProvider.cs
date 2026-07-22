namespace LOP
{
    /// <summary>
    /// Luban <c>TbAbility</c> 행을 LOP-Shared <see cref="AbilityData"/>로 매핑하는 side-local 어댑터.
    /// 런타임 식별은 int <c>id</c>. (테이블의 string <c>code</c>는 데이터/에디터 별칭으로, 런타임 경로엔 쓰지 않는다.)
    /// </summary>
    public class AbilityDataProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public AbilityDataProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

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
            return new AbilityData(r.Id, r.CooldownTicks, r.MpCost,
                r.StartupTicks, r.ActiveTicks, r.RecoveryTicks,
                MapEffects(r.Effects),
                r.StartupMoveScale, r.ActiveMoveScale, r.RecoveryMoveScale, r.BlockJump);
        }

        // Luban 다형 effect 행을 코어 AbilityEffect로 매핑(코어는 MasterData 비참조 → 여기서 변환).
        private static AbilityEffect[] MapEffects(System.Collections.Generic.List<LOP.MasterData.AbilityEffect> src)
        {
            if (src == null || src.Count == 0)
            {
                return System.Array.Empty<AbilityEffect>();
            }
            var result = new System.Collections.Generic.List<AbilityEffect>(src.Count);
            foreach (var e in src)
            {
                switch (e)
                {
                    case LOP.MasterData.StatusEffectApplyEffect s:
                        result.Add(new StatusEffectApplyEffect(s.StatusEffectId));
                        break;
                    case LOP.MasterData.MotionEffect m:
                        result.Add(new MotionEffect(m.Speed));
                        break;
                    case LOP.MasterData.DamageEffect d:
                        result.Add(new DamageEffect(d.Amount, d.Range, d.Angle));
                        break;
                    case LOP.MasterData.KnockbackEffect k:
                        result.Add(new KnockbackEffect(k.Strength, k.DurationTicks, k.DecayPerTick));
                        break;
                }
            }
            return result.ToArray();
        }
    }
}
