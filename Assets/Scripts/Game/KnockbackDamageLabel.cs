using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 몸마다 머리 위에 "N%"를 띄운다(많이 맞을수록 멀리 날아간다는 걸 한눈에). 이름표(CharacterNameplate)와 같은
    /// 월드 공간 UI 패널 방식 — 화면을 보는 방향으로 세우고 거리에 비례해 키워 화면에서 크기가 일정하다.
    /// 값은 몸의 KnockbackBody.DamagePercent다: 남은 서버 스냅이 쓰고, 나는 예측한 뒤 서버 결과로 보정된다.
    /// </summary>
    public class KnockbackDamageLabel : IStartable, System.IDisposable
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/KnockbackDamageLabel";
        // 이름표(머리 위 2.0)보다 위에 둔다.
        private static readonly Vector3 HeadOffset = new Vector3(0f, 2.7f, 0f);
        private const float ScreenSizeFactor = 0.1f;

        public const int YellowFrom = 48;
        public const int RedFrom = 80;

        private class Entry
        {
            public GameObject go;
            public Label label;
            public int shown = -1;
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

        public KnockbackDamageLabel(ActorRegistry actors, GameFramework.World.EntityRegistry entityRegistry, KnockbackClientState state)
        {
            this.actors = actors;
            this.entityRegistry = entityRegistry;
            this.state = state;
        }

        public static Color ColorFor(int percent)
        {
            if (percent >= RedFrom) return new Color(1f, 0.25f, 0.2f);
            if (percent >= YellowFrom) return new Color(1f, 0.85f, 0.2f);
            return Color.white;
        }

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
                if (percent != entry.shown)
                {
                    entry.shown = percent;
                    entry.label.text = percent + "%";
                    entry.label.style.color = ColorFor(percent);
                }

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
