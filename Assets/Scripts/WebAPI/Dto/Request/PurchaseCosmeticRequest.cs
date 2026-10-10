using System;

namespace LOP
{
    /// <summary>가격 하나. 통화 id와 금액(마스터데이터 TbCosmetic.prices의 한 항목과 같은 모양).</summary>
    [Serializable]
    public class PriceDto
    {
        public int currencyId;
        public int amount;
    }

    public class PurchaseCosmeticRequest
    {
        //  구매 한 번마다 클라가 만드는 키. 같은 키로 다시 보내면 같은 응답을 받는다(멱등).
        public string idempotencyKey;
        public int cosmeticId;
        //  화면에 보여 준 가격. 서버 가격과 다르면 80003으로 거절된다 — 가격은 서버가 정한다.
        public PriceDto expectedPrice;
        //  사자마자 장착할지.
        public bool equip;
    }
}
