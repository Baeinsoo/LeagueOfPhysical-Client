using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// 나가기 확인 창을 실제 UXML로 띄우고 누르기를 흉내 낸다 — 어디를 누르면 닫히고 어디는 그대로인지.
    /// 누르기 신호는 화면판(panel)에 붙은 요소에만 전달되므로 숨긴 UIDocument에 붙인다.
    /// </summary>
    public class LeaveMatchConfirmViewTests
    {
        private GameObject _host;
        private PanelSettings _panelSettings;
        private LeaveMatchConfirmView _view;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _host = new GameObject("leave-confirm-test") { hideFlags = HideFlags.HideAndDontSave };
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;

            _root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uxml").Instantiate();
            document.rootVisualElement.Add(_root);

            _view = new LeaveMatchConfirmView(new LeaveMatchConfirmViewModel());
            _view.Initialize(_root);
            _view.OnOpen();
        }

        [TearDown]
        public void TearDown()
        {
            _view.OnClose();
            _view.Dispose();
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_panelSettings);
        }

        private static void Press(VisualElement element)
        {
            var mouse = new UnityEngine.Event { type = EventType.MouseDown, button = 0 };
            using var evt = PointerDownEvent.GetPooled(mouse);
            evt.target = element;
            element.SendEvent(evt);
        }

        //  버튼은 누르기·떼기 대신 "확정" 신호로 누른다 — 둘 다 같은 clicked로 이어진다.
        private static void Submit(Button button)
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        private void AssertStillOpen() =>
            Assert.AreEqual(UniTaskStatus.Pending, _view.ResultAsync.Status, "창이 닫히면 안 된다");

        private void AssertClosedWith(bool expected)
        {
            Assert.AreEqual(UniTaskStatus.Succeeded, _view.ResultAsync.Status, "창이 닫혀야 한다");
            Assert.AreEqual(expected, _view.ResultAsync.GetAwaiter().GetResult());
        }

        [Test]
        public void 판_바깥을_누르면_취소된다()
        {
            Press(_root.Q("leave-root"));
            AssertClosedWith(false);
        }

        [Test]
        public void 판_가장자리_그림자를_눌러도_그대로다()
        {
            Press(_root.Q(className: "kit-panel__back"));
            AssertStillOpen();
        }

        [Test]
        public void 판_안_글자를_눌러도_그대로다()
        {
            Press(_root.Q<Label>(className: "leave-message"));
            AssertStillOpen();
        }

        [Test]
        public void 취소_버튼은_머문다는_결과()
        {
            Submit(_root.Q<Button>("leave-cancel"));
            AssertClosedWith(false);
        }

        [Test]
        public void 나가기_버튼은_나간다는_결과()
        {
            Submit(_root.Q<Button>("leave-confirm"));
            AssertClosedWith(true);
        }
    }
}
