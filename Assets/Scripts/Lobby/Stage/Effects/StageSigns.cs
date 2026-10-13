using LOP.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 로비 터널 간판 — 보 위 전광판, 기둥 현수막 4장, 출구 간판, 관중석 티포.
    /// 경기 이름표와 같은 월드 공간 UI Toolkit 패널(100px = 1m)에 그려 키트 글꼴·티어 도형을 그대로 쓴다.
    /// 랭크 카드가 바뀌면 현수막·티포 글자와 색만 다시 넣는다.
    /// </summary>
    public sealed class StageSigns : MonoBehaviour
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/StageSigns";

        private readonly System.Collections.Generic.List<UIDocument> _docs = new System.Collections.Generic.List<UIDocument>();
        private float _weight = -1f;
        private StageSignText _text = StageSignText.From(default);

        public static StageSigns Build(Transform parent, TunnelLayout layout)
        {
            var go = new GameObject("StageSigns");
            go.transform.SetParent(parent, false);
            var signs = go.AddComponent<StageSigns>();
            var panel = Resources.Load<PanelSettings>(PanelSettingsResource);
            var uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            if (panel == null || uxml == null)
            {
                Debug.LogWarning($"[StageSigns] 리소스를 못 찾아 간판 없이 간다: {PanelSettingsResource} / {UxmlResource}");
                return signs;
            }
            float px = layout.Width / 2f - layout.PillarSize / 2f - 0.26f;
            signs.Add(panel, uxml, "board", new Vector2(720, 180), new Vector3(0f, layout.BeamY - 0.65f, 11.6f), 0f, 3.6f / 7.2f);   // 뒤쪽 보 바로 앞 — 앞 보(z=9)는 히어로 카메라에 너무 커 중계 표시를 가렸다
            foreach (float z in new[] { 6f, 12f })
                foreach (float s in new[] { -1f, 1f })
                    signs.Add(panel, uxml, "pennant", new Vector2(140, 350), new Vector3(s * px, 2.6f, z), s * 90f, 0.7f / 1.4f);
            signs.Add(panel, uxml, "exit", new Vector2(840, 210), new Vector3(0f, layout.Height - 0.62f, layout.Length - 0.6f), 0f, 4.2f / 8.4f);
            signs.Add(panel, uxml, "tifo", new Vector2(1400, 900), new Vector3(0f, 3.6f, layout.ArenaZ - 0.1f), 0f, 14f / 14f);
            signs.Apply();
            return signs;
        }

        private void Add(PanelSettings panel, VisualTreeAsset uxml, string name, Vector2 size, Vector3 position, float yaw, float scale)
        {
            var go = new GameObject($"Sign_{name}");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = uxml;
            doc.worldSpaceSize = size;
            go.SetActive(true);
            //  한 UXML에 네 간판이 다 있다 — 이 문서가 맡은 것만 남긴다.
            foreach (var n in new[] { "board", "pennant", "exit", "tifo" })
            {
                var e = doc.rootVisualElement.Q(n);
                if (e != null) e.style.display = n == name ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (name == "tifo") FillCrowd(doc.rootVisualElement.Q("tifo-crowd"));
            _docs.Add(doc);
        }

        private static void FillCrowd(VisualElement crowd)
        {
            if (crowd == null) return;
            for (int i = 0; i < 26 * 7; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("stage-tifo__dot");
                if (i * 7 % 4 == 0) dot.AddToClassList("stage-tifo__dot--face");
                crowd.Add(dot);
            }
        }

        public void SetRank(RankCard card)
        {
            _text = StageSignText.From(card);
            Apply();
        }

        private void Apply()
        {
            foreach (var doc in _docs)
            {
                var root = doc.rootVisualElement;
                root.Q<TierShapeElement>("pennant-tier")?.SetTier(_text.TierCode);
                root.Q<TierShapeElement>("tifo-tier")?.SetTier(_text.TierCode);
                var label = root.Q<Label>("pennant-label");
                if (label != null) label.text = _text.TierLabel;
                var tifoLabel = root.Q<Label>("tifo-label");
                if (tifoLabel != null) tifoLabel.text = _text.TierEnglish;
                var pennant = root.Q("pennant");
                if (pennant != null) pennant.style.backgroundColor = _text.TierColor;
                var flag = root.Q("tifo-flag");
                if (flag != null) flag.style.backgroundColor = _text.TierColor;
            }
        }

        public void SetWeight(float w)
        {
            if (Mathf.Approximately(w, _weight)) return;
            _weight = w;
            foreach (var doc in _docs) doc.rootVisualElement.style.opacity = w;
        }
    }
}
