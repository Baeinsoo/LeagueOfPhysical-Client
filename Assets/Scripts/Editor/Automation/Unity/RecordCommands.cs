using System;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;

namespace LOP.EditorTools.Automation
{
    /// <summary>녹화 명령(spec §7.1·§14)과 같은 일을 하는 메뉴. 모두 바로 돌아온다 — 녹화는 에디터 틱·Input System 엿보기가 한다.</summary>
    public static class RecordCommands
    {
        private const string Tag = "lop/automation";

        [CliCommand("automation_record_start", "재생 중 사람의 동작을 시나리오로 녹화하기 시작한다(Game 뷰에서 손으로; F9 스크린샷, F10 체크포인트). 멈춤은 automation_record_stop.", Tags = new[] { Tag })]
        public static RecordStatus Start(
            [CliArg("name", "시나리오 이름 — Automation/Scenarios/<name>.json이 된다(영소문자·숫자·-·_)", Required = true)] string name,
            [CliArg("max_wait", "좌표·키·스크린샷 앞 기다림 상한(초). 0이면 5")] int maxWait = 0,
            [CliArg("overwrite", "같은 이름 파일을 덮는다")] bool overwrite = false,
            [CliArg("all_input", "녹화 동안 Game 뷰 초점 없이 들어온 입력도 게임이 받게(simulate_* 연기 확인용)")] bool allInput = false)
            => MacroRecorderHost.Start(name, maxWait, overwrite, allInput);

        [CliCommand("automation_record_stop", "녹화를 멈추고 시나리오 파일로 쓴다. 경로·단계 수·경고·검사 오류를 돌려준다.", Tags = new[] { Tag })]
        public static RecordStatus Stop() => MacroRecorderHost.Stop("automation_record_stop");

        [CliCommand("automation_record_status", "녹화 중인지, 아니면 마지막 녹화 결과", Tags = new[] { Tag })]
        public static RecordStatus Status() => MacroRecorderHost.Status();

        [MenuItem("LOP/Automation/녹화 시작", false, 300)]
        private static void MenuStart() => MacroRecorderHost.Start("rec-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), 0, false, false);

        [MenuItem("LOP/Automation/녹화 시작", true)]
        private static bool CanMenuStart() => EditorApplication.isPlaying && !MacroRecorderHost.IsRecording && !AutomationHost.Session.IsRunning;

        [MenuItem("LOP/Automation/녹화 멈춤", false, 301)]
        private static void MenuStop()
        {
            var status = MacroRecorderHost.Stop("메뉴");
            if (File.Exists(status.Path)) EditorUtility.RevealInFinder(status.Path);
        }

        [MenuItem("LOP/Automation/녹화 멈춤", true)]
        private static bool CanMenuStop() => MacroRecorderHost.IsRecording;
    }
}
