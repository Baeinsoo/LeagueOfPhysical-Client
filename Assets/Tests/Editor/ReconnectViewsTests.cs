using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>재접속 덮개와 실패 팝업을 실제 UXML로 띄운다 — 눌러서 닫히지 않고, [다시 시도]만 팝업을 닫는다.</summary>
    public class ReconnectViewsTests
    {
        private GameObject _host;
        private PanelSettings _panelSettings;
        private VisualElement _docRoot;

        [SetUp]
        public void SetUp()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _host = new GameObject("reconnect-test") { hideFlags = HideFlags.HideAndDontSave };
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            _docRoot = document.rootVisualElement;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_panelSettings);
        }

        private VisualElement Load(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path).Instantiate();
            _docRoot.Add(root);
            return root;
        }

        //  버튼은 누르기·떼기 대신 "확정" 신호로 누른다 — 둘 다 같은 clicked로 이어진다.
        private static void Submit(Button button)
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        //  처음 입장·다시 들어가기 중에는 로딩판(Loading 층, 입력 막음)이 떠 있다 — 그 아래 깔리면 팝업을 못 누른다.
        [Test]
        public void 덮개와_팝업은_로딩판보다_위에_뜬다()
        {
            Assert.Greater(new ReconnectingOverlayView().Layer, UILayer.Loading);
            Assert.Greater(new ReconnectFailedView(new ReconnectFailedViewModel()).Layer, UILayer.Loading);
        }

        [Test]
        public void 덮개는_눌러도_닫히지_않고_문구를_보여_준다()
        {
            var root = Load("Assets/UI/Reconnect/ReconnectingOverlayView.uxml");
            var view = new ReconnectingOverlayView();
            view.Initialize(root);
            view.OnOpen();

            Assert.IsFalse(view.AutoClose);
            Assert.IsTrue(view.IsModal, "뒤 경기 입력을 막아야 한다");
            Assert.AreEqual("연결이 끊겼습니다", root.Q<Label>("reconnecting-title").text);
            StringAssert.Contains("다시 연결하는 중", root.Q<Label>("reconnecting-message").text);
            view.OnClose();
            view.Dispose();
        }

        [Test]
        public void 팝업은_다시_시도를_눌러야_닫힌다()
        {
            var root = Load("Assets/UI/Reconnect/ReconnectFailedView.uxml");
            var view = new ReconnectFailedView(new ReconnectFailedViewModel());
            view.Initialize(root);
            view.OnOpen();

            Assert.IsFalse(view.AutoClose);
            Assert.AreEqual(ReconnectFailedViewModel.Message, root.Q<Label>("reconnect-failed-message").text);
            Assert.AreEqual(UniTaskStatus.Pending, view.ResultAsync.Status);

            Submit(root.Q<Button>("reconnect-failed-retry"));

            Assert.AreEqual(UniTaskStatus.Succeeded, view.ResultAsync.Status);
            view.OnClose();
            view.Dispose();
        }
    }
}
