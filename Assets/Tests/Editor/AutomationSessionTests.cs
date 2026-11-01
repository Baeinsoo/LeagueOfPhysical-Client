using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOP.EditorTools.Automation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>AutomationSession(spec §5·§7) — 시작·한 번에 하나·검사·이어 돌기·에디터 재시작 뒤 interrupted.</summary>
    public class AutomationSessionTests
    {
        private sealed class FakeMemory : IRunMemory
        {
            public string ActiveRunDir { get; set; }
        }

        private const string Never = "{\"name\":\"grind\",\"loop\":[{\"op\":\"never\"}]}";
        private string _root;
        private FakeAutomationDriver _driver;
        private FakeMemory _memory;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "lop-automation-tests", Guid.NewGuid().ToString("N"));
            _driver = new FakeAutomationDriver();
            _memory = new FakeMemory();
            _now = new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private IReadOnlyDictionary<string, IStepHandler> Handlers() => new IStepHandler[]
        {
            new ScriptedHandler("ok", _ => StepResult.Done()),
            new ScriptedHandler("never", _ => StepResult.Running, timeout: 1000),
            new ScriptedHandler("setpref", ctx =>
            {
                string key = ctx.Step.Str("key");
                bool had = ctx.Driver.HasPref(key);
                ctx.RememberRestore(RestoreKinds.BoolPref, key, had && ctx.Driver.GetBoolPref(key, false) ? "true" : "false", had);
                ctx.Driver.SetBoolPref(key, true);
                return StepResult.Done();
            }),
        }.ToDictionary(h => h.Op);

        private AutomationSession NewSession() => new AutomationSession(_driver, _root, _memory, Handlers);

        private void TickMany(AutomationSession session, int times)
        {
            for (int i = 0; i < times; i++)
            {
                session.Tick(_now);
                _now = _now.AddSeconds(1);
            }
        }

        [Test]
        public void 시작하면_실행_폴더와_파일을_만들고_도는_중으로_표시한다()
        {
            var status = NewSession().Start(Never, "a.json", 0, _now);

            Assert.AreEqual("running", status.Status);
            Assert.IsFalse(status.Done);
            StringAssert.EndsWith("-grind", status.Run);
            string dir = Path.Combine(_root, status.Run);
            foreach (var file in new[] { "scenario.json", "state.json", "report.json" })
            {
                Assert.IsTrue(File.Exists(Path.Combine(dir, file)), file);
            }
            Assert.IsTrue(Directory.Exists(Path.Combine(dir, "shots")));
            Assert.AreEqual(dir, _memory.ActiveRunDir);
        }

        [Test]
        public void 도는_중엔_두_번째_시작을_거절한다()
        {
            var session = NewSession();
            var first = session.Start(Never, "a.json", 0, _now);

            var e = Assert.Throws<InvalidOperationException>(() => session.Start(Never, "a.json", 0, _now));

            StringAssert.Contains(first.Run, e.Message);
        }

        [Test]
        public void 시나리오가_틀리면_오류를_모아_거절하고_폴더를_안_만든다()
        {
            var e = Assert.Throws<ArgumentException>(() => NewSession().Start("{\"name\":\"x\",\"loop\":[{\"op\":\"zzz\"}]}", "a.json", 0, _now));

            StringAssert.Contains("zzz", e.Message);
            Assert.IsFalse(Directory.Exists(_root) && Directory.GetDirectories(_root).Length > 0);
            Assert.IsNull(_memory.ActiveRunDir);
        }

        [Test]
        public void 에디터가_바쁘면_시작을_거절한다()
        {
            _driver.IsBusy = true;

            Assert.Throws<InvalidOperationException>(() => NewSession().Start(Never, "a.json", 0, _now));
        }

        [Test]
        public void repeat_인자가_시나리오_값보다_먼저다()
        {
            const string json = "{\"name\":\"g\",\"repeat\":3,\"loop\":[{\"op\":\"ok\"}]}";

            var first = NewSession();
            Assert.AreEqual(5, first.Start(json, "a.json", 5, _now).Repeat);
            //  두 번째 세션이 시작하기 전에 첫 실행을 끝내 둔다 — 안 그러면(의도대로) "이미 도는 실행" 거절에 걸린다.
            first.Stop();
            TickMany(first, 3);
            Assert.IsFalse(first.IsRunning);

            Assert.AreEqual(3, NewSession().Start(json, "a.json", 0, _now.AddSeconds(100)).Repeat);
        }

        [Test]
        public void Resume_전에_와도_명령이_이어_도는_실행을_본다()
        {
            // L1: delayCall(Resume)이 아직 안 돈 상태를 흉내 — Resume()을 부르지 않은 채 바로 명령을 보낸다.
            var before = NewSession();
            var started = before.Start(Never, "a.json", 0, _now);
            TickMany(before, 2);

            var after = NewSession();   //  Resume()을 부르지 않았다

            Assert.IsTrue(after.IsRunning);
            Assert.AreEqual(started.Run, after.Status(_now).Run);
            Assert.IsTrue(after.Stop());
        }

        [Test]
        public void 재시작_뒤_start는_끊긴_실행_여러_개를_모두_interrupted로_닫는다()
        {
            // M1: 옛 코드는 "가장 최근 폴더 하나"만 봤다 — 두 개를 쌓아서 전부 닫히는지 본다.
            _driver.Prefs["k1"] = false;
            _driver.Prefs["k2"] = false;

            var crash1 = NewSession();
            var run1 = crash1.Start("{\"name\":\"g1\",\"setup\":[{\"op\":\"setpref\",\"key\":\"k1\"}],\"loop\":[{\"op\":\"never\"}]}", "a.json", 0, _now);
            TickMany(crash1, 3);
            Assert.AreEqual(true, _driver.Prefs["k1"]);
            _memory.ActiveRunDir = null;   //  첫 번째 "재시작"

            var crash2 = NewSession();
            var run2 = crash2.Start("{\"name\":\"g2\",\"setup\":[{\"op\":\"setpref\",\"key\":\"k2\"}],\"loop\":[{\"op\":\"never\"}]}", "b.json", 0, _now.AddSeconds(5));
            TickMany(crash2, 3);
            Assert.AreEqual(true, _driver.Prefs["k2"]);
            _memory.ActiveRunDir = null;   //  두 번째 "재시작" — 이제 끊긴 실행 폴더가 두 개 쌓였다

            NewSession().Start("{\"name\":\"g3\",\"loop\":[{\"op\":\"ok\"}]}", "c.json", 0, _now.AddSeconds(10));

            foreach (var run in new[] { run1, run2 })
            {
                var report = JObject.Parse(File.ReadAllText(Path.Combine(_root, run.Run, RunStateStore.ReportFile)));
                Assert.AreEqual("interrupted", (string)report["status"]);
            }
            Assert.AreEqual(false, _driver.Prefs["k1"]);
            Assert.AreEqual(false, _driver.Prefs["k2"]);
        }

        [Test]
        public void 재시작_뒤_status_없이_바로_start해도_끊긴_실행의_설정을_원래_값으로_되돌린다()
        {
            // M1: status를 한 번도 안 부르고 곧장 automation_start(또는 automation_step)를 부르는 실제 시나리오.
            // 고치기 전에는 이전 실행이 영영 안 닫혀 오염된 값("true")을 새 실행이 "원래 값"으로 잘못 기억했다.
            _driver.Prefs["k"] = false;
            var before = NewSession();
            var oldRun = before.Start("{\"name\":\"g\",\"setup\":[{\"op\":\"setpref\",\"key\":\"k\"}],\"loop\":[{\"op\":\"never\"}]}", "a.json", 0, _now);
            TickMany(before, 3);
            Assert.AreEqual(true, _driver.Prefs["k"]);   //  오염된 채로 "죽음"

            _memory.ActiveRunDir = null;   //  에디터 재시작 — SessionState가 비었다

            var after = NewSession();
            //  automation_status를 부르지 않고 바로 start — 새 실행도 같은 키를 쓴다.
            after.Start("{\"name\":\"g2\",\"setup\":[{\"op\":\"setpref\",\"key\":\"k\"}],\"loop\":[{\"op\":\"ok\"}]}", "b.json", 0, _now.AddSeconds(10));

            //  start가 돌아온 시점에 이미 옛 실행은 interrupted로 닫혔고 k는 진짜 원래 값(false)으로 돌아와 있어야 한다.
            var oldReport = JObject.Parse(File.ReadAllText(Path.Combine(_root, oldRun.Run, RunStateStore.ReportFile)));
            Assert.AreEqual("interrupted", (string)oldReport["status"]);
            Assert.AreEqual(false, _driver.Prefs["k"]);

            TickMany(after, 3);   //  새 실행의 setup(setpref)·loop(ok)·teardown(없음)을 끝까지 돈다

            Assert.AreEqual("completed", after.Status(_now).Status);
            //  새 실행이 "원래 값"으로 기억한 게 false였다는 뜻 — 오염된 true가 아니다.
            Assert.AreEqual(false, _driver.Prefs["k"]);
        }

        [Test]
        public void 도메인을_다시_불러온_새_세션이_표시된_실행을_이어_돈다()
        {
            var before = NewSession();
            var started = before.Start(Never, "a.json", 0, _now);
            TickMany(before, 2);

            var after = NewSession();   //  static이 다 사라진 셈 — 남은 것은 파일과 기억(SessionState)뿐
            after.Resume();

            Assert.IsTrue(after.IsRunning);
            Assert.AreEqual(started.Run, after.Status(_now).Run);
            Assert.IsTrue(after.Stop());
            TickMany(after, 5);
            Assert.IsFalse(after.IsRunning);
            Assert.AreEqual("stopped", after.Status(_now).Status);
            Assert.IsNull(_memory.ActiveRunDir);
        }

        [Test]
        public void 에디터가_다시_켜져_표시가_없는_실행은_interrupted로_적고_설정을_되돌린다()
        {
            _driver.Prefs["k"] = false;
            var before = NewSession();
            before.Start("{\"name\":\"g\",\"setup\":[{\"op\":\"setpref\",\"key\":\"k\"}],\"loop\":[{\"op\":\"never\"}]}", "a.json", 0, _now);
            TickMany(before, 3);
            Assert.AreEqual(true, _driver.Prefs["k"]);

            _memory.ActiveRunDir = null;   //  에디터 재시작 — SessionState가 비었다
            var status = NewSession().Status(_now);

            Assert.AreEqual("interrupted", status.Status);
            Assert.IsTrue(status.Done);
            Assert.AreEqual(false, _driver.Prefs["k"]);
            var report = JObject.Parse(File.ReadAllText(Path.Combine(_root, status.Run, RunStateStore.ReportFile)));
            Assert.AreEqual("interrupted", (string)report["status"]);
        }

        [Test]
        public void 실행이_하나도_없으면_idle이다()
        {
            var status = NewSession().Status(_now);

            Assert.AreEqual("idle", status.Status);
            Assert.IsTrue(status.Done);
        }
    }
}
