namespace LOP
{
    /// <summary>TbCardRushConfig id=1 → 공용 CardRushConfig. 클·서가 같은 행을 읽어야 이동·멈춤 틱이 같다.</summary>
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
            return new CardRushConfig(r.MatchDurationSeconds, r.MoveSeconds, r.StunSeconds, r.MinLanes,
                                      r.BotReactMinSeconds, r.BotReactMaxSeconds, r.BotTimingJitterSeconds, r.BotMissChance);
        }
    }
}
