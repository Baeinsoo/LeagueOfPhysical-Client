namespace LOP.UI
{
    /// <summary>투석기 HUD 글. 숫자는 ASCII 빼기(-)를 쓴다 — 테마 글꼴(Jua)에 수학 빼기 글자가 없을 수 있다.</summary>
    public static class ArcheryCatapultHudText
    {
        public static string RoundLabel(int round, int count) => $"라운드 {round + 1}/{count}";
        public static string SeatText(int slot) => $"내 자리 {slot + 1}번";
        public static string PopupText(string name, int points) => points < 0 ? $"{name} 앗! {points}" : $"{name} +{points}";
        public static string GainText(int gain) => gain > 0 ? "+" + gain : gain.ToString();
    }
}
