using System;

namespace LOP
{
    /// <summary>보유 인스턴스 하나(로비 GET economy / 구매 응답).</summary>
    [Serializable]
    public class OwnedCosmeticDto
    {
        public string id;
        public int cosmeticId;
        public string source;
        public string acquiredAt;
    }
}
