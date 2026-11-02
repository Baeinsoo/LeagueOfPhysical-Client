using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 몸마다 머리 위에 위험도 "N%"를 띄우고 몸 가장자리 빛도 같은 색으로 물들인다(많이 맞을수록 멀리 날아간다는 걸 한눈에). 이름표(CharacterNameplate)와 같은
    /// 월드 공간 UI 패널 방식 — 화면을 보는 방향으로 세우고 거리에 비례해 키워 화면에서 크기가 일정하다.
    /// 값은 몸의 KnockbackBody.DamagePercent다: 남은 서버 스냅이 쓰고, 나는 예측한 뒤 서버 결과로 보정된다.
    /// </summary>
    public class KnockbackDamageLabel : IStartable, System.IDisposable
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/KnockbackDamageLabel";
        // 이름표(머리 위 2.0)보다 위에 둔다.
        private static readonly Vector3 HeadOffset = new Vector3(0f, 2.7f, 0f);
        private const float ScreenSizeFactor = 0.075f;

        public const int YellowAt = 60;
        public const int OrangeAt = 120;
        public const int RedAt = 180;
        private const float BlinkPerSecond = 3f;
        private const string ToonShaderName = "LOP/Toon";
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");

        public static readonly Color YellowColor = new Color(1f, 0.85f, 0.2f);
        public static readonly Color OrangeColor = new Color(1f, 0.55f, 0.15f);
        public static readonly Color RedColor = new Color(1f, 0.2f, 0.18f);

        private class Entry
        {
            public GameObject go;
            public Label label;
            public int shown = -1;
            public Renderer[] bodyRenderers;
            public Color[] baseRimColors;
            public float[] baseRimStrengths;
            public GameObject bodyOwner;
            public float appliedTint = -1f;
            public bool appliedBlinkOn = true;
        }

        private readonly ActorRegistry actors;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly KnockbackClientState state;

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> gone = new List<string>();
        private PanelSettings panelSettings;
        private VisualTreeAsset uxml;
        private Camera camera;
        private KnockbackLateUpdateDriver driver;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public KnockbackDamageLabel(ActorRegistry actors, GameFramework.World.EntityRegistry entityRegistry, KnockbackClientState state)
        {
            this.actors = actors;
            this.entityRegistry = entityRegistry;
            this.state = state;
        }

        /// <summary>위험도 색: 0 흰색 → 60 노랑 → 120 주황 → 180 이상 빨강, 사이는 이어서 섞는다.</summary>
        public static Color ColorFor(int percent)
        {
            if (percent <= 0) return Color.white;
            if (percent < YellowAt) return Color.Lerp(Color.white, YellowColor, percent / (float)YellowAt);
            if (percent < OrangeAt) return Color.Lerp(YellowColor, OrangeColor, (percent - YellowAt) / (float)(OrangeAt - YellowAt));
            if (percent < RedAt) return Color.Lerp(OrangeColor, RedColor, (percent - OrangeAt) / (float)(RedAt - OrangeAt));
            return RedColor;
        }

        /// <summary>180 이상이면 숫자와 몸이 깜박인다(그림만).</summary>
        public static bool IsBlinking(int percent) => percent >= RedAt;

        /// <summary>몸 가장자리 빛을 원래 값에서 위험도 색으로 얼마나 옮길지(0이면 원래 그대로, 180 이상 1).</summary>
        public static float BodyTintAmount(int percent) => Mathf.Clamp01(percent / (float)RedAt);

        private static bool BlinkPhaseOn(float time) => ((int)(time * BlinkPerSecond * 2f) & 1) == 0;

        // 몸(보간기 0)과 카메라(3000)가 다 움직인 뒤에 놓아야 한 프레임 늦어 떨리지 않는다.
        public void Start()
        {
            driver = KnockbackLateUpdateDriver.Create("KnockbackDamageLabelDriver", Place);
        }

        private void Place()
        {
            if (camera == null)
            {
                camera = Camera.main;
                if (camera == null) return;
            }
            if (panelSettings == null)
            {
                panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
                uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
                if (panelSettings == null || uxml == null)
                {
                    return;   // 리소스가 없으면 라벨만 못 띄운다 — 게임은 계속된다
                }
            }

            foreach (var actor in actors.All)
            {
                string id = actor.entityId;
                var model = actor.visualGameObject;
                bool eliminated = state.IsEliminated(id);
                if (!entries.TryGetValue(id, out var entry))
                {
                    if (model == null || eliminated) continue;
                    entry = Create(id);
                    entries[id] = entry;
                }

                bool visible = model != null && !eliminated;
                if (entry.go.activeSelf != visible) entry.go.SetActive(visible);
                if (!visible) continue;

                int percent = entityRegistry.Get(id)?.Get<KnockbackBody>()?.DamagePercent ?? 0;
                bool blinkOn = !IsBlinking(percent) || BlinkPhaseOn(Time.unscaledTime);
                if (percent != entry.shown)
                {
                    entry.shown = percent;
                    entry.label.text = percent + "%";
                }
                entry.label.style.color = blinkOn ? ColorFor(percent) : Color.white;
                ApplyBodyTint(entry, model, percent, blinkOn);

                var position = model.transform.position + HeadOffset;
                entry.go.transform.position = position;
                // 카메라 회전을 그대로 복사하면 어느 위치에서든 화면과 평행해 글자가 눕지 않는다.
                entry.go.transform.rotation = camera.transform.rotation;
                entry.go.transform.localScale = Vector3.one * (Vector3.Distance(position, camera.transform.position) * ScreenSizeFactor);
            }

            // 몸이 없어진 라벨은 치운다.
            gone.Clear();
            foreach (var pair in entries)
            {
                if (!actors.Contains(pair.Key)) gone.Add(pair.Key);
            }
            for (int i = 0; i < gone.Count; i++)
            {
                if (entries[gone[i]].go != null) Object.Destroy(entries[gone[i]].go);
                entries.Remove(gone[i]);
            }
        }

        // 옷 색이 든 블록을 지우지 않으려고 받아서 가장자리 빛 두 값만 바꿔 다시 넣는다.
        private void ApplyBodyTint(Entry entry, GameObject model, int percent, bool blinkOn)
        {
            if (entry.bodyOwner != model)
            {
                CacheBody(entry, model);
            }
            if (entry.bodyRenderers.Length == 0) return;
            float tint = blinkOn ? BodyTintAmount(percent) : 0f;
            if (Mathf.Approximately(tint, entry.appliedTint)) return;
            entry.appliedTint = tint;

            var color = ColorFor(percent);
            for (int i = 0; i < entry.bodyRenderers.Length; i++)
            {
                var r = entry.bodyRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(RimColorId, Color.Lerp(entry.baseRimColors[i], color, tint));
                block.SetFloat(RimStrengthId, Mathf.Lerp(entry.baseRimStrengths[i], 1f, tint));
                r.SetPropertyBlock(block);
            }
        }

        private static void CacheBody(Entry entry, GameObject model)
        {
            entry.bodyOwner = model;
            entry.appliedTint = -1f;
            var found = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && m.shader != null && m.shader.name == ToonShaderName) found.Add(r);
            }
            entry.bodyRenderers = found.ToArray();
            entry.baseRimColors = new Color[found.Count];
            entry.baseRimStrengths = new float[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                var m = found[i].sharedMaterial;
                entry.baseRimColors[i] = m.GetColor(RimColorId);
                entry.baseRimStrengths[i] = m.GetFloat(RimStrengthId);
            }
        }

        private Entry Create(string id)
        {
            // 패널 값을 정한 뒤 OnEnable이 패널을 만들도록 꺼 둔 채 구성하고 켠다.
            var go = new GameObject($"KnockbackDamage_{id}");
            go.SetActive(false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.worldSpaceSize = new Vector2(160f, 60f);
            go.SetActive(true);
            return new Entry { go = go, label = document.rootVisualElement.Q<Label>("percent-text") };
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            foreach (var entry in entries.Values)
            {
                if (entry.go != null) Object.Destroy(entry.go);
            }
            entries.Clear();
        }
    }
}
