using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 카드 한 장의 그림(월드 공간 UI 패널 — 이름표와 같은 방식이라 한글이 그대로 나온다).
    /// 흰 판 1.6 × 1.1 m에 카드 모양(원·세모·네모)을 회색으로 크게 깔고 그 위에 글자를 글자색으로 쓴다.
    /// 도는 카드는 점 셋(지금 색을 키운다), 잡힌 카드는 정답 초록·오답 빨강 테두리.
    /// </summary>
    public sealed class TriGrabCardFace
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/TriGrabCard";
        /// <summary>패널 크기(px). 패널 설정이 1 m = 100 px라 1.6 × 1.1 m가 된다.</summary>
        public static readonly Vector2 PanelPixels = new Vector2(160f, 110f);

        private static PanelSettings panelSettings;
        private static VisualTreeAsset uxml;

        private readonly GameObject go;
        private readonly VisualElement card;
        private readonly Label word;
        private readonly VisualElement dots;
        private readonly VisualElement[] dot = new VisualElement[3];
        private readonly VisualElement flash;
        private readonly TriGrabShapeGlyph glyph;
        private int shownWord = -1, shownInk = -1;
        private static readonly Color GlyphFill = new Color(0.74f, 0.77f, 0.85f);
        private static readonly Color GlyphStroke = new Color(0.2f, 0.23f, 0.33f);
        private bool shown = true;

        private TriGrabCardFace(GameObject go, VisualElement root)
        {
            this.go = go;
            card = root.Q<VisualElement>("card");
            word = root.Q<Label>("word");
            dots = root.Q<VisualElement>("dots");
            for (int i = 0; i < 3; i++) dot[i] = root.Q<VisualElement>("dot" + i);
            flash = root.Q<VisualElement>("flash");
            //  글자 뒤에 깔리게 맨 앞에 넣는다.
            glyph = new TriGrabShapeGlyph { name = "shape" };
            glyph.style.position = Position.Absolute;
            glyph.style.left = 2; glyph.style.right = 2; glyph.style.top = 1; glyph.style.bottom = 1;
            card.Insert(0, glyph);
        }

        public GameObject GameObject => go;

        /// <summary>리소스가 없으면 null(카드만 안 보인다 — 게임은 계속된다).</summary>
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
        /// 카드를 그 자리에 그린다. <paramref name="faceUp"/>가 false면 흰 판만(준비 중 — 주문을 먼저 읽게).
        /// <paramref name="glow"/>는 잡힌 결과, <paramref name="pop01"/>은 새 카드 번쩍임(1 → 0).
        /// </summary>
        public void Show(in TriGrabCard c, int ink, bool faceUp, Vector3 at, Quaternion facing, float scale,
                         TriGrabHookResult glow, float pop01)
        {
            if (go == null) return;
            SetShown(true);
            go.transform.SetPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * scale;

            word.style.display = faceUp ? DisplayStyle.Flex : DisplayStyle.None;
            glyph.Set(faceUp ? c.Shape : -1, GlyphFill, GlyphStroke, 9f);
            if (faceUp && (c.Word != shownWord || ink != shownInk))
            {
                shownWord = c.Word;
                shownInk = ink;
                word.text = TriGrabWords.Name(c.Word);
                word.style.color = TriGrabPalette.Ink(ink);
            }

            bool cycling = faceUp && c.Cycles;
            dots.style.display = cycling ? DisplayStyle.Flex : DisplayStyle.None;
            if (cycling)
            {
                for (int i = 0; i < 3; i++)
                {
                    bool now = i == ink;
                    float size = now ? 22f : 12f;
                    dot[i].style.width = size;
                    dot[i].style.height = size;
                    dot[i].style.backgroundColor = now ? TriGrabPalette.Ink(i) : new Color(0.72f, 0.74f, 0.8f);
                }
            }

            var border = glow == TriGrabHookResult.Correct ? TriGrabPalette.Correct
                       : glow == TriGrabHookResult.Wrong ? TriGrabPalette.Wrong
                       : new Color(0.24f, 0.27f, 0.39f);
            float width = glow == TriGrabHookResult.None ? 5f : 12f;
            card.style.borderTopColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderRightColor = border;
            card.style.borderTopWidth = width;
            card.style.borderBottomWidth = width;
            card.style.borderLeftWidth = width;
            card.style.borderRightWidth = width;

            flash.style.opacity = Mathf.Clamp01(pop01);
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
