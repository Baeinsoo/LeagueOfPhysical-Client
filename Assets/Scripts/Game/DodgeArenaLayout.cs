using UnityEngine;

namespace LOP
{
    /// <summary>체육관 장식 배치(그림만). 벽 안쪽 ±9, 벽 바깥 ±10 — DodgeMap 충돌체와 같은 값이다.</summary>
    public static class DodgeArenaLayout
    {
        public const float LineWidth = 0.08f;
        public const float LineInset = 0.4f;
        public const float CenterCircleRadius = 1.8f;
        public const float OuterFloorSize = 80f;
        private const float LineY = 0.005f;
        private const float LineThick = 0.004f;

        public static (Vector3 center, Vector3 size)[] CourtLines()
        {
            float e = 9f - LineInset;
            return new[]
            {
                (new Vector3(0f, LineY, e), new Vector3(e * 2f, LineThick, LineWidth)),
                (new Vector3(0f, LineY, -e), new Vector3(e * 2f, LineThick, LineWidth)),
                (new Vector3(e, LineY, 0f), new Vector3(LineWidth, LineThick, e * 2f)),
                (new Vector3(-e, LineY, 0f), new Vector3(LineWidth, LineThick, e * 2f)),
                (new Vector3(0f, LineY, 0f), new Vector3(e * 2f, LineThick, LineWidth)),   // 가운데 선
            };
        }

        /// <summary>북쪽 벽 밖 낮은 관중석 세 단 — 화면 위쪽에 보인다.</summary>
        public static (Vector3 center, Vector3 size)[] Bleachers() => new[]
        {
            (new Vector3(0f, 0.2f, 10.8f), new Vector3(22f, 0.4f, 1.2f)),
            (new Vector3(0f, 0.4f, 12.0f), new Vector3(22f, 0.8f, 1.2f)),
            (new Vector3(0f, 0.6f, 13.2f), new Vector3(22f, 1.2f, 1.2f)),
        };
    }
}
