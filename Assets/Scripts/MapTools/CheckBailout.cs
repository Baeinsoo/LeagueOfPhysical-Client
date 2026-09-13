using System;

namespace LOP.MapTools
{
    /// <summary>
    /// 검사가 <b>더 못 하겠다</b>고 판단했을 때 그 사실을 어떻게 알릴지 고르는 규칙.
    ///
    /// <para>메뉴에서 사람이 눌렀을 때는 대화상자가 맞다 — 콘솔은 안 볼 수 있고, 검사가
    /// 왜 아무것도 안 했는지 그 자리에서 알려야 한다.</para>
    ///
    /// <para><b>그런데 사람이 없을 때 대화상자를 띄우면 잡이 실패로 끝나는 게 아니라
    /// 통째로 멎는다.</b> 대화상자는 눌러 줄 사람을 기다리며 에디터 메인 스레드를 잡고
    /// 있어서, 밖에서 보면 CPU도 낮고 모든 명령이 타임아웃한다 — <i>"20분짜리 검사가
    /// 도는 중"과 구별이 안 된다.</i> 실제로 이것 때문에 하루에 두 번 막혔고 한 번은
    /// 몇 시간을 잃었다. 그래서 사람이 없으면 <b>같은 문구를 에러로 남기고 그냥 돌아간다</b> —
    /// 잡은 실패로 끝나고, 이유는 로그에 남는다.</para>
    ///
    /// <para>대화상자·로그를 직접 부르지 않고 넘겨받는 이유는 하나다: 이 <b>분기 판단</b>을
    /// EditMode 테스트로 시험하기 위해서다(<c>EditorUtility.DisplayDialog</c>는 테스트에서
    /// 부를 수 없다).</para>
    /// </summary>
    public static class CheckBailout
    {
        /// <summary>
        /// <paramref name="humanWatching"/>이면 대화상자를, 아니면 <paramref name="logError"/>에
        /// <b>같은 문구</b>를 넘긴다. 어느 쪽이든 부르는 쪽은 곧바로 돌아가야 한다.
        /// </summary>
        public static void Report(bool humanWatching, string title, string message,
                                  Action<string, string> showDialog, Action<string> logError)
        {
            if (showDialog == null)
            {
                throw new ArgumentNullException(nameof(showDialog));
            }
            if (logError == null)
            {
                throw new ArgumentNullException(nameof(logError));
            }
            if (humanWatching)
            {
                showDialog(title, message);
                return;
            }
            logError(message);
        }
    }
}
