namespace LOP
{
    /// <summary>TbCardRushConfig id=1 → 공용 CardRushConfig. 클·서가 같은 행을 읽어야 코스·벌이 같다.</summary>
    public class CardRushConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public CardRushConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public CardRushConfig Get()
        {
            var r = md.Tables.TbCardRushConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbCardRushConfig id 1 이 없다 — TableFiles에 tbcardrushconfig가 있나");
            }
            return new CardRushConfig(r.MatchDurationSeconds, r.MinLanes, r.TrackLengthM,
                                      r.ObstacleGapMinM, r.ObstacleGapMaxM, r.BarricadeShare,
                                      r.ZoneGapMinM, r.ZoneGapMaxM, r.BarricadeWaitSeconds,
                                      r.RollKnockM, r.RollStunSeconds, r.ZoneKnockM, r.ZoneStunSeconds,
                                      r.BotReactMinSeconds, r.BotReactMaxSeconds, r.BotReactExtraSeconds,
                                      r.BotHesitateChance, r.BotFreeDashChance,
                                      r.PitShare, r.PitStunSeconds);
        }
    }
}
