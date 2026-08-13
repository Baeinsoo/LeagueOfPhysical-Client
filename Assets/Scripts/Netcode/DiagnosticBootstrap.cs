using UnityEngine;

namespace LOP
{
    /// <summary>
    /// [진단용 임시] 실기기 프레임 예산 측정용 부트스트랩. 확인 후 이 파일째로 제거한다.
    ///
    /// 개발 빌드는 일반 로그에도 스택 트레이스를 붙이는데, 프레임마다 찍는 진단 로그에 25줄씩
    /// 딸려 오면 logcat 버퍼가 시작 구간을 밀어내 정작 볼 곳이 사라진다. 경고·오류의 스택은
    /// 그대로 두고 일반 로그만 끈다.
    /// </summary>
    public static class DiagnosticBootstrap
    {
        // 이 프로젝트엔 LOP.Application이 따로 있어 이 네임스페이스에서 Application은 그쪽으로 잡힌다.
        // 유니티 것을 쓰려면 풀네임이어야 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            UnityEngine.Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            // logcat에서 이번 실행이 어디서 시작하는지 찾는 표식.
            Debug.Log($"[DiagStart] {SystemInfo.deviceModel} / {SystemInfo.processorType}" +
                      $" / {SystemInfo.graphicsDeviceName}" +
                      $" / targetFrameRate={UnityEngine.Application.targetFrameRate}" +
                      $" vSync={QualitySettings.vSyncCount}");
        }
    }
}
