namespace LOP
{
    /// <summary>TbDodgeConfig id=1 → 공용 DodgeConfig. 클·서가 같은 행을 읽어야 그림과 판정이 같다.</summary>
    public class DodgeConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public DodgeConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public DodgeConfig Get()
        {
            var r = md.Tables.TbDodgeConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbDodgeConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 DodgeConfig 데이터 누락");
            }
            return new DodgeConfig(r.Lives, r.InvulnerableSeconds, r.HitRadius, r.LeadSeconds, r.ArenaHalf,
                                   r.TileCount, r.FirstPatternDelaySeconds, r.PatternIntervalSeconds, r.OnlyKind,
                                   r.WarnSeconds, r.BulletSpeed, r.BulletRadius, r.BombRadius, r.BombActiveSeconds,
                                   r.LaserWidth, r.LaserOnSeconds, r.RockSpeed, r.RockRadius, r.TileOnSeconds);
        }
    }
}
