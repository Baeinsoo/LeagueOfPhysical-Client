using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 연출을 그리는 쪽(spec §7): 화면 공간 판에서 내 새를 <b>읽기만</b> 한다 — 새 그림의 자식이 되거나 자리를 고치면 안 된다(10-25 규칙).
    /// 날갯짓은 그 장의 그려진 새 자리로 낸다.
    /// </summary>
    public class FlapComboViewTests
    {
        private const float R = 0.45f;
        private GameObject viewGo;
        private GameObject visualGo;
        private GameObject cameraGo;
        private bool hasBird;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { viewGo, visualGo, cameraGo })
            {
                if (go != null) { Object.DestroyImmediate(go); }
            }
        }

        private (FlapComboView view, FlapComboFx fx) Make()
        {
            hasBird = true;
            visualGo = new GameObject("Visual_me");
            visualGo.transform.position = new Vector3(5f, 1f, 0f);
            cameraGo = new GameObject("Cam");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.transform.position = new Vector3(5f, 3f, -23f);

            viewGo = new GameObject("FlapComboView");
            viewGo.SetActive(false);
            var document = viewGo.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/FlapComboPanelSettings");
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/FlapCombo");
            var view = viewGo.AddComponent<FlapComboView>();
            viewGo.SetActive(true);
            var fx = new FlapComboFx(new System.Random(3));
            view.cameraOverride = camera;
            view.Bind(document.rootVisualElement.Q("combo-root"), fx, () => hasBird ? visualGo.transform : null, () => true, R);
            return (view, fx);
        }

        [Test]
        public void 리소스가_있다()
        {
            var panel = Resources.Load<PanelSettings>("UI/FlapComboPanelSettings");
            Assert.IsNotNull(panel);
            Assert.Less(panel.sortingOrder, 0, "앱 UI(HUD) 아래");
            Assert.IsNotNull(Resources.Load<VisualTreeAsset>("UI/FlapCombo"));
        }

        [Test]
        public void 날갯짓을_그리고_새_그림은_건드리지_않는다()
        {
            var (view, fx) = Make();
            fx.Queue(10);

            view.Step(0.016f);

            Assert.AreEqual(1, fx.Numbers.Count, "그 장의 새 자리로 냈다");
            Assert.AreEqual(DisplayStyle.Flex, view.NumberLabels[0].style.display.value);
            Assert.AreEqual("+10", view.NumberLabels[0].text);
            Assert.AreEqual(DisplayStyle.Flex, view.Word.style.display.value);
            Assert.AreEqual("좋아!", view.Word.text);
            Assert.AreEqual(new Vector3(5f, 1f, 0f), visualGo.transform.position, "새 그림 자리는 그대로");
            Assert.AreEqual(0, visualGo.transform.childCount, "새 그림에 아무것도 붙이지 않는다");
            Assert.IsNull(viewGo.transform.parent);
            Assert.AreEqual(new Vector3(5f, 3f, -23f), cameraGo.transform.position, "카메라도 그대로");
        }

        [Test]
        public void 새가_없으면_밀린_날갯짓을_버린다()
        {
            var (view, fx) = Make();
            hasBird = false;   //  잡혔거나 아직 그림이 안 붙었다
            fx.Queue(3);

            view.Step(0.016f);

            Assert.AreEqual(0, fx.Numbers.Count);
            Assert.AreEqual(DisplayStyle.None, view.Word.style.display.value);
        }

        [Test]
        public void 게이지를_준_고비는_한마디_글자에_대시플러스가_붙는다()
        {
            var (view, fx) = Make();
            fx.Queue(10, 0, true);

            view.Step(0.016f);

            Assert.AreEqual(DisplayStyle.Flex, view.Word.style.display.value);
            Assert.AreEqual(FlapComboLook.WordWithCue("좋아!", true), view.Word.text);
        }
    }
}
