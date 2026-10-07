using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 대시 연출(클라 전용). 내 새: 대시 시작에 카메라 흔들림 한 번 + 대시 중 화면 가로 속도선. 모든 새: 대시 중 불꽃 꼬리 —
    /// 남이 대시한 것도 읽힌다. 카메라 거리는 여기서 건드리지 않는다 — 내 대시 중 3m 빠지는 줌은
    /// <see cref="FlappyCameraDistance"/>가 따로 맡는다(2026-09-29 결정은 2026-10-07에 뒤집혔다). FOV는 여전히 안 바꾼다.
    /// 판정·시뮬과 무관하다.
    /// </summary>
    public class FlappyDashFx : ITickable, System.IDisposable
    {
        private const float ShakeSeconds = 0.15f;
        private const float ShakeAmplitude = 0.35f;
        private const float ShakeFrequency = 55f;   // 라디안/초 — 0.15초에 한두 번 흔들린다
        private const float LineFadeSeconds = 0.15f;
        private const int LineCount = 16;
        private const float LineSpeed = 2600f;       // 화면 픽셀/초
        private const float TrailSeconds = 0.25f;

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyConfig config;

        private readonly Dictionary<string, bool> wasDashing = new Dictionary<string, bool>();
        private readonly Dictionary<string, TrailRenderer> trails = new Dictionary<string, TrailRenderer>();
        private float shakeElapsed = -1f;
        private float lineAlpha;
        private GameObject overlay;
        private RectTransform[] lines;
        private Image[] lineImages;
        private Material trailMaterial;

        //  테스트 seam — 실제로는 씬의 LOPEntityView와 카메라, 테스트는 가짜를 끼운다.
        internal System.Func<string, UnityEngine.Transform> viewLookup;
        internal System.Action<Vector3> applyShake;
        internal int ShakeCount { get; private set; }
        internal float SpeedLineAlpha => lineAlpha;
        internal TrailRenderer TrailOf(string entityId) => trails.TryGetValue(entityId, out var t) ? t : null;

        public FlappyDashFx(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                            FlappyConfig config, CameraController cameraController)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
            viewLookup = DefaultViewLookup;
            applyShake = v => { if (cameraController != null) { cameraController.ShakeOffset = v; } };
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            bool mineDashing = false;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                float remaining = entity.Get<FlappyDash>()?.DashRemaining ?? 0f;
                bool dashing = remaining > 0f;
                bool mine = entity.Id == playerContext.entityId;
                wasDashing.TryGetValue(entity.Id, out bool before);
                //  시작은 화면 틱끼리 비교한다 — 되감기 재생 중 틱이 여러 번 돌아도 흔들림이 겹치지 않는다.
                if (mine && dashing && before == false)
                {
                    shakeElapsed = 0f;
                    ShakeCount++;
                }
                if (mine)
                {
                    mineDashing = dashing;
                }
                wasDashing[entity.Id] = dashing;
                UpdateTrail(entity.Id, dashing, remaining);
            }
            UpdateShake(deltaTime);
            UpdateLines(deltaTime, mineDashing);
        }

        private void UpdateShake(float deltaTime)
        {
            if (shakeElapsed < 0f)
            {
                return;
            }
            if (shakeElapsed >= ShakeSeconds)
            {
                shakeElapsed = -1f;
                applyShake(Vector3.zero);
                return;
            }
            float decay = 1f - shakeElapsed / ShakeSeconds;
            applyShake(new Vector3(Mathf.Sin(shakeElapsed * ShakeFrequency),
                                   Mathf.Cos(shakeElapsed * ShakeFrequency * 1.3f), 0f) * (ShakeAmplitude * decay));
            shakeElapsed += deltaTime;
        }

        private void UpdateLines(float deltaTime, bool dashing)
        {
            lineAlpha = dashing ? 1f : Mathf.MoveTowards(lineAlpha, 0f, deltaTime / LineFadeSeconds);
            if (lineAlpha <= 0f)
            {
                if (overlay != null) { overlay.SetActive(false); }
                return;
            }
            EnsureOverlay();
            overlay.SetActive(true);
            float width = Mathf.Max(1f, Screen.width);
            for (int i = 0; i < lines.Length; i++)
            {
                Vector2 p = lines[i].anchoredPosition;
                p.x -= LineSpeed * deltaTime;
                if (p.x < -lines[i].sizeDelta.x)
                {
                    p.x += width + lines[i].sizeDelta.x;
                    p.y = Random.Range(0f, Mathf.Max(1f, Screen.height));
                }
                lines[i].anchoredPosition = p;
                lineImages[i].color = new Color(1f, 1f, 1f, 0.55f * lineAlpha);
            }
        }

        private void EnsureOverlay()
        {
            if (overlay != null)
            {
                return;
            }
            overlay = new GameObject("FlappyDashSpeedLines");
            var canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            //  HUD(UI Toolkit)보다 아래 — 속도선이 버튼·게이지를 덮지 않게.
            canvas.sortingOrder = -50;
            lines = new RectTransform[LineCount];
            lineImages = new Image[LineCount];
            for (int i = 0; i < LineCount; i++)
            {
                var go = new GameObject("Line" + i);
                go.transform.SetParent(overlay.transform, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(Random.Range(90f, 200f), 3f);
                rect.anchoredPosition = new Vector2(Random.Range(0f, Mathf.Max(1f, Screen.width)),
                                                    Random.Range(0f, Mathf.Max(1f, Screen.height)));
                lines[i] = rect;
                lineImages[i] = image;
            }
        }

        private void UpdateTrail(string entityId, bool dashing, float remaining)
        {
            if (trails.TryGetValue(entityId, out var trail) == false || trail == null)
            {
                if (dashing == false)
                {
                    return;   // 대시한 적 없는 새에는 붙이지 않는다
                }
                UnityEngine.Transform view = viewLookup(entityId);
                if (view == null)
                {
                    return;
                }
                trail = view.gameObject.AddComponent<TrailRenderer>();
                trail.time = TrailSeconds;
                trail.minVertexDistance = 0.1f;
                trail.sharedMaterial = TrailMaterial();
                trail.colorGradient = FlameGradient();
                trail.emitting = false;
                trails[entityId] = trail;
            }
            trail.emitting = dashing;
            float ratio = config.DashDuration > 0f ? Mathf.Clamp01(remaining / config.DashDuration) : 0f;
            trail.widthMultiplier = 0.25f + 0.55f * ratio;
        }

        private Material TrailMaterial()
        {
            if (trailMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                trailMaterial = new Material(shader) { name = "FlappyDashTrail" };
            }
            return trailMaterial;
        }

        private static Gradient FlameGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static UnityEngine.Transform DefaultViewLookup(string entityId)
        {
            foreach (var view in Object.FindObjectsByType<LOPEntityView>(FindObjectsSortMode.None))
            {
                if (view.entityId == entityId)
                {
                    return view.visualGameObject != null ? view.visualGameObject.transform : view.transform;
                }
            }
            return null;
        }

        public void Dispose()
        {
            applyShake(Vector3.zero);
            foreach (var trail in trails.Values) { DestroyObject(trail); }
            trails.Clear();
            DestroyObject(overlay);
            overlay = null;
            DestroyObject(trailMaterial);
            trailMaterial = null;
        }

        //  플레이 중이 아니면(EditMode 테스트) Destroy가 안 먹어 DestroyImmediate로 갈아 끼운다.
        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }
            if (UnityEngine.Application.isPlaying) { Object.Destroy(obj); }
            else { Object.DestroyImmediate(obj); }
        }
    }
}
