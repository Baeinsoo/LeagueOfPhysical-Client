namespace LOP
{
    /// <summary>꾸밈 구매 결과(로비 POST /user/:id/economy/purchase). 거절이면 code만 오고
    /// owned/wallets/loadout은 null이다.</summary>
    public class PurchaseCosmeticResponse : HttpResponse
    {
        //  산(또는 재생으로 이미 산) 인스턴스
        public OwnedCosmeticDto owned;
        public WalletDto[] wallets;
        public LoadoutSlotDto[] loadout;
    }
}
