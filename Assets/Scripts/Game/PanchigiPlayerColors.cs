using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 플레이어 색 — 참가 순서로 정한다. 색을 정하는 곳은 여기 하나다: 스킨에 색이 생기면 이것만 바꾼다.
    /// </summary>
    public static class PanchigiPlayerColors
    {
        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.25f, 0.25f),   // 빨강
            new Color(0.25f, 0.50f, 0.95f),   // 파랑
            new Color(0.30f, 0.80f, 0.40f),   // 초록
            new Color(0.95f, 0.80f, 0.20f),   // 노랑
        };

        public static Color For(int playerIndex)
        {
            return playerIndex < 0 ? Color.white : Palette[playerIndex % Palette.Length];
        }
    }
}
