using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    public sealed class RecordStatus
    {
        [JsonProperty("recording")] public bool Recording;
        [JsonProperty("name")] public string Name;
        [JsonProperty("seconds")] public double Seconds;
        [JsonProperty("events")] public int Events;
        [JsonProperty("path")] public string Path;
        [JsonProperty("steps")] public int Steps;
        /// <summary>왜 닫혔나(명령·메뉴·재생 꺼짐·다시 불러오기).</summary>
        [JsonProperty("reason")] public string Reason;
        [JsonProperty("warnings")] public List<string> Warnings = new List<string>();
        /// <summary>만든 파일을 지금 단계 목록으로 다시 읽은 오류 — 있으면 사람이 고친다.</summary>
        [JsonProperty("errors")] public List<string> Errors = new List<string>();
    }

    /// <summary>
    /// 녹화 하나를 맡는 자리(spec §14). 재생이 꺼지거나 스크립트가 다시 불러와지면 그 자리까지 파일로 닫는다 — 이어 녹화하지 않는다
    /// (재생 진입 때 도메인이 다시 불러와지므로 녹화는 재생 중에만 시작한다).
    /// </summary>
    [InitializeOnLoad]
    public static class MacroRecorderHost
    {
        private static MacroRecorder _active;
        private static RecordStatus _last;

        static MacroRecorderHost()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingPlayMode && _active != null) Stop("재생이 꺼져 녹화를 닫았다");
            };
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (_active != null) Stop("스크립트를 다시 불러와 녹화를 닫았다");
            };
        }

        public static bool IsRecording => _active != null;

        public static RecordStatus Start(string name, double maxWaitSeconds, bool overwrite, bool allInput)
        {
            if (_active != null) throw new InvalidOperationException($"이미 녹화 중이다: {_active.Name} — automation_record_stop 뒤에");
            if (AutomationHost.Session.IsRunning) throw new InvalidOperationException("자동화 실행이 도는 중이다 — 끝나거나 멈춘 뒤에");
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("재생 중에만 녹화한다 — 재생을 켜고 시작할 화면(보통 로비)까지 간 뒤에");
            string bad = RecordNames.Validate(name);
            if (bad != null) throw new ArgumentException(bad);

            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), ScenarioFiles.Folder, name + ".json");
            if (File.Exists(path) && !overwrite) throw new InvalidOperationException($"이미 있다: {ScenarioFiles.Folder}/{name}.json — --overwrite true로 덮는다");

            _active = new MacroRecorder(name, path, maxWaitSeconds > 0 ? maxWaitSeconds : new MacroOptions().MaxWaitSeconds, allInput);
            Debug.Log($"[Automation] 녹화 시작: {name} — Game 뷰에서 손으로. F9 스크린샷, F10 체크포인트");
            return Status();
        }

        public static RecordStatus Stop(string reason)
        {
            if (_active == null) return _last ?? throw new InvalidOperationException("녹화 중이 아니다");
            var recorder = _active;
            _active = null;

            var events = recorder.Finish();
            var macro = MacroCompiler.Compile(events, new MacroOptions { MaxWaitSeconds = recorder.MaxWaitSeconds });
            var scenario = MacroScenario.Build(recorder.Name, recorder.StartState, recorder.StartScene, recorder.Environment, recorder.AutoFlap, macro, DateTime.Now);
            string json = MacroScenario.ToJson(scenario);

            //  번역기가 만든 것이 지금 단계 목록으로 읽히는지 — 안 읽혀도 파일은 쓴다(사람이 고친다).
            var errors = new List<string>();
            ScenarioParser.Parse(json, StepCatalog.CreateAll(), errors);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(recorder.OutputPath));
            File.WriteAllText(recorder.OutputPath, json);

            var warnings = new List<string>(recorder.Warnings);
            warnings.AddRange(macro.Warnings);
            _last = new RecordStatus
            {
                Recording = false,
                Name = recorder.Name,
                Seconds = Math.Round(recorder.Seconds, 1),
                Events = events.Count,
                Path = recorder.OutputPath,
                Steps = macro.Steps.Count,
                Reason = reason,
                Warnings = warnings,
                Errors = errors,
            };
            Debug.Log($"[Automation] 녹화 끝({reason}): {recorder.OutputPath} — 단계 {macro.Steps.Count}, 경고 {warnings.Count}, 오류 {errors.Count}");
            return _last;
        }

        public static RecordStatus Status()
        {
            if (_active == null) return _last ?? new RecordStatus { Recording = false };
            return new RecordStatus
            {
                Recording = true,
                Name = _active.Name,
                Seconds = Math.Round(_active.Seconds, 1),
                Events = _active.EventCount,
                Path = _active.OutputPath,
                Warnings = new List<string>(_active.Warnings),
            };
        }
    }
}
