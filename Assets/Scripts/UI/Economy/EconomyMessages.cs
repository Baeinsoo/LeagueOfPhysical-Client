namespace LOP.UI
{
    /// <summary>재화·꾸미기 요청(구매·로드아웃·외형) 거절 코드 → 안내 문구. 상점과 라커룸이 같은 표를 쓴다.
    /// 백엔드 responseCode.interface.ts의 Economy 영역과 짝이다.</summary>
    public static class EconomyMessages
    {
        public static string For(int code) => code switch
        {
            ResponseCode.INSUFFICIENT_FUNDS => "코인이 부족합니다",
            ResponseCode.COSMETIC_ALREADY_OWNED => "이미 가진 품목",
            ResponseCode.COSMETIC_NOT_PURCHASABLE => "살 수 없는 품목",
            ResponseCode.PRICE_MISMATCH => "가격이 바뀌었습니다. 앱을 업데이트해 주세요",
            ResponseCode.ACCOUNT_FROZEN => "계정이 일시 정지 상태",
            ResponseCode.ECONOMY_DISABLED => "지금은 상점을 쓸 수 없습니다",
            ResponseCode.WALLET_FULL => "지갑이 가득 찼습니다",
            ResponseCode.INVALID_APPEARANCE_COLOR => "고를 수 없는 색이에요",
            _ => "잠시 뒤 다시 시도",
        };
    }
}
