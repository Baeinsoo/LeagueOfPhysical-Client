using System.Collections.Generic;
using System.Linq;
using LOP.EditorTools.Automation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>녹화 → 단계(spec §14.3) — 선택자 우선, 기다림은 대상으로, 좌표 앞 기다림 상한, 상태·글자 칸·단축키.</summary>
    public class MacroCompilerTests
    {
        private static RecordedEvent Down(double t, float x, float y, string name = null) => new RecordedEvent
        {
            Kind = RecordedKind.PointerDown, T = t, X = x, Y = y, Target = name == null ? null : new UiSelector { Name = name },
        };

        private static RecordedEvent Move(double t, float x, float y) => new RecordedEvent { Kind = RecordedKind.PointerMove, T = t, X = x, Y = y };
        private static RecordedEvent Up(double t, float x, float y) => new RecordedEvent { Kind = RecordedKind.PointerUp, T = t, X = x, Y = y };

        private static CompiledMacro Compile(params RecordedEvent[] events) => MacroCompiler.Compile(events, new MacroOptions());
        private static string[] Ops(CompiledMacro macro) => macro.Steps.Select(s => (string)s["op"]).ToArray();

        [Test]
        public void 이름_있는_요소를_짧게_누르면_ui_click이고_머뭇거림은_버린다()
        {
            var macro = Compile(Down(2, 0.5f, 0.5f, "play-button"), Up(2.1, 0.5f, 0.5f));

            Assert.AreEqual("{\"op\":\"ui_click\",\"name\":\"play-button\"}", macro.Steps.Single().ToString(Formatting.None));
        }

        [Test]
        public void 오래_기다린_뒤_누른_요소는_ui_wait_clickable을_앞에_둔다()
        {
            var macro = Compile(Down(25, 0.5f, 0.9f, "confirm-button"), Up(25.1, 0.5f, 0.9f));

            CollectionAssert.AreEqual(new[] { "ui_wait", "ui_click" }, Ops(macro));
            Assert.AreEqual("confirm-button", (string)macro.Steps[0]["name"]);
            Assert.IsTrue((bool)macro.Steps[0]["clickable"]);
            Assert.AreEqual(75, (int)macro.Steps[0]["timeout_s"]);
        }

        [Test]
        public void 대상이_없으면_touch이고_앞의_간격은_상한까지_wait다()
        {
            var macro = Compile(
                Down(1, 0.123456f, 0.5f), Up(1.08, 0.123456f, 0.5f),
                Down(13.08, 0.2f, 0.2f), Up(13.18, 0.2f, 0.2f));

            CollectionAssert.AreEqual(new[] { "wait", "touch", "wait", "touch" }, Ops(macro));
            Assert.AreEqual(1.0, (double)macro.Steps[0]["seconds"]);
            Assert.AreEqual(0.123, (double)macro.Steps[1]["x"]);
            Assert.AreEqual(80, (int)macro.Steps[1]["hold_ms"]);
            Assert.AreEqual(5.0, (double)macro.Steps[2]["seconds"]);
            StringAssert.Contains("12.0", (string)macro.Steps[2]["note"]);
        }

        [Test]
        public void 오래_누르면_long_press이고_움직이면_drag다()
        {
            var macro = Compile(
                Down(0, 0.5f, 0.5f), Up(0.9, 0.5f, 0.505f),
                Down(1.0, 0.1f, 0.5f), Move(1.1, 0.5f, 0.5f), Up(1.3, 0.9f, 0.5f));

            CollectionAssert.AreEqual(new[] { "long_press", "drag" }, Ops(macro));
            Assert.AreEqual(900, (int)macro.Steps[0]["hold_ms"]);
            var drag = macro.Steps[1];
            Assert.AreEqual(0.1, (double)drag["x"]);
            Assert.AreEqual(0.9, (double)drag["to_x"]);
            Assert.AreEqual(300, (int)drag["duration_ms"]);
        }

        [Test]
        public void 키는_input_tap이다()
        {
            var macro = Compile(new RecordedEvent { Kind = RecordedKind.Key, T = 2, Duration = 0.15, Key = "Space" });

            CollectionAssert.AreEqual(new[] { "wait", "input_tap" }, Ops(macro));
            Assert.AreEqual("Space", (string)macro.Steps[1]["key"]);
            Assert.AreEqual(150, (int)macro.Steps[1]["hold_ms"]);
        }

        [Test]
        public void 글자_칸은_ui_type이고_Enter면_submit이며_안_바꿨으면_버린다()
        {
            var field = new UiSelector { Name = "cdn-input" };
            var macro = Compile(
                new RecordedEvent { Kind = RecordedKind.TextField, T = 1, Target = field, Before = "옛이름", Value = "봇01", Submit = true },
                new RecordedEvent { Kind = RecordedKind.TextField, T = 5, Target = field, Before = "봇01", Value = "봇01" });

            Assert.AreEqual("{\"op\":\"ui_type\",\"name\":\"cdn-input\",\"value\":\"봇01\",\"submit\":true}", macro.Steps.Single().ToString(Formatting.None));
        }

        [Test]
        public void 연달아_바뀐_상태는_마지막_것만_기다린다()
        {
            var macro = Compile(
                new RecordedEvent { Kind = RecordedKind.State, T = 5, Value = "InMatch", Scene = "Room" },
                new RecordedEvent { Kind = RecordedKind.State, T = 6, Value = "InMatch", Scene = "FlappyRace" },
                Down(9, 0.5f, 0.5f), Up(9.1, 0.5f, 0.5f));

            CollectionAssert.AreEqual(new[] { "wait_state", "wait", "touch" }, Ops(macro));
            Assert.AreEqual("FlappyRace", (string)macro.Steps[0]["scene"]);
            Assert.AreEqual(60, (int)macro.Steps[0]["timeout_s"]);
            Assert.AreEqual(3.0, (double)macro.Steps[1]["seconds"]);
        }

        [Test]
        public void F9는_스크린샷이고_F10은_기다림_없는_화면_비교다()
        {
            var macro = Compile(
                new RecordedEvent { Kind = RecordedKind.Screenshot, T = 3, Label = "rec-01" },
                new RecordedEvent { Kind = RecordedKind.Checkpoint, T = 3.5, Label = "tour/cp-01" });

            CollectionAssert.AreEqual(new[] { "wait", "screenshot", "snapshot_compare" }, Ops(macro));
            Assert.AreEqual("rec-01", (string)macro.Steps[1]["as"]);
            Assert.AreEqual("tour/cp-01", (string)macro.Steps[2]["baseline"]);
        }

        [Test]
        public void 떼지_않고_끝난_누름은_버리고_경고한다()
        {
            var macro = Compile(Down(1, 0.5f, 0.5f, "play-button"));

            Assert.IsEmpty(macro.Steps);
            Assert.AreEqual(1, macro.Warnings.Count);
        }

        [Test]
        public void 누름과_뗌_사이에_들어온_글자_칸도_잃지_않는다()
        {
            //  칸을 누르면 같은 프레임쯤 초점이 들어와 TextField 사실이 누름과 뗌 사이에 끼인다.
            var macro = Compile(
                Down(3, 0.5f, 0.5f, "cdn-input"),
                new RecordedEvent { Kind = RecordedKind.TextField, T = 3.01, Target = new UiSelector { Name = "cdn-input" }, Before = "", Value = "a" },
                Up(3.08, 0.5f, 0.5f));

            CollectionAssert.AreEqual(new[] { "ui_click", "ui_type" }, Ops(macro));
        }
    }
}
