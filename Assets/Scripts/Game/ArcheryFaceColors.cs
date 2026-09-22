using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 과녁 띠의 색. <b>세계양궁연맹 규격</b> — 안쪽부터 금·빨강·파랑·검정·흰색이고
    /// 각 색이 <b>링 두 개씩</b>을 덮는다(10·9점이 금, 8·7점이 빨강 …).
    ///
    /// <para><b>띠 번호가 아니라 비율로 고른다.</b> 번호로 고르면 띠 개수가 바뀔 때마다 색이
    /// 어긋난다 — 실제로 띠가 3개일 땐 번호 0·1·2가 금·빨강·파랑으로 맞았지만, 규정대로
    /// 10개로 늘리자 <c>번호 % 5</c>가 되감겨 6번째 띠가 다시 금색이 됐다. 비율은 띠를
    /// 몇 개로 쪼개든 같은 그림을 준다.</para>
    ///
    /// <para>3D 과녁(<see cref="ArcheryTargetView"/>)과 착탄 기록판(<c>ArcheryPadView</c>)이
    /// <b>같은 함수</b>를 쓴다 — 두 곳이 다른 색을 칠하면 기록판이 거짓말을 한다.</para>
    /// </summary>
    public static class ArcheryFaceColors
    {
        public static readonly Color Gold = new Color(1f, 0.85f, 0.1f);
        public static readonly Color Red = new Color(0.9f, 0.15f, 0.15f);
        public static readonly Color Blue = new Color(0.15f, 0.35f, 0.9f);
        public static readonly Color Black = new Color(0.1f, 0.1f, 0.1f);
        public static readonly Color White = Color.white;

        /// <summary>그 띠의 바깥 비율(0~1)에 해당하는 색.</summary>
        public static Color Of(float outerRatio)
        {
            if (outerRatio <= 0.2f) { return Gold; }
            if (outerRatio <= 0.4f) { return Red; }
            if (outerRatio <= 0.6f) { return Blue; }
            if (outerRatio <= 0.8f) { return Black; }
            return White;
        }
    }
}
