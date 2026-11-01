using System.Collections.Generic;
using System.IO;
using LOP.UI;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>키트 이름 바꾸기 모달 — View 코드가 찾는 요소 이름 그대로, 키트 루트·판.</summary>
    public class ChangeDisplayNameViewKitTests
    {
        private const string Uxml = "Assets/UI/ChangeDisplayName/ChangeDisplayNameView.uxml";
        private const string Uss = "Assets/UI/ChangeDisplayName/ChangeDisplayNameView.uss";

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            foreach (var n in new[] { "cdn-root", "cdn-input", "cdn-message", "cdn-cancel", "cdn-confirm" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("class=\"kit cdn-root", x);
            StringAssert.Contains("kit-panel", x);
            StringAssert.Contains("kit-btn--primary", x);
        }

        [Test]
        public void 키트_색만_쓴다()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains("var(--lop-live)", s);
            StringAssert.DoesNotContain("rgb(255, 120, 120)", s);
            StringAssert.DoesNotContain("#000", s);
        }

        private sealed class FakeUserDataStore : IUserDataStore
        {
            public User user { get; set; }
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        /// <summary>
        /// M1 재고침 — ChangeDisplayNameView.OnKeyDown을 TrickleDown으로 등록했으니 진짜 키보드와 같은 길
        /// (UiTyper를 거치지 않고 직접 초점 요소로 보내는 KeyDownEvent)로 Enter를 보내도 OnConfirmClicked →
        /// Submit이 정확히 한 번만 깨어나는지 본다. 짧은 이름("x")은 로컬 검사에서 막혀 네트워크 없이도
        /// MessageChanged가 울린다 — 그 횟수로 "정확히 한 번"을 잰다(두 번 불리면 메시지도 두 번 울린다).
        /// </summary>
        [Test]
        public void 진짜_Enter는_확인_동작을_정확히_한번_깨운다()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var host = new GameObject("change-display-name-test") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;

                var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
                document.rootVisualElement.Add(root);

                var store = new FakeUserDataStore { user = new User { id = "me", displayName = "원래이름" } };
                var viewModel = new ChangeDisplayNameViewModel(store);
                var view = new ChangeDisplayNameView(viewModel);
                view.Initialize(root);
                view.OnOpen();

                try
                {
                    var input = root.Q<TextField>("cdn-input");
                    input.value = "x"; // MinLength(2) 미만 — 로컬 검사로만 막혀 네트워크가 필요 없다.
                    input.Focus();

                    int messageChanged = 0;
                    viewModel.MessageChanged += () => messageChanged++;

                    //  UiTyper를 거치지 않는다 — 진짜 키보드가 UI Toolkit에 만드는 것과 같은 길로 초점 요소에 직접 보낸다.
                    var target = input.panel?.focusController?.focusedElement as VisualElement ?? input;
                    var imgui = new UnityEngine.Event { type = EventType.KeyDown, keyCode = KeyCode.Return };
                    using (var evt = KeyDownEvent.GetPooled(imgui))
                    {
                        target.SendEvent(evt);
                    }

                    Assert.AreEqual(1, messageChanged, "Enter 한 번에 확인 동작(Submit)이 정확히 한 번 깨어나야 한다");
                    Assert.AreEqual("2~12자로 입력해 주세요.", viewModel.Message);
                }
                finally
                {
                    view.OnClose();
                    view.Dispose();
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(panelSettings);
            }
        }
    }
}
