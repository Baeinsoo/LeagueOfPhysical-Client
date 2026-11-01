using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 지금 화면을 기준 그림과 비교한다(spec §15, monkeyrunner sameAs). settle_ms 뒤 첫 캡처, 다르면 retry_s 동안 0.5초마다 다시 찍고,
    /// 끝까지 다르면 가장 덜 달랐던 순간의 지금 그림·차이 그림을 남기고 실패한다. 기준 만들기 실행이면 첫 캡처를 기준으로 쓴다.
    /// 단계가 스스로 마감을 지킨다 — 실행기의 시간 제한(그보다 10초 길다)으로 끝나면 기록을 못 남긴다.
    /// </summary>
    public sealed class SnapshotCompareStep : IStepHandler
    {
        public const int DefaultWidth = 1280;
        public const int DefaultHeight = 720;
        public const int DefaultTolerance = 16;
        public const double DefaultMaxDiffPercent = 1.0;
        private const double IntervalSeconds = 0.5;

        public string Op => "snapshot_compare";

        private static double Settle(ScenarioStep step) => (step.Int("settle_ms") ?? 500) / 1000.0;
        private static double Retry(ScenarioStep step) => step.Num("retry_s") ?? 3;

        public double DefaultTimeoutSeconds(ScenarioStep step) => Settle(step) + Retry(step) + 10;

        public void Validate(ScenarioStep step, List<string> errors)
        {
            if (!BaselineFiles.IsValidName(step.Str("baseline"), out var why)) errors.Add($"{step}: {why}");
            int tolerance = step.Int("tolerance") ?? DefaultTolerance;
            if (tolerance < 0 || tolerance > 255) errors.Add($"{step}: tolerance는 0..255 정수");
            double max = step.Num("max_diff_percent") ?? DefaultMaxDiffPercent;
            if (max < 0 || max > 100) errors.Add($"{step}: max_diff_percent는 0..100");
            if (step.Has("retry_s") && !(step.Num("retry_s") >= 0)) errors.Add($"{step}: retry_s는 0 이상의 수");
            if (step.Has("settle_ms") && !(step.Int("settle_ms") >= 0)) errors.Add($"{step}: settle_ms는 0 이상의 정수");
            if (step.Has("region") && !NormalizedRect.TryParse(step.Args["region"], out _, out var regionWhy)) errors.Add($"{step}: region {regionWhy}");
            if (step.Has("ignore"))
            {
                if (step.Args["ignore"] is JArray list)
                {
                    foreach (var item in list)
                    {
                        if (!NormalizedRect.TryParse(item, out _, out var ignoreWhy)) errors.Add($"{step}: ignore {ignoreWhy}");
                    }
                }
                else
                {
                    errors.Add($"{step}: ignore는 구역 목록 [[x, y, w, h], …]");
                }
            }
        }

        public StepResult Tick(StepContext ctx)
        {
            var step = ctx.Step;
            if (!ctx.Driver.IsPlaying) return StepResult.Fail("재생 중이 아니다 — 화면 비교는 재생 중에만(합성 화면은 재생 중에만 있다)");
            string name = step.Str("baseline");
            if (!BaselineFiles.TryResolve(ctx.Driver.BaselineRoot, name, out var baselinePath, out var why)) return StepResult.Fail(why);

            double settle = Settle(step);
            if (ctx.ElapsedSeconds < settle)
            {
                ctx.Waiting = "화면이 가라앉기를 기다림";
                return StepResult.Running;
            }
            var lastAt = (double?)ctx.Scratch["last_at"];
            if (lastAt != null && ctx.ElapsedSeconds - lastAt.Value < IntervalSeconds)
            {
                ctx.Waiting = "다시 찍기를 기다림";
                return StepResult.Running;
            }
            ctx.Scratch["last_at"] = ctx.ElapsedSeconds;

            if (!ctx.Driver.CapturePng(step.Int("width") ?? DefaultWidth, step.Int("height") ?? DefaultHeight, out var png, out why)) return StepResult.Fail(why);

            if (ctx.BaselineMode)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(baselinePath));
                File.WriteAllBytes(baselinePath, png);
                ctx.Recorder.AddSnapshot(new SnapshotRecord { Where = step.Where, Baseline = name, Passed = true, Updated = true });
                return StepResult.Done($"기준을 새로 썼다: {BaselineFiles.Folder}/{name}.png");
            }

            if (!File.Exists(baselinePath))
                return StepResult.Fail($"기준 그림이 없다: {BaselineFiles.Folder}/{name}.png — automation_baseline --scenario <이 시나리오>로 만든다");
            if (!PngPixels.TryDecode(File.ReadAllBytes(baselinePath), out int baseWidth, out int baseHeight, out var expected))
                return StepResult.Fail($"기준 그림을 못 읽는다: {BaselineFiles.Folder}/{name}.png");
            if (!PngPixels.TryDecode(png, out int width, out int height, out var actual)) return StepResult.Fail("지금 그림을 못 읽는다");
            //  늘여서 비교하지 않는다 — 비율이 다르면 모든 것이 어긋나 "조금 다름"으로 숨는다.
            if (baseWidth != width || baseHeight != height)
                return StepResult.Fail($"크기가 다르다 — 기준 {baseWidth}×{baseHeight}, 지금 {width}×{height}(width·height를 기준과 같게)");

            int tolerance = step.Int("tolerance") ?? DefaultTolerance;
            double max = step.Num("max_diff_percent") ?? DefaultMaxDiffPercent;
            var diffPixels = new Color32[actual.Length];
            var result = ImageDiff.Compare(expected, actual, width, height, tolerance, Region(step), Ignore(step), diffPixels);

            if (result.Percent <= max)
            {
                ctx.Recorder.AddSnapshot(new SnapshotRecord { Where = step.Where, Baseline = name, Percent = Math.Round(result.Percent, 3), MaxPercent = max, Passed = true });
                return StepResult.Done($"차이 {result.Percent:0.###}% ≤ {max}%");
            }

            //  가장 덜 다른 순간을 남긴다 — 실패한 뒤 사람이 볼 것은 "가장 가까웠던 화면"이다.
            var best = (double?)ctx.Scratch["best"];
            if (best == null || result.Percent < best.Value)
            {
                string label = name.Replace('/', '-');
                var (actualRelative, actualAbsolute) = ctx.Recorder.NextShotPath(label + "-actual");
                var (diffRelative, diffAbsolute) = ctx.Recorder.NextShotPath(label + "-diff");
                Directory.CreateDirectory(Path.GetDirectoryName(actualAbsolute));
                File.WriteAllBytes(actualAbsolute, png);
                File.WriteAllBytes(diffAbsolute, PngPixels.Encode(width, height, diffPixels));
                ctx.Scratch["best"] = result.Percent;
                ctx.Scratch["actual"] = actualRelative;
                ctx.Scratch["diff"] = diffRelative;
            }

            if (ctx.ElapsedSeconds - settle >= Retry(step))
            {
                double bestPercent = (double)ctx.Scratch["best"];
                string actualPath = (string)ctx.Scratch["actual"];
                string diffPath = (string)ctx.Scratch["diff"];
                ctx.Recorder.AddSnapshot(new SnapshotRecord
                {
                    Where = step.Where, Baseline = name, Percent = Math.Round(bestPercent, 3), MaxPercent = max, Passed = false, Actual = actualPath, Diff = diffPath,
                });
                ctx.Recorder.AddScreenshot(actualPath);
                ctx.Recorder.AddScreenshot(diffPath);
                return StepResult.Fail($"화면이 기준과 다르다 — 가장 낮은 차이 {bestPercent:0.###}% > {max}%(허용 {tolerance}) · {actualPath} · {diffPath}");
            }

            ctx.Waiting = $"차이 {result.Percent:0.###}% > {max}% — 다시 찍는다";
            return StepResult.Running;
        }

        private static Rect? Region(ScenarioStep step) =>
            step.Has("region") && NormalizedRect.TryParse(step.Args["region"], out var rect, out _) ? rect : (Rect?)null;

        private static List<Rect> Ignore(ScenarioStep step)
        {
            var rects = new List<Rect>();
            if (step.Args["ignore"] is JArray list)
            {
                foreach (var item in list)
                {
                    if (NormalizedRect.TryParse(item, out var rect, out _)) rects.Add(rect);
                }
            }
            return rects;
        }
    }
}
