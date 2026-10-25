using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 디버그 HUD를 보일지 기억하는 자리. <b>기본은 숨김</b>(2026-10-25 레이스 포장 spec §1) — 출시 화면에 넷코드 숫자가
    /// 떠 있으면 안 된다. 토글(F1·왼쪽 위 손잡이)은 그대로라 개발자는 한 번 켜 두면 계속 보인다.
    /// <para>옛 키 <c>lop.debugHud.hidden</c>은 쓰지 않는다. 그 키는 창을 열 때마다 현재 값(기본 0 = 보임)이 적혀 와서,
    /// 판을 한 번이라도 본 기기엔 전부 "보임"이 저장돼 있다 — 기본값만 바꿔선 사용자 폰에서 안 숨는다.</para>
    /// <para>손잡이(와 디버그 HUD 전체)는 에디터·Development 빌드에서만 있다(사용자 결정 2026-10-25) — 릴리스 화면엔
    /// "디버그" 글자 하나도 남으면 안 된다(intent 성공 판정 6).</para>
    /// </summary>
    public static class DebugHudPrefs
    {
        public const string ShownKey = "lop.debugHud.shown";

        public static bool LoadHidden() => PlayerPrefs.GetInt(ShownKey, 0) == 0;

        public static void SaveHidden(bool hidden) => PlayerPrefs.SetInt(ShownKey, hidden ? 0 : 1);

        /// <summary>손잡이를 둘지. 부르는 쪽이 <c>Application.isEditor</c>·<c>Debug.isDebugBuild</c>를 넘긴다(시험에서 둘 다 바꿔 보려고 인자로 받는다).</summary>
        public static bool HandleVisible(bool isEditor, bool isDebugBuild) => isEditor || isDebugBuild;
    }
}
