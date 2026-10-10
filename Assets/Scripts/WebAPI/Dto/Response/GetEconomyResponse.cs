namespace LOP
{
    /// <summary>내 재화·레벨·꾸밈(로비 GET /user/:id/economy).</summary>
    public class GetEconomyResponse : HttpResponse
    {
        public WalletDto[] wallets;
        public ProgressDto progress;
        public LoadoutSlotDto[] loadout;
        public OwnedCosmeticDto[] owned;
    }
}
