using LOP.EditorTools.Automation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// ui_type의 submit:true(Enter 흉내, spec §8.5)가 ChangeDisplayNameView.OnKeyDown과 똑같은 등록 방식
    /// (TextField 자신에 RegisterCallback&lt;KeyDownEvent&gt;(핸들러, TrickleDown.TrickleDown))의 바깥 핸들러를
    /// 정확히 한 번만 깨우는지(Task 6 S5, 머지 전 M1 재고침). UI Toolkit 6000.3의 KeyboardTextEditorEventHandler는
    /// 안쪽 텍스트 요소(초점 요소)에서 Return을 AtTarget 단계에 StopPropagation으로 삼킨다 — 그래서 바깥 핸들러는
    /// TrickleDown(그보다 먼저 도는 단계)으로 등록해야 한다. UiTyper.SendSubmit은 지름길 없이 초점 요소로 그대로
    /// 보내므로(진짜 키보드와 같은 경로), 이 시험은 그 경로에서 TrickleDown 등록이 실제로 깨어나는지 증명한다.
    /// </summary>
    public class UiTyperSubmitTests
    {
        private GameObject _host;
        private PanelSettings _panelSettings;
        private TextField _field;
        private int _confirmCount;

        [SetUp]
        public void SetUp()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _host = new GameObject("ui-typer-submit-test") { hideFlags = HideFlags.HideAndDontSave };
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;

            //  ChangeDisplayNameView.OnOpen과 똑같이 — Focus 전에 값이 있어야 한다(빈 칸에서 SelectAll은 딴 유니티 버그를 밟는다).
            _field = new TextField { name = "cdn-input", value = "자동화테스트" };
            document.rootVisualElement.Add(_field);

            //  ChangeDisplayNameView.OnKeyDown과 똑같은 등록 — 필드 자신에, TrickleDown.
            _confirmCount = 0;
            _field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                _confirmCount++;
            }
        }

        [TearDown]
        public void TearDown()
        {
            _field.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_panelSettings);
        }

        //  value=""·clear=false로 글자 넣기 자체는 건드리지 않고 submit 길만 가둬서 본다
        //  (빈 칸에서 SelectAll·막 만든 칸의 글자 엔진 동기화는 딴 유니티 버그라 이 시험의 관심사가 아니다).

        [Test]
        public void ui_type의_submit은_TrickleDown_등록된_바깥_KeyDown_핸들러를_정확히_한번_깨운다()
        {
            bool ok = UiTyper.Type(_field, "", clear: false, submit: true, out var why);

            Assert.IsTrue(ok, why);
            Assert.AreEqual(1, _confirmCount, "submit:true가 바깥 KeyDownEvent 핸들러(확인 동작)를 정확히 한 번 깨우지 못했다");
        }

        [Test]
        public void submit이_false면_바깥_핸들러를_안_깨운다()
        {
            bool ok = UiTyper.Type(_field, "", clear: false, submit: false, out var why);

            Assert.IsTrue(ok, why);
            Assert.AreEqual(0, _confirmCount, "submit:false인데 확인 동작이 깨어났다");
        }
    }
}
