using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOP.EditorTools.Automation;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>snapshot_compare(spec §15) — 가라앉기·통과·다시 찍기·가장 가까운 실패 그림·기준 없음·크기·기준 만들기.</summary>
    public class SnapshotCompareStepTests
    {
        private static readonly Color32 Gray = new Color32(100, 100, 100, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private StepTestKit _kit;

        [SetUp]
        public void SetUp()
        {
            _kit = new StepTestKit();
            _kit.Driver.IsPlaying = true;
            _kit.State.StepIndex = 7;
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        private string BaselinePath(string name) => Path.Combine(_kit.Driver.BaselineRoot, name + ".png");

        private void WriteBaseline(string name, byte[] png)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath(name)));
            File.WriteAllBytes(BaselinePath(name), png);
        }

        private StepResult Tick(string args, double elapsed, bool baselineMode = false)
        {
            var ctx = _kit.Ctx("snapshot_compare", args, elapsed);
            ctx.BaselineMode = baselineMode;
            return new SnapshotCompareStep().Tick(ctx);
        }

        private static byte[] GrayWithRed(int count) =>
            TestPng.Png(4, 4, Gray, Enumerable.Range(0, count).Select(i => (i, 0, Red)).ToArray());

        [Test]
        public void 기준_이름과_허용치와_구역을_검사한다()
        {
            var errors = new List<string>();
            var step = new SnapshotCompareStep();

            step.Validate(StepTestKit.Step("snapshot_compare",
                "{\"baseline\":\"lobby/home\",\"tolerance\":0,\"max_diff_percent\":2.5,\"region\":[0,0,1,1],\"ignore\":[[0.8,0,0.2,0.1]],\"retry_s\":0}"), errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));

            step.Validate(StepTestKit.Step("snapshot_compare", "{}"), errors);
            step.Validate(StepTestKit.Step("snapshot_compare", "{\"baseline\":\"../x\"}"), errors);
            step.Validate(StepTestKit.Step("snapshot_compare", "{\"baseline\":\"a\",\"tolerance\":300}"), errors);
            step.Validate(StepTestKit.Step("snapshot_compare", "{\"baseline\":\"a\",\"max_diff_percent\":-1}"), errors);
            step.Validate(StepTestKit.Step("snapshot_compare", "{\"baseline\":\"a\",\"region\":[0,0,2,1]}"), errors);
            step.Validate(StepTestKit.Step("snapshot_compare", "{\"baseline\":\"a\",\"ignore\":\"x\"}"), errors);
            Assert.AreEqual(6, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void 가라앉기_전엔_찍지_않는다()
        {
            WriteBaseline("a", GrayWithRed(0));
            _kit.Driver.Captures.Enqueue(GrayWithRed(0));

            Assert.AreEqual(StepStatus.Running, Tick("{\"baseline\":\"a\"}", 0.2).Status);
            Assert.AreEqual(1, _kit.Driver.Captures.Count);
        }

        [Test]
        public void 같으면_통과하고_기록에_남긴다()
        {
            WriteBaseline("lobby/home", GrayWithRed(0));
            _kit.Driver.Captures.Enqueue(GrayWithRed(0));

            Assert.AreEqual(StepStatus.Done, Tick("{\"baseline\":\"lobby/home\"}", 0.5).Status);

            var snapshot = _kit.State.Report.Snapshots.Single();
            Assert.IsTrue(snapshot.Passed);
            Assert.AreEqual(0, snapshot.Percent);
            Assert.AreEqual(1, snapshot.Iteration);
            Assert.AreEqual("loop[0]", snapshot.Where);
        }

        [Test]
        public void 처음엔_다르다가_같아지면_통과한다()
        {
            WriteBaseline("a", GrayWithRed(0));
            _kit.Driver.Captures.Enqueue(GrayWithRed(4));
            _kit.Driver.Captures.Enqueue(GrayWithRed(0));

            Assert.AreEqual(StepStatus.Running, Tick("{\"baseline\":\"a\"}", 0.5).Status);
            Assert.AreEqual(StepStatus.Running, Tick("{\"baseline\":\"a\"}", 0.7).Status);
            Assert.AreEqual(1, _kit.Driver.Captures.Count, "0.5초 간격 안에는 다시 찍지 않는다");
            Assert.AreEqual(StepStatus.Done, Tick("{\"baseline\":\"a\"}", 1.0).Status);
        }

        [Test]
        public void 다시_찍어도_다르면_실패하고_가장_가까웠던_지금_그림과_차이_그림을_남긴다()
        {
            WriteBaseline("lobby/home", GrayWithRed(0));
            _kit.Driver.Captures.Enqueue(GrayWithRed(4));   // 25 %
            _kit.Driver.Captures.Enqueue(GrayWithRed(2));   // 12.5 % — 가장 가깝다
            _kit.Driver.Captures.Enqueue(GrayWithRed(4));
            const string args = "{\"baseline\":\"lobby/home\",\"retry_s\":1}";

            Assert.AreEqual(StepStatus.Running, Tick(args, 0.5).Status);
            Assert.AreEqual(StepStatus.Running, Tick(args, 1.0).Status);
            var result = Tick(args, 1.5);

            Assert.AreEqual(StepStatus.Failed, result.Status);
            StringAssert.Contains("12.5", result.Detail);
            var snapshot = _kit.State.Report.Snapshots.Single();
            Assert.IsFalse(snapshot.Passed);
            Assert.AreEqual(12.5, snapshot.Percent);
            Assert.AreEqual("shots/01-07-lobby-home-actual.png", snapshot.Actual);
            Assert.AreEqual("shots/01-07-lobby-home-diff.png", snapshot.Diff);
            Assert.IsTrue(PngPixels.TryDecode(File.ReadAllBytes(Path.Combine(_kit.State.RunDir, snapshot.Actual)), out _, out _, out var kept));
            Assert.AreEqual(2, kept.Count(p => p.r == 255 && p.g == 0));
            Assert.IsTrue(File.Exists(Path.Combine(_kit.State.RunDir, snapshot.Diff)));
            CollectionAssert.AreEqual(new[] { snapshot.Actual, snapshot.Diff }, _kit.Iteration.Screenshots);
        }

        [Test]
        public void 기준이_없으면_실패하고_만드는_법을_알린다()
        {
            _kit.Driver.Captures.Enqueue(GrayWithRed(0));

            var result = Tick("{\"baseline\":\"none\"}", 0.5);

            Assert.AreEqual(StepStatus.Failed, result.Status);
            StringAssert.Contains("automation_baseline", result.Detail);
        }

        [Test]
        public void 크기가_다르면_비교하지_않고_실패한다()
        {
            WriteBaseline("a", GrayWithRed(0));
            _kit.Driver.Captures.Enqueue(TestPng.Png(8, 4, Gray));

            var result = Tick("{\"baseline\":\"a\"}", 0.5);

            Assert.AreEqual(StepStatus.Failed, result.Status);
            StringAssert.Contains("크기", result.Detail);
        }

        [Test]
        public void 기준_만들기_모드면_지금_그림을_기준으로_쓴다()
        {
            var png = GrayWithRed(3);
            _kit.Driver.Captures.Enqueue(png);

            Assert.AreEqual(StepStatus.Done, Tick("{\"baseline\":\"new/one\"}", 0.5, baselineMode: true).Status);

            CollectionAssert.AreEqual(png, File.ReadAllBytes(BaselinePath("new/one")));
            Assert.IsTrue(_kit.State.Report.Snapshots.Single().Updated);
        }

        [Test]
        public void 재생_중이_아니면_실패한다()
        {
            _kit.Driver.IsPlaying = false;

            Assert.AreEqual(StepStatus.Failed, Tick("{\"baseline\":\"a\"}", 0.5).Status);
        }

        [Test]
        public void 비교할_픽셀이_없으면_조용히_통과하지_않고_실패한다()
        {
            //  ignore가 전체를 덮으면 Compared가 0이라 Percent도 0 — 고치기 전엔 그대로 통과했다(Review L1).
            WriteBaseline("a", GrayWithRed(0));
            _kit.Driver.Captures.Enqueue(GrayWithRed(4));

            var result = Tick("{\"baseline\":\"a\",\"ignore\":[[0,0,1,1]]}", 0.5);

            Assert.AreEqual(StepStatus.Failed, result.Status);
            StringAssert.Contains("비교할 픽셀", result.Detail);
        }
    }
}
