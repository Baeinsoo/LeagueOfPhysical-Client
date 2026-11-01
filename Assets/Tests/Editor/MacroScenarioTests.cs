using System;
using System.Collections.Generic;
using System.Linq;
using LOP.EditorTools.Automation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>녹화 시나리오 파일(spec §14.4) — setup·teardown, 단계 목록으로 다시 읽힘, 한 줄에 한 단계, 이름.</summary>
    public class MacroScenarioTests
    {
        private static readonly DateTime At = new DateTime(2026, 11, 1, 15, 30, 0);

        private static CompiledMacro EveryKind() => MacroCompiler.Compile(new[]
        {
            new RecordedEvent { Kind = RecordedKind.PointerDown, T = 1, X = 0.5f, Y = 0.5f, Target = new UiSelector { Name = "nav-quests" } },
            new RecordedEvent { Kind = RecordedKind.PointerUp, T = 1.1, X = 0.5f, Y = 0.5f },
            new RecordedEvent { Kind = RecordedKind.PointerDown, T = 30, X = 0.5f, Y = 0.9f, Target = new UiSelector { Text = "확인" } },
            new RecordedEvent { Kind = RecordedKind.PointerUp, T = 30.1, X = 0.5f, Y = 0.9f },
            new RecordedEvent { Kind = RecordedKind.PointerDown, T = 31, X = 0.2f, Y = 0.3f },
            new RecordedEvent { Kind = RecordedKind.PointerUp, T = 31.9, X = 0.2f, Y = 0.3f },
            new RecordedEvent { Kind = RecordedKind.PointerDown, T = 32, X = 0.1f, Y = 0.5f },
            new RecordedEvent { Kind = RecordedKind.PointerUp, T = 32.3, X = 0.9f, Y = 0.5f },
            new RecordedEvent { Kind = RecordedKind.PointerDown, T = 33, X = 0.4f, Y = 0.4f },
            new RecordedEvent { Kind = RecordedKind.PointerUp, T = 33.05, X = 0.4f, Y = 0.4f },
            new RecordedEvent { Kind = RecordedKind.Key, T = 34, Duration = 0.1, Key = "Space" },
            new RecordedEvent { Kind = RecordedKind.TextField, T = 35, Target = new UiSelector { Name = "cdn-input" }, Before = "", Value = "봇01" },
            new RecordedEvent { Kind = RecordedKind.State, T = 40, Value = "InMatch", Scene = "FlappyRace" },
            new RecordedEvent { Kind = RecordedKind.Screenshot, T = 41, Label = "rec-01" },
            new RecordedEvent { Kind = RecordedKind.Checkpoint, T = 42, Label = "tour/cp-01" },
        }, new MacroOptions());

        [Test]
        public void 로비에서_시작한_녹화는_로비까지_가는_setup을_갖고_단계_목록으로_다시_읽힌다()
        {
            var macro = EveryKind();
            var scenario = MacroScenario.Build("tour", "FrontEnd", "Lobby", "local-k8s", false, macro, At);
            var errors = new List<string>();

            var parsed = ScenarioParser.Parse(MacroScenario.ToJson(scenario), StepCatalog.CreateAll(), errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            CollectionAssert.AreEqual(new[] { "set_environment", "play_enter", "boot_to_lobby" }, parsed.Setup.Select(s => s.Op).ToArray());
            Assert.AreEqual(macro.Steps.Count, parsed.Loop.Count);
            CollectionAssert.IsSubsetOf(
                new[] { "ui_click", "ui_wait", "long_press", "drag", "touch", "input_tap", "ui_type", "wait_state", "screenshot", "snapshot_compare", "wait" },
                parsed.Loop.Select(s => s.Op).Distinct().ToArray());
            Assert.AreEqual("play_exit", parsed.Teardown.Single().Op);
            Assert.AreEqual(1, parsed.Repeat);
        }

        [Test]
        public void 로비가_아닌_곳에서_시작했으면_wait_state와_고칠_곳_메모를_남긴다()
        {
            var scenario = MacroScenario.Build("mid", "InMatch", "FlappyRace", null, true, EveryKind(), At);

            var setup = (JArray)scenario["setup"];
            CollectionAssert.AreEqual(new[] { "auto_flap", "play_enter", "boot_to_lobby", "wait_state" }, setup.Select(s => (string)s["op"]).ToArray());
            StringAssert.Contains("InMatch", (string)setup[3]["note"]);
            Assert.AreEqual("FlappyRace", (string)setup[3]["scene"]);
        }

        [Test]
        public void 동작이_없으면_log_한_단계로_읽힌다()
        {
            var scenario = MacroScenario.Build("empty", "FrontEnd", "Lobby", null, false, new CompiledMacro(), At);
            var errors = new List<string>();

            var parsed = ScenarioParser.Parse(MacroScenario.ToJson(scenario), StepCatalog.CreateAll(), errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual("log", parsed.Loop.Single().Op);
        }

        [Test]
        public void 파일은_단계_하나를_한_줄에_쓰고_같은_내용으로_읽힌다()
        {
            var scenario = MacroScenario.Build("tour", "FrontEnd", "Lobby", "local-k8s", false, EveryKind(), At);

            string json = MacroScenario.ToJson(scenario);

            StringAssert.Contains("\n    {\"op\":\"play_enter\"},\n", json);
            Assert.IsTrue(JToken.DeepEquals(scenario, JObject.Parse(json)));
        }

        [Test]
        public void 녹화_이름은_파일_이름으로_쓸_수_있는_것만()
        {
            Assert.IsNull(RecordNames.Validate("lobby-tour"));
            Assert.IsNull(RecordNames.Validate("rec-20261101-153000"));
            foreach (var bad in new[] { null, "", "../x", "A", "a b", "-x", new string('a', 65) })
            {
                Assert.IsNotNull(RecordNames.Validate(bad), bad);
            }
        }
    }
}
