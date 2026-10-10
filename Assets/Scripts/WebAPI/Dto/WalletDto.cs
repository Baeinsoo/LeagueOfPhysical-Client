using System;

namespace LOP
{
    /// <summary>통화 하나의 잔액(로비 GET/PUT economy 공통).</summary>
    [Serializable]
    public class WalletDto
    {
        public int currencyId;
        public long balance;
    }
}
