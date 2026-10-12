namespace LOP.UI
{
    /// <summary>
    /// 로비 배치 고르기. 패널은 폭을 1920에 맞추므로 기기 비율에 따라 높이만 바뀐다(20:9 폰 ≈ 864).
    /// 16:9 배치는 높이 1080에서 오른쪽 줄 아래(594)와 PLAY 묶음 위(1080 − 476) 사이 여유가 10뿐이라,
    /// 그보다 낮으면 버튼을 줄이고 전환 칩·종목 칩을 PLAY 왼쪽으로 옮긴 낮은 배치를 쓴다.
    /// </summary>
    public static class LobbyLayout
    {
        public const string ShortClass = "short";
        private const float FullHeight = 1070f;

        public static bool IsShort(float panelHeight) => panelHeight > 0f && panelHeight < FullHeight;
    }
}
