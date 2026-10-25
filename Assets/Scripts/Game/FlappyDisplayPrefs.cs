using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 플래피 화면 설정을 기기에 기억하는 자리. 지금은 "상대 이름 보기" 하나.
    /// <para>설정 셸의 토글 UI는 조각 2("흔들림 끄기"를 첫 항목으로 넣을 때)에 같이 단다 — 이 조각은 읽기만 한다.
    /// 경기 스코프를 지을 때 한 번 읽으므로 판 도중에 바꾸면 다음 판부터 적용된다.</para>
    /// </summary>
    public static class FlappyDisplayPrefs
    {
        public const string ShowOpponentNamesKey = "lop.flappy.showOpponentNames";

        public static bool ShowOpponentNames => PlayerPrefs.GetInt(ShowOpponentNamesKey, 0) != 0;

        public static void SetShowOpponentNames(bool show) => PlayerPrefs.SetInt(ShowOpponentNamesKey, show ? 1 : 0);
    }
}
