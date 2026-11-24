using UnityEngine;

namespace LOP
{
    /// <summary>삼색 그랩 색. 카드 4색(글씨용 밝은색·바탕용 진한색 — Shared 값)과 좌석 색 여덟.</summary>
    public static class TriGrabPalette
    {
        private static readonly string[] Names = { "빨강", "노랑", "파랑", "초록" };
        private static readonly string[] SeatHex = { "#FF9F1C", "#C77DFF", "#4CC9F0", "#F15BB5", "#90BE6D", "#E9C46A", "#FF6B6B", "#F8F9FA" };

        public static readonly Color Correct = Parse("#3ddc84");
        public static readonly Color Wrong = Parse("#ff4d6d");
        public static readonly Color Miss = Parse("#a59cc4");
        public static readonly Color MoleLine = Parse("#ffb3c1");
        public static readonly Color Star = Parse("#ffd23f");

        public static string ColorName(int color) => Names[Mod(color, Names.Length)];

        public static string InkHtml(int color) => LOP.TriGrab.TriGrabColors.InkHex[Mod(color, LOP.TriGrab.TriGrabColors.Count)];
        public static Color Ink(int color) => Parse(InkHtml(color));

        public static Color Fill(int color) => Parse(LOP.TriGrab.TriGrabColors.FillHex[Mod(color, LOP.TriGrab.TriGrabColors.Count)]);

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
