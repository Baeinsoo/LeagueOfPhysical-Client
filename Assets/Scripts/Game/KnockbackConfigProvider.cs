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
                                       r.TileSize, r.HoleRadius, r.FallDepth, r.SpawnRadius);
        }
    }
}
