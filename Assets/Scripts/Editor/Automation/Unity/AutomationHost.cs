using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 자동화 세션을 에디터에 붙인다: 에디터 틱마다 실행기를 돌리고, 도메인이 다시 불러와지면 이어 돈다.
    /// 이 프로젝트는 재생 진입 때도 도메인을 다시 불러온다(Enter Play Mode 옵션 꺼짐) — 그래서 상태는 파일에, "도는 실행"은 SessionState에.
    /// </summary>
    [InitializeOnLoad]
    public static class AutomationHost
    {
        public const string RunsRoot = "Logs/automation";
        //  틱마다 씬의 스코프·UI 문서를 찾으므로 초당 10번이면 충분하다.
        private const double TickIntervalSeconds = 0.1;
        //  저장 실패(디스크 가득·잠금)가 계속되는 동안 더 못 기다리게 늘어나는 상한(L6).
        private const double MaxTickRetryDelaySeconds = 5.0;
        private static double _lastTick;
        private static double _tickRetryDelaySeconds = TickIntervalSeconds;
        private static bool _tickFailureLogged;

        public static readonly AutomationSession Session =
            new AutomationSession(new EditorAutomationDriver(), RunsRoot, new SessionStateRunMemory(), () => StepCatalog.CreateAll());

        static AutomationHost()
        {
            EditorApplication.update += Update;
            UnityEngine.Application.logMessageReceived += OnLog;
            AssemblyReloadEvents.beforeAssemblyReload += Session.Flush;
            //  이어 돌 실행을 붙이고, 그 사이(또는 명령이 delayCall보다 먼저 와 EnsureResumed가 대신 붙인 뒤) 쌓인
            //  끊긴 실행 폴더를 전부 interrupted로 닫는다(M1) — 하나만 보던 옛 Status()와 다르다.
            EditorApplication.delayCall += () => Session.ResumeAndSweep(DateTime.UtcNow);
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup - _lastTick < _tickRetryDelaySeconds) return;
            _lastTick = EditorApplication.timeSinceStartup;
            try
            {
                Session.Tick(DateTime.UtcNow);
                _tickRetryDelaySeconds = TickIntervalSeconds;
                _tickFailureLogged = false;
            }
            catch (IOException e)
            {
                //  state.json·run.log 쓰기(Save·Recorder.Log)가 디스크 가득·잠금으로 던지면 — 단계 예외와 달리
                //  runner가 못 잡는다(L6). 매 틱 콘솔을 채우지 않게 한 번만 적고, 다시 쓰기 간격을 늘린다.
                if (!_tickFailureLogged)
                {
                    Debug.LogError($"[Automation] 상태 저장 실패(디스크 가득·잠금?) — 다시 쓰기를 늦춘다: {e.Message}");
                    _tickFailureLogged = true;
                }
                _tickRetryDelaySeconds = Math.Min(_tickRetryDelaySeconds * 2, MaxTickRetryDelaySeconds);
            }
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string firstFrame = stackTrace?.Split('\n')[0];
            Session.ConsoleError(string.IsNullOrEmpty(firstFrame) ? message : $"{message} @ {firstFrame}");
        }

        private sealed class SessionStateRunMemory : IRunMemory
        {
            private const string Key = "LOP.Automation.ActiveRunDir";

            public string ActiveRunDir
            {
                get
                {
                    string value = SessionState.GetString(Key, string.Empty);
                    return value.Length == 0 ? null : value;
                }
                set
                {
                    if (string.IsNullOrEmpty(value)) SessionState.EraseString(Key);
                    else SessionState.SetString(Key, value);
                }
            }
        }
    }
}
