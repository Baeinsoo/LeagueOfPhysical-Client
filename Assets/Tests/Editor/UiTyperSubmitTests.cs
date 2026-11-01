using LOP.EditorTools.Automation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// ui_type의 submit:true(Enter 흉내, spec §8.5)가 ChangeDisplayNameView.OnKeyDown과 똑같은 등록 방식
    /// (TextField 자신에 RegisterCallback&lt;KeyDownEvent&gt;, 기본 TrickleDown=버블)의 바깥 핸들러를 깨우는지(Task 6 S5).
    /// UI Toolkit 6000.3의 KeyboardTextEditorEventHandler는 안쪽 텍스트 요소(초점 요소)에서 Return을 항상
    /// StopPropagation으로 삼킨다 — 초점 요소로 보내면(기존 길) 절대 바깥 TextField 핸들러까지 안 번진다.
    /// </summary>
    public class UiTyperSubmitTests
    {
        private GameObject _host;
        private PanelSettings _panelSettings;
        private TextField _field;
        private bool _confirmed;

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

            //  ChangeDisplayNameView.OnKeyDown과 똑같은 등록 — 필드 자신에, 기본(트리클다운 아님=버블).
            _confirmed = false;
            _field.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                _confirmed = true;
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_panelSettings);
        }

        //  value=""·clear=false로 글자 넣기 자체는 건드리지 않고 submit 길만 가둬서 본다
        //  (빈 칸에서 SelectAll·막 만든 칸의 글자 엔진 동기화는 딴 유니티 버그라 이 시험의 관심사가 아니다).

        [Test]
        public void ui_type의_submit은_필드_자신에_등록된_바깥_KeyDown_핸들러를_깨운다()
        {
            bool ok = UiTyper.Type(_field, "", clear: false, submit: true, out var why);

            Assert.IsTrue(ok, why);
            Assert.IsTrue(_confirmed, "submit:true가 바깥 KeyDownEvent 핸들러(확인 동작)를 깨우지 못했다");
        }

        [Test]
        public void submit이_false면_바깥_핸들러를_안_깨운다()
        {
            bool ok = UiTyper.Type(_field, "", clear: false, submit: false, out var why);

            Assert.IsTrue(ok, why);
            Assert.IsFalse(_confirmed, "submit:false인데 확인 동작이 깨어났다");
        }
    }
}
