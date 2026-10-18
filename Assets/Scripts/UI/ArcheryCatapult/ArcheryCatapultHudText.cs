namespace LOP.UI
{
    /// <summary>"OO +3" 팝업의 꼴. 대박은 크게 오래, 종은 "땡~"을 하나 더.</summary>
    public enum ArcheryCatapultPopupStyle { Normal, Jackpot, Ding }

    /// <summary>투석기 HUD 글. 숫자는 ASCII 빼기(-)를 쓴다 — 테마 글꼴(Jua)에 수학 빼기 글자가 없을 수 있다.</summary>
    public static class ArcheryCatapultHudText
    {
        public static string RoundLabel(int round, int count) => $"라운드 {round + 1}/{count}";
        public static string SeatText(int slot) => $"내 자리 {slot + 1}번";
        public static string PopupText(string name, int points) => points < 0 ? $"{name} 앗! {points}" : $"{name} +{points}";
        public static string GainText(int gain) => gain > 0 ? "+" + gain : gain.ToString();

        public const string DingText = "땡~";

        public static int DurationMs(ArcheryCatapultPopupStyle style)
            => style == ArcheryCatapultPopupStyle.Jackpot ? 1600 : style == ArcheryCatapultPopupStyle.Ding ? 1300 : 1100;

        public static string ClassOf(ArcheryCatapultPopupStyle style)
            => style == ArcheryCatapultPopupStyle.Jackpot ? "catapult-popup--big"
             : style == ArcheryCatapultPopupStyle.Ding ? "catapult-popup--ding" : string.Empty;
    }
}
