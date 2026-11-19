using UnityEngine;

namespace LOP
{
    /// <summary>삼색 그랩 색. 글자색 셋과 좌석 색 여덟(시안 v2 그대로).</summary>
    public static class TriGrabPalette
    {
        private static readonly string[] InkHex = { "#FF5252", "#FFC21A", "#4C8DFF" };
        private static readonly string[] SeatHex = { "#FF8A65", "#4DD0E1", "#AED581", "#BA68C8", "#F06292", "#FFD54F", "#7986CB", "#BCAAA4" };

        public static readonly Color Correct = new Color(0.36f, 1f, 0.54f);
        public static readonly Color Wrong = new Color(1f, 0.23f, 0.23f);

        public static string InkHtml(int color) => InkHex[Mod(color, InkHex.Length)];
        public static Color Ink(int color) => Parse(InkHtml(color));

        /// <summary>좌석(등록 순서) 색. 모르는 사람(-1)은 회색.</summary>
        public static Color Seat(int seat) => seat < 0 ? new Color(0.6f, 0.6f, 0.6f) : Parse(SeatHex[seat % SeatHex.Length]);
        public static string SeatHtml(int seat) => seat < 0 ? "#999999" : SeatHex[seat % SeatHex.Length];

        private static int Mod(int a, int n) => ((a % n) + n) % n;

        private static Color Parse(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
