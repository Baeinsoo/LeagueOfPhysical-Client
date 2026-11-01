using System;
using System.Reflection;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// ui_click CLI(spec §7.1 배타 목록 바깥, Review L2) — UiClicker는 패널 이벤트라 녹화기의 Input System
    /// 엿보기(MacroRecorder.OnEvent)에 안 걸린다. 녹화 중에 쓰면 그 클릭이 시나리오에서 조용히 빠지므로,
    /// MacroRecorderHost.IsRecording이면 ui_click 자체를 거절한다(MacroRecorderHost.Start·AutomationHost.Session.BlockReason과
    /// 같은 방향). 실제 재생을 켜지 않고 MacroRecorderHost의 private static _active를 리플렉션으로 꽂아
    /// "녹화 중" 상태를 흉내 낸다.
    /// </summary>
    public class AutomationCommandsUiClickTests
    {
        private static readonly FieldInfo ActiveField =
            typeof(MacroRecorderHost).GetField("_active", BindingFlags.NonPublic | BindingFlags.Static);

        [TearDown]
        public void TearDown()
        {
            //  다음 시험이 "녹화 중"을 물려받지 않게 반드시 되돌린다.
            ActiveField.SetValue(null, null);
        }

        [Test]
        public void 녹화_중이면_ui_click은_거절된다()
        {
            Assert.IsFalse(MacroRecorderHost.IsRecording, "다른 시험이 녹화 상태를 안 치웠다");

            var recorder = new MacroRecorder("rec-ui-click-test", "Automation/Scenarios/rec-ui-click-test.json", 5.0, allInput: false);
            try
            {
                ActiveField.SetValue(null, recorder);
                Assert.IsTrue(MacroRecorderHost.IsRecording);

                var ex = Assert.Throws<InvalidOperationException>(() => AutomationCommands.UiClick(name: "whatever"));
                StringAssert.Contains("녹화 중", ex.Message);
            }
            finally
            {
                ActiveField.SetValue(null, null);
                recorder.Finish();
            }
        }

        [Test]
        public void 녹화_중이_아니면_녹화_사유로_거절되지_않는다()
        {
            Assert.IsFalse(MacroRecorderHost.IsRecording);

            //  선택자를 하나도 안 줘 다른 이유(ArgumentException)로 거절된다 — "녹화 중" 메시지는 아니어야 한다.
            var ex = Assert.Throws<ArgumentException>(() => AutomationCommands.UiClick());
            StringAssert.DoesNotContain("녹화 중", ex.Message);
        }
    }
}
