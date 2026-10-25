using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화면 설정을 기기에 기억하는 자리. "상대 이름 보기"(플래피 이름표)와 "흔들림 끄기"(카메라 흔들림 전부).
    /// <para>설정 셸(<see cref="LOP.UI.SettingsView"/>)이 쓰고 경기 스코프가 지어질 때 한 번 읽는다 — 설정은 로비에서만 열리므로
    /// 바꾸면 다음 판부터 적용된다.</para>
    /// </summary>
    public static class FlappyDisplayPrefs
    {
        public const string ShowOpponentNamesKey = "lop.flappy.showOpponentNames";

        //  흔들림은 플래피만의 것이 아니다(멀미·취향) — 키 이름을 종목에 묶지 않는다.
        public const string ShakeOffKey = "lop.display.shakeOff";

        public static bool ShowOpponentNames => PlayerPrefs.GetInt(ShowOpponentNamesKey, 0) != 0;

        public static void SetShowOpponentNames(bool show) => PlayerPrefs.SetInt(ShowOpponentNamesKey, show ? 1 : 0);

        /// <summary>참이면 카메라 흔들림(충돌·대시)을 전부 끈다. 기본은 흔들림 켬.</summary>
        public static bool ShakeOff => PlayerPrefs.GetInt(ShakeOffKey, 0) != 0;

        public static void SetShakeOff(bool off) => PlayerPrefs.SetInt(ShakeOffKey, off ? 1 : 0);
    }
}
