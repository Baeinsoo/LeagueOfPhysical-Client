using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>공용 시상대 장식 — 현수막·단 번호(월드 공간 UI), 등장 때 색종이(일반) 또는 서치라이트(랭크).</summary>
    public sealed class PodiumDecor : MonoBehaviour
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/PodiumSigns";
        private const int PaperCount = 80;
        //  월드 공간 패널은 100px = 1m — 현수막 680px를 6.8m로 두면 너무 크다.
        private const float BannerScale = 0.6f;
        private const float StepLabelScale = 0.5f;

        private readonly List<(Transform t, float speed, float spin, float x)> _paper = new List<(Transform, float, float, float)>();
        private readonly List<(Transform pivot, float phase, float dir)> _beams = new List<(Transform, float, float)>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private bool _revealed;
        private float _t;

        public int PaperPieces => _paper.Count;
        public int Beams => _beams.Count;

        public void Build(PodiumContext context, IReadOnlyList<PodiumSlot> slots)
        {
            var panel = Resources.Load<PanelSettings>(PanelSettingsResource);
            var uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            if (panel != null && uxml != null)
            {
                var banner = Sign(panel, uxml, "banner", new Vector2(680, 90), new Vector3(PodiumLayout.Center.x, 3.4f, 1.5f), BannerScale);
                banner.transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
                banner.rootVisualElement.Q("banner")?.EnableInClassList("podium-banner--ranked", context.Ranked);
                var label = banner.rootVisualElement.Q<Label>("banner-label");
                if (label != null) label.text = context.Ranked ? "리그 경기 · 시상식" : "피지컬 리그 · 시상식";

                for (int i = 0; i < slots.Count; i++)
                {
                    if (!slots[i].OnStep) continue;
                    var front = new Vector3(slots[i].Position.x, slots[i].Position.y * 0.5f, PodiumLayout.Center.z - PodiumLayout.StepDepth / 2f - 0.01f);
                    var doc = Sign(panel, uxml, "step", new Vector2(120, 120), front, StepLabelScale);
                    var n = doc.rootVisualElement.Q<Label>("step-label");
                    if (n != null) n.text = slots[i].StepLabel;
                }
            }
            else
            {
                Debug.LogWarning($"[PodiumDecor] 리소스를 못 찾아 글자 없이 간다: {PanelSettingsResource} / {UxmlResource}");
            }

            if (context.Ranked) BuildBeams();
            else BuildConfetti();
        }

        private UIDocument Sign(PanelSettings panel, VisualTreeAsset uxml, string name, Vector2 size, Vector3 position, float scale)
        {
            var go = new GameObject($"Sign_{name}");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = uxml;
            doc.worldSpaceSize = size;
            go.SetActive(true);
            //  한 UXML에 두 글자판이 다 있다 — 이 문서가 맡은 것만 남긴다.
            foreach (var n in new[] { "banner", "step" })
            {
                var e = doc.rootVisualElement.Q(n);
                if (e != null) e.style.display = n == name ? DisplayStyle.Flex : DisplayStyle.None;
            }
            return doc;
        }

        private void BuildConfetti()
        {
            var colors = new[] { "#E8322F", "#FFD23F", "#FF8A00", "#4A9FE0", "#FFF8E8" };
            var mats = new Material[colors.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = LobbyTunnel.MakeGlowMaterial(colors[i]);
            var rnd = new System.Random(11);   // 같은 씨앗 — 미리보기·시험이 같은 그림
            for (int i = 0; i < PaperCount; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = $"Paper_{i}";
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(transform, false);
                float x = PodiumLayout.Center.x + ((float)rnd.NextDouble() - 0.5f) * 7f;
                p.transform.localPosition = new Vector3(x, 6f + (float)rnd.NextDouble() * 3f, ((float)rnd.NextDouble() - 0.5f) * 3f);
                p.transform.localScale = new Vector3(0.08f, 0.12f, 0.004f);
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = mats[i % mats.Length];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.SetActive(false);
                _paper.Add((p.transform, 1.2f + (float)rnd.NextDouble() * 1.4f, (float)rnd.NextDouble() * 360f, x));
            }
        }

        private void BuildBeams()
        {
            var mesh = GlowMeshes.Cone(0.12f, 1.6f, 12f, 20);
            _meshes.Add(mesh);
            float[] xs = { PodiumLayout.Center.x - 3.2f, PodiumLayout.Center.x + 3.2f };
            for (int i = 0; i < xs.Length; i++)
            {
                var pivot = new GameObject($"Beam_{i}").transform;
                pivot.SetParent(transform, false);
                pivot.localPosition = new Vector3(xs[i], 0f, 3.5f);
                var cone = new GameObject("Cone");
                cone.transform.SetParent(pivot, false);
                cone.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = cone.AddComponent<MeshRenderer>();
                var mat = LobbyTunnel.MakeStageGlowMaterial(new Color(0.92f, 0.95f, 1f, 0.22f));
                if (mat.HasProperty("_FadeAlongV")) mat.SetFloat("_FadeAlongV", 0.8f);
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pivot.gameObject.SetActive(false);
                _beams.Add((pivot, i * 1.3f, i == 0 ? 1f : -1f));
            }
        }

        public void Reveal()
        {
            if (_revealed) return;
            _revealed = true;
            foreach (var p in _paper) p.t.gameObject.SetActive(true);
            foreach (var b in _beams) b.pivot.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_revealed) return;
            _t += Time.deltaTime;
            foreach (var p in _paper)
            {
                if (!p.t.gameObject.activeSelf) continue;
                var pos = p.t.localPosition;
                pos.y -= p.speed * Time.deltaTime;
                pos.x = p.x + Mathf.Sin(_t * 2f + p.spin) * 0.25f;
                if (pos.y < -0.2f)
                {
                    p.t.gameObject.SetActive(false);
                    continue;
                }
                p.t.localPosition = pos;
                p.t.localRotation = Quaternion.Euler(p.spin + _t * 200f, _t * 140f, 0f);
            }
            foreach (var b in _beams)
            {
                b.pivot.localRotation = Quaternion.Euler(0f, 0f, b.dir * (12f + Mathf.Sin(_t * 0.9f + b.phase) * 14f));
            }
        }

        private void OnDestroy()
        {
            LobbyTunnel.DestroyMaterials(gameObject);
            foreach (var m in _meshes)
            {
                if (m == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(m);
                else DestroyImmediate(m);
            }
        }
    }
}
