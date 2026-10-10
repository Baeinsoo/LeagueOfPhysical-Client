using System;

namespace LOP
{
    /// <summary>로드아웃 한 슬롯(로비 GET/PUT economy 공통).</summary>
    [Serializable]
    public class LoadoutSlotDto
    {
        public int slotId;
        //  장착한 보유 인스턴스. 기본값으로 대체한 슬롯에서 기본 인스턴스가 없으면 null.
        public string userCosmeticId;
        public int cosmeticId;
    }
}
