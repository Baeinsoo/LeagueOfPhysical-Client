using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 동전 카드 한 장의 면(월드 공간 UI 패널 — 이름표와 같은 방식이라 한글이 그대로 나온다).
    /// 지름 1.2 m 동그라미 안에 색 이름 글자를 글씨 색으로, 바탕은 진한 색. 결과 페이즈엔 정답 카드 테두리를 초록으로.
    /// </summary>
    public sealed class TriGrabCardFace
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/TriGrabCard";
        /// <summary>패널 크기(px). 패널 설정이 1 m = 100 px라 지름 1.2 m.</summary>
        public static readonly Vector2 PanelPixels = new Vector2(120f, 120f);
        /// <summary>배율 1일 때 지름(m).</summary>
        public const float PanelMeters = 1.2f;

        private static PanelSettings panelSettings;
        private static VisualTreeAsset uxml;

        private readonly GameObject go;
        private readonly VisualElement card;
        private readonly Label word;
        private readonly VisualElement flash;
        private int shownWord = -1, shownInk = -1, shownFill = -1;
        private bool shown = true;

        private TriGrabCardFace(GameObject go, VisualElement root)
        {
            this.go = go;
            card = root.Q<VisualElement>("card");
            word = root.Q<Label>("word");
            flash = root.Q<VisualElement>("flash");
        }

        public GameObject GameObject => go;

        /// <summary>리소스가 없으면 null(카드 면만 안 보인다 — 게임은 계속된다).</summary>
        public static TriGrabCardFace Create(string name, Transform parent)
        {
            if (panelSettings == null)
            {
                panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
                uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            }
            if (panelSettings == null || uxml == null) return null;
            //  패널 값을 정한 뒤 OnEnable이 패널을 만들도록 꺼 둔 채 구성하고 켠다.
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.worldSpaceSize = PanelPixels;
            go.SetActive(true);
            var root = document.rootVisualElement;
            return root?.Q<VisualElement>("card") == null ? null : new TriGrabCardFace(go, root);
        }

        /// <summary>
        /// 면을 그 자리에 그린다. <paramref name="okGlow"/>면 초록 테두리(결과 페이즈의 정답), <paramref name="dim"/>이면 흐리게
        /// (결과 페이즈의 틀린 카드), <paramref name="pop01"/>은 새 카드 번쩍임(1 → 0).
        /// </summary>
        public void Show(LOP.TriGrab.TriGrabCard c, Vector3 at, Quaternion facing, float diameter, bool okGlow, bool dim, float pop01)
        {
            if (go == null) return;
            SetShown(true);
            go.transform.SetPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (diameter / PanelMeters);

            if (c.Word != shownWord || c.Ink != shownInk)
            {
                shownWord = c.Word;
                shownInk = c.Ink;
                word.text = TriGrabPalette.ColorName(c.Word);
                word.style.color = TriGrabPalette.Ink(c.Ink);
            }
            if (c.Fill != shownFill)
            {
                shownFill = c.Fill;
                card.style.backgroundColor = TriGrabPalette.Fill(c.Fill);
            }

            var border = okGlow ? TriGrabPalette.Correct : new Color(1f, 1f, 1f, 0.5f);
            float width = okGlow ? 12f : 5f;
            card.style.borderTopColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderRightColor = border;
            card.style.borderTopWidth = width;
            card.style.borderBottomWidth = width;
            card.style.borderLeftWidth = width;
            card.style.borderRightWidth = width;
            card.style.opacity = dim ? 0.25f : 1f;

            flash.style.opacity = Mathf.Clamp01(pop01) * 0.7f;
        }

        //  GameObject를 껐다 켜면 UIDocument가 트리를 새로 지어 잡아 둔 요소가 끊긴다 — 켠 채 숨긴다.
        public void Hide() => SetShown(false);

        private void SetShown(bool on)
        {
            if (shown == on || card == null) return;
            shown = on;
            card.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Destroy()
        {
            if (go == null) return;
            if (UnityEngine.Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
