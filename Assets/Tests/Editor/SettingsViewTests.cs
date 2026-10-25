using System.IO;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// 설정 셸 첫 두 항목(spec §3 "흔들림 끄기", §2 "상대 이름 보기"). 값은 기기에 남고, 화면은 지금 값이 눌린 모양을 보인다.
    /// </summary>
    public class SettingsViewTests
    {
        private const string Uxml = "Assets/UI/Shell/SettingsView.uxml";
        private const string Uss = "Assets/UI/Shell/SettingsView.uss";
        private const string On = "kit-toggle__opt--on";

        private static readonly string[] Keys = { FlappyDisplayPrefs.ShakeOffKey, FlappyDisplayPrefs.ShowOpponentNamesKey };
        private readonly bool[] had = new bool[2];
        private readonly int[] saved = new int[2];
        private SettingsView view;

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                had[i] = PlayerPrefs.HasKey(Keys[i]);
                saved[i] = PlayerPrefs.GetInt(Keys[i], 0);
                PlayerPrefs.DeleteKey(Keys[i]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            for (int i = 0; i < Keys.Length; i++)
            {
                if (had[i]) { PlayerPrefs.SetInt(Keys[i], saved[i]); }
                else { PlayerPrefs.DeleteKey(Keys[i]); }
            }
        }

        private VisualElement Open()
        {
            view = new SettingsView();
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            view.Initialize(root);
            view.OnOpen();
            return root;
        }

        [Test]
        public void 흔들림은_기본으로_켜져_있다()
        {
            Assert.IsFalse(FlappyDisplayPrefs.ShakeOff);
        }

        [Test]
        public void 셸_틀과_두_항목이_있다()
        {
            string x = File.ReadAllText(Uxml);
            ShellFrameTests.AssertShellFrame(x);
            foreach (var n in new[] { "settings-shake-row", "settings-shake-on", "settings-shake-off", "settings-names-row", "settings-names-hide", "settings-names-show" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("kit-toggle", x);
        }

        [Test]
        public void 처음_열면_기본값이_눌려_있다()
        {
            var root = Open();

            Assert.IsTrue(root.Q("settings-shake-on").ClassListContains(On), "흔들림 켜기");
            Assert.IsFalse(root.Q("settings-shake-off").ClassListContains(On));
            Assert.IsTrue(root.Q("settings-names-hide").ClassListContains(On), "상대 이름 숨기기");
            Assert.IsFalse(root.Q("settings-names-show").ClassListContains(On));
        }

        [Test]
        public void 흔들림_끄기를_고르면_저장되고_눌림이_옮겨간다()
        {
            var root = Open();

            view.SetShakeOff(true);

            Assert.IsTrue(FlappyDisplayPrefs.ShakeOff);
            Assert.IsTrue(root.Q("settings-shake-off").ClassListContains(On));
            Assert.IsFalse(root.Q("settings-shake-on").ClassListContains(On));
        }

        [Test]
        public void 상대_이름_보기를_고르면_저장된다()
        {
            var root = Open();

            view.SetShowOpponentNames(true);

            Assert.IsTrue(FlappyDisplayPrefs.ShowOpponentNames);
            Assert.IsTrue(root.Q("settings-names-show").ClassListContains(On));
            Assert.IsFalse(root.Q("settings-names-hide").ClassListContains(On));
        }

        [Test]
        public void 창_목록은_설정_화면을_쓴다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UI/UIViewCatalog.asset");
            Assert.IsTrue(catalog.TryGet(nameof(SettingsView), out var entry));
            Assert.AreEqual(Uxml, AssetDatabase.GetAssetPath(entry.uxml));
            Assert.AreEqual(Uss, AssetDatabase.GetAssetPath(entry.uss));
        }
    }
}
