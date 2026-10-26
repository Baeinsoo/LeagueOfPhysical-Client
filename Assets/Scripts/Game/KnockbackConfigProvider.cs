namespace LOP
{
    /// <summary>TbKnockbackConfig id=1 → 공용 KnockbackConfig. 클·서가 같은 행을 읽어야 바닥과 속도가 같다.</summary>
    public class KnockbackConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public KnockbackConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public KnockbackConfig Get()
        {
            var r = md.Tables.TbKnockbackConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbKnockbackConfig id 1 이 없다 — TableFiles에 tbknockbackconfig가 있나");
            }
            return new KnockbackConfig(r.MatchDurationSeconds, r.MoveSpeed, r.MoveGain, r.ArenaRadius,
                                       r.TileSize, r.HoleRadius, r.FallDepth, r.SpawnRadius,
                                       r.ShotSpeed, r.ShotRange, r.ShotRadius, r.HitRadius, r.AmmoMax, r.ReloadSeconds, r.FireCooldownSeconds, r.Recoil, r.DamagePerHit, r.KnockbackBase, r.KnockbackScale, r.KnockbackCarry, r.StunBase, r.StunScale, r.StunGain, r.BodyRestitution, r.ZoneFirstRevealSeconds,
                                       ZoneStages());
        }

        //  엑셀 줄이 옮겨져도 1단계부터 순서대로 나가도록 id로 정렬한다. 원 밖 피해는 이 모드에 없다(0).
        private System.Collections.Generic.List<MountedArcheryZoneStageConfig> ZoneStages()
        {
            var stages = new System.Collections.Generic.List<MountedArcheryZoneStageConfig>();
            foreach (var row in System.Linq.Enumerable.OrderBy(md.Tables.TbKnockbackZone.DataList, x => x.Id))
            {
                stages.Add(new MountedArcheryZoneStageConfig(row.RestSeconds, row.ShrinkSeconds, row.EndRadius, 0f));
            }
            return stages;
        }
    }
}
