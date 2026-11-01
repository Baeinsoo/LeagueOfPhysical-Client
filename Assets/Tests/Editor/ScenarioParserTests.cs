using System;
using System.Collections.Generic;
using System.Linq;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>시나리오 JSON 읽기(spec 2026-11-01 §4.1) — 세 목록·공통 칸·기본 시간·오류를 한꺼번에 모으기.</summary>
    public class ScenarioParserTests
    {
        private sealed class FakeHandler : IStepHandler
        {
            private readonly double _timeout;
            public FakeHandler(string op, double timeout = 7) { Op = op; _timeout = timeout; }
            public string Op { get; }
            public double DefaultTimeoutSeconds(ScenarioStep step) => _timeout;
            public void Validate(ScenarioStep step, List<string> errors)
            {
                if (Op == "needs" && step.Str("x") == null) errors.Add($"{step}: x가 없다");
            }
            public StepResult Tick(StepContext ctx) => StepResult.Done();
        }

        private static readonly Dictionary<string, IStepHandler> Handlers =
            new IStepHandler[] { new FakeHandler("a"), new FakeHandler("needs") }.ToDictionary(h => h.Op);

        private static AutomationScenario Parse(string json, out List<string> errors)
        {
            errors = new List<string>();
            return ScenarioParser.Parse(json, Handlers, errors);
        }

        [Test]
        public void 세_목록과_공통_칸을_읽는다()
        {
            var s = Parse("{\"name\":\"n\",\"description\":\"d\",\"repeat\":4,\"setup\":[{\"op\":\"a\"}]," +
                          "\"loop\":[{\"op\":\"a\",\"optional\":true,\"timeout_s\":12},{\"op\":\"needs\",\"x\":\"1\"}],\"teardown\":[]}", out var errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual("n", s.Name);
            Assert.AreEqual("d", s.Description);
            Assert.AreEqual(4, s.Repeat);
            Assert.AreEqual(1, s.Setup.Count);
            Assert.AreEqual(2, s.Loop.Count);
            Assert.AreEqual(0, s.Teardown.Count);
            Assert.IsTrue(s.Loop[0].Optional);
            Assert.AreEqual(12, s.Loop[0].TimeoutSeconds);
            Assert.AreEqual("loop[1]", s.Loop[1].Where);
            Assert.AreEqual("needs", s.Loop[1].Op);
            Assert.AreEqual("1", s.Loop[1].Str("x"));
            Assert.AreEqual("loop[1] needs", s.Loop[1].ToString());
            Assert.IsNull(s.Loop[0].Args["op"]);
            Assert.IsNull(s.Loop[0].Args["optional"]);
            Assert.IsNull(s.Loop[0].Args["timeout_s"]);
        }

        [Test]
        public void timeout_s가_없으면_단계_기본값을_쓰고_repeat는_1이다()
        {
            var s = Parse("{\"name\":\"n\",\"loop\":[{\"op\":\"a\"}]}", out var errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(7, s.Loop[0].TimeoutSeconds);
            Assert.IsFalse(s.Loop[0].Optional);
            Assert.AreEqual(1, s.Repeat);
        }

        [Test]
        public void 모르는_op는_위치와_함께_오류다()
        {
            var s = Parse("{\"name\":\"n\",\"loop\":[{\"op\":\"a\"},{\"op\":\"zzz\"}]}", out var errors);

            Assert.IsNull(s);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("loop[1]", errors[0]);
            StringAssert.Contains("zzz", errors[0]);
        }

        [Test]
        public void 구조_오류와_단계_검증_오류를_한꺼번에_모은다()
        {
            var s = Parse("{\"name\":\"\",\"repeat\":0,\"loop\":[{\"op\":\"needs\",\"timeout_s\":0}],\"extra\":1}", out var errors);

            Assert.IsNull(s);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("name")));
            Assert.IsTrue(errors.Any(e => e.Contains("repeat")));
            Assert.IsTrue(errors.Any(e => e.Contains("timeout_s")));
            Assert.IsTrue(errors.Any(e => e.Contains("x가 없다")));
            Assert.IsTrue(errors.Any(e => e.Contains("extra")));
        }

        [Test]
        public void loop가_비면_오류다()
        {
            Assert.IsNull(Parse("{\"name\":\"n\",\"setup\":[{\"op\":\"a\"}]}", out var errors));
            StringAssert.Contains("loop", errors.Single());
        }

        [Test]
        public void 깨진_JSON은_오류다()
        {
            Assert.IsNull(Parse("{", out var errors));
            StringAssert.Contains("JSON", errors.Single());
        }

        [Test]
        public void 단계_하나는_loop_하나짜리_시나리오가_된다()
        {
            var s = Parse(ScenarioParser.SingleStepScenario("{\"op\":\"needs\",\"x\":\"1\",\"timeout_s\":3}"), out var errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual("step-needs", s.Name);
            Assert.AreEqual(1, s.Repeat);
            Assert.AreEqual(1, s.Loop.Count);
            Assert.AreEqual(3, s.Loop[0].TimeoutSeconds);
        }

        [Test]
        public void 단계_하나_JSON이_깨지면_ArgumentException이다()
        {
            Assert.Throws<ArgumentException>(() => ScenarioParser.SingleStepScenario("{\"op\":"));
        }
    }
}
