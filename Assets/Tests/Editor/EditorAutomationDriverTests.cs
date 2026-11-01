using LOP.EditorTools.Automation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>EditorAutomationDriver의 좌표 Pick 후보 판정(리뷰 M1) — 월드 공간 패널(간판·이름표)은 화면 정규화 좌표 Pick에서 빠진다.</summary>
    public class EditorAutomationDriverTests
    {
        private GameObject _go;
        private PanelSettings _settings;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        private UIDocument MakeDocument(PanelRenderMode renderMode)
        {
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.renderMode = renderMode;
            _go = new GameObject("doc");
            var document = _go.AddComponent<UIDocument>();
            document.panelSettings = _settings;
            return document;
        }

        [Test]
        public void 화면_공간_패널은_좌표_Pick_후보다()
        {
            var screen = MakeDocument(PanelRenderMode.ScreenSpaceOverlay);

            Assert.IsTrue(EditorAutomationDriver.IsScreenSpaceDocument(screen));
        }

        [Test]
        public void 월드_공간_패널은_좌표_Pick에서_빠진다()
        {
            var world = MakeDocument(PanelRenderMode.WorldSpace);

            Assert.IsFalse(EditorAutomationDriver.IsScreenSpaceDocument(world));
        }

        [Test]
        public void panelSettings이_없으면_화면_공간으로_본다()
        {
            _go = new GameObject("doc");
            var document = _go.AddComponent<UIDocument>();

            Assert.IsTrue(EditorAutomationDriver.IsScreenSpaceDocument(document));
        }
    }
}
