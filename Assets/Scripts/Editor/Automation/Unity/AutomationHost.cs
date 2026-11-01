using System;
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
        private static double _lastTick;

        public static readonly AutomationSession Session =
            new AutomationSession(new EditorAutomationDriver(), RunsRoot, new SessionStateRunMemory(), () => StepCatalog.CreateAll());

        static AutomationHost()
        {
            EditorApplication.update += Update;
            UnityEngine.Application.logMessageReceived += OnLog;
            AssemblyReloadEvents.beforeAssemblyReload += Session.Flush;
            EditorApplication.delayCall += Session.Resume;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup - _lastTick < TickIntervalSeconds) return;
            _lastTick = EditorApplication.timeSinceStartup;
            Session.Tick(DateTime.UtcNow);
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
