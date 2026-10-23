using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 다이브 충전 연출(클라 전용, spec 2026-10-23-flappy-dive-charge §2). 날갯짓을 참고 빠르게 떨어지는 동안
    /// 새 둘레에 하늘색 기운이 감기고 위로 빛줄기가 끌린다 — 남의 새도 보인다. 내 새는 둘레 고리 게이지·충전음·"대시 1칸!".
    ///
    /// <para><b>판정·게이지와 무관하다.</b> 게이지는 시뮬(<see cref="FlappyDashSystem"/>)이 정한다. 충전 중인지는 시뮬과 같은 문턱으로
    /// 보되, 내 새는 시뮬 속도로, 남의 새는 그림 자리의 세로 속도로 본다(남의 새는 시뮬하지 않는다).</para>
    /// </summary>
    public class FlappyDiveFx : ITickable, System.IDisposable
    {
        private const float RingRadius = 0.78f;
        private const float RingWidth = 0.1f;
        private const int RingSegments = 48;
        private const int StreakCount = 6;
        private const float AuraSize = 2.2f;

        private static readonly Color Cyan = new Color(0.5f, 0.94f, 1f, 1f);

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyMapRulesField rules;
        private readonly FlappyConfig config;
        private readonly FlappyMoteFeed feed;

        private GameObject root;
        private Sprite glow;
        private Sprite streak;
        private Material lineMaterial;
        private LineRenderer ringBack, ringFill;
        private AudioSource hum;
        private AudioClip humClip, blipClip;
        private readonly Dictionary<string, Bird> birds = new Dictionary<string, Bird>();
        private readonly Dictionary<string, LOPEntityView> views = new Dictionary<string, LOPEntityView>();
        private float nextScan;
        private float time;
        private int lastStacks = -1;

        //  테스트 seam — 실제로는 씬의 LOPEntityView 그림, 테스트는 가짜를 끼운다.
        internal System.Func<string, UnityEngine.Transform> viewLookup;
        internal float StrengthOf(string entityId) => birds.TryGetValue(entityId, out var b) ? b.Strength : 0f;
        internal float AuraAlphaOf(string entityId) => birds.TryGetValue(entityId, out var b) && b.Aura != null ? b.Aura.color.a : 0f;
        internal float RingFraction { get; private set; }

        private class Bird
        {
            public UnityEngine.Transform View;
            public float LastY;
            public bool HasLast;
            public float RenderVy;
            public float Strength;   // 0 = 충전 안 함, 0.4~1 = 충전 세기
            public SpriteRenderer Aura;
            public SpriteRenderer[] Streaks;
            public float[] Phase;
        }

        public FlappyDiveFx(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                            FlappyMapRulesField rules, FlappyConfig config, FlappyMoteFeed feed)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.rules = rules;
            this.config = config;
            this.feed = feed;
            viewLookup = DefaultViewLookup;
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            if (rules.DiveCharge == false)
            {
                ClearAll();
                return;
            }
            time += deltaTime;
            EnsureRoot();
            float min = config.MaxFallSpeed * rules.DiveMinFallRatio;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var view = viewLookup(entity.Id);
                if (view == null)
                {
                    continue;
                }
                if (birds.TryGetValue(entity.Id, out var bird) == false)
                {
                    bird = MakeBird();
                    birds[entity.Id] = bird;
                }
                bird.View = view;
                bool mine = entity.Id == playerContext.entityId;
                Vector3 center = view.position + Vector3.up * config.BodyRadius;

                //  충전 세기: 시뮬과 같은 문턱. 내 새는 시뮬 속도, 남의 새는 그림이 움직인 세로 속도(부드럽게).
                if (bird.HasLast && deltaTime > 0f)
                {
                    float vy = (center.y - bird.LastY) / deltaTime;
                    bird.RenderVy = Mathf.Lerp(bird.RenderVy, vy, Mathf.Min(1f, deltaTime * 20f));
                }
                bird.LastY = center.y;
                bird.HasLast = true;
                float fall = mine ? -(entity.Get<GameFramework.World.Velocity>()?.Linear.Y ?? 0f) : -bird.RenderVy;
                bool dashing = (entity.Get<FlappyDash>()?.DashRemaining ?? 0f) > 0f;
                bool stunned = (entity.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;
                float target = 0f;
                if (dashing == false && stunned == false && fall >= min && config.MaxFallSpeed > min)
                {
                    float k = Mathf.Clamp01((fall - min) / (config.MaxFallSpeed - min));
                    target = 0.4f + 0.6f * k;
                }
                bird.Strength = target;
                AnimateBird(bird, center, deltaTime);

                if (mine)
                {
                    UpdateMine(entity, center, bird.Strength, deltaTime);
                }
            }
        }

        private void AnimateBird(Bird bird, Vector3 center, float deltaTime)
        {
            bool on = bird.Strength > 0f;
            var ac = bird.Aura.color;
            ac.a = Mathf.MoveTowards(ac.a, on ? 0.35f + 0.4f * bird.Strength : 0f, deltaTime * 6f);
            bird.Aura.color = ac;
            bird.Aura.transform.position = new Vector3(center.x, center.y, center.z + 0.1f);
            float size = AuraSize * (1f + 0.3f * bird.Strength) + 0.15f * Mathf.Sin(time * 18f);
            bird.Aura.transform.localScale = Vector3.one * size;
            //  빛줄기: 떨어지는 반대(위)로 끌린다 — 몸 둘레에서 출발해 위로 흐르다 사라진다.
            for (int i = 0; i < bird.Streaks.Length; i++)
            {
                bird.Phase[i] = (bird.Phase[i] + deltaTime * (2.5f + 2f * bird.Strength)) % 1f;
                float ang = i / (float)bird.Streaks.Length * Mathf.PI * 2f;
                var st = bird.Streaks[i];
                st.transform.position = new Vector3(center.x + Mathf.Cos(ang) * 0.45f - 0.15f,
                                                    center.y + 0.4f + bird.Phase[i] * 1.8f, center.z + 0.2f);
                var sc = Cyan;
                sc.a = on ? Mathf.Sin(bird.Phase[i] * Mathf.PI) * (0.4f + 0.5f * bird.Strength) : 0f;
                st.color = sc;
            }
        }

        private void UpdateMine(GameFramework.World.Entity entity, Vector3 center, float strength, float deltaTime)
        {
            float charge = entity.Get<FlappyDash>()?.Charge ?? 0f;
            int stacks = Mathf.FloorToInt(charge + 1e-6f);
            float fraction = charge >= FlappyDash.MaxCharge ? 1f : charge - stacks;
            RingFraction = fraction;
            DrawRing(ringBack, center, 1f, new Color(1f, 1f, 1f, 0.12f));
            var fc = Cyan;
            fc.a = 0.6f + 0.4f * strength;
            DrawRing(ringFill, center, fraction, fc);

            //  충전음: 차는 동안 웅, 칸 안 몫이 찰수록 높아진다.
            float vol = strength > 0f && fraction < 1f ? 0.12f * (0.5f + strength) : 0f;
            hum.volume = Mathf.MoveTowards(hum.volume, vol, deltaTime * 2f);
            hum.pitch = 0.8f + fraction * 1.2f + strength * 0.25f;
            if (hum.volume > 0f && hum.isPlaying == false) { hum.Play(); }

            //  칸이 막 찼다 — 글자와 짧은 소리(버튼 튕김은 FlapPadView가 한다).
            if (lastStacks >= 0 && stacks > lastStacks)
            {
                feed.Publish(new FlappyMotePopup(center + Vector3.up * 1.4f, "대시 1칸!", FlappyMotePopupKind.Count));
                hum.PlayOneShot(blipClip, 0.4f);
            }
            lastStacks = stacks;
        }

        private static void DrawRing(LineRenderer line, Vector3 center, float fraction, Color color)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(RingSegments * Mathf.Clamp01(fraction)) + 1);
            line.enabled = fraction > 0.005f;
            if (line.enabled == false)
            {
                return;
            }
            line.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                //  12시에서 시계 방향으로.
                float a = Mathf.PI * 0.5f - Mathf.PI * 2f * fraction * i / (n - 1);
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * RingRadius, center.y + Mathf.Sin(a) * RingRadius, center.z - 0.05f));
            }
            line.startColor = line.endColor = color;
        }

        private Bird MakeBird()
        {
            var bird = new Bird { Streaks = new SpriteRenderer[StreakCount], Phase = new float[StreakCount] };
            bird.Aura = MakeSprite("DiveAura", glow, 1, new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
            for (int i = 0; i < StreakCount; i++)
            {
                bird.Streaks[i] = MakeSprite("DiveStreak", streak, 2, new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
                bird.Streaks[i].transform.localScale = new Vector3(1f, 1.4f, 1f);
                bird.Phase[i] = i / (float)StreakCount;
            }
            return bird;
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = color;
            return r;
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }
            root = new GameObject("FlappyDiveFx");
            glow = MakeGlow();
            streak = MakeStreak();
            lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "FlappyDiveRing" };
            ringBack = MakeLine("DiveRingBack");
            ringFill = MakeLine("DiveRingFill");
            hum = root.AddComponent<AudioSource>();
            hum.playOnAwake = false;
            hum.loop = true;
            hum.spatialBlend = 0f;
            hum.volume = 0f;
            humClip = MakeHum();
            blipClip = MakeBlip();
            hum.clip = humClip;
        }

        private LineRenderer MakeLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.widthMultiplier = RingWidth;
            line.numCapVertices = 2;
            line.sortingOrder = 3;
            line.enabled = false;
            return line;
        }

        //  번지는 빛 한 점(가운데 밝고 가장자리로 사라짐).
        private static Sprite MakeGlow()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
                }
            }
            return SpriteFrom(pixels, size, size, "FlappyDiveGlow");
        }

        //  가는 빛줄기 한 줄: 가운데가 밝고 위아래 끝이 사라진다.
        private static Sprite MakeStreak()
        {
            const int w = 8, h = 64;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Sin(Mathf.PI * y / (h - 1));
                for (int x = 0; x < w; x++)
                {
                    float e = 1f - Mathf.Abs(x - (w - 1) * 0.5f) / (w * 0.5f);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a * e));
                }
            }
            return SpriteFrom(pixels, w, h, "FlappyDiveStreak");
        }

        private static Sprite SpriteFrom(Color32[] pixels, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
        }

        //  충전 웅 소리 — 끊김 없이 되풀이되는 부드러운 세모파(1초, 정수 주기라 이음매가 없다).
        private static AudioClip MakeHum()
        {
            const int rate = 44100;
            var data = new float[rate];
            for (int i = 0; i < rate; i++)
            {
                float t = i / (float)rate;
                data[i] = 0.6f * Mathf.Sin(2f * Mathf.PI * 220f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 440f * t);
            }
            var clip = AudioClip.Create("FlappyDiveHum", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeBlip()
        {
            const int rate = 44100;
            int length = (int)(rate * 0.18f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float f = t < 0.08f ? 880f : 1320f;
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 14f) * 0.8f;
            }
            var clip = AudioClip.Create("FlappyDiveSlot", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        //  찾은 뷰는 들고 있고, 못 찾은 새가 있으면 씬 훑기는 0.5초에 한 번만. 그림이 아직 안 붙었으면 null(루트는 안 움직인다).
        private UnityEngine.Transform DefaultViewLookup(string entityId)
        {
            if (views.TryGetValue(entityId, out var view) == false || view == null)
            {
                if (time < nextScan)
                {
                    return null;
                }
                nextScan = time + 0.5f;
                views.Clear();
                foreach (var v in Object.FindObjectsByType<LOPEntityView>(FindObjectsSortMode.None))
                {
                    if (string.IsNullOrEmpty(v.entityId) == false)
                    {
                        views[v.entityId] = v;
                    }
                }
                if (views.TryGetValue(entityId, out view) == false)
                {
                    return null;
                }
            }
            return view.visualGameObject != null ? view.visualGameObject.transform : null;
        }

        private void ClearAll()
        {
            if (birds.Count == 0 && root == null)
            {
                return;
            }
            foreach (var bird in birds.Values)
            {
                DestroyObject(bird.Aura != null ? bird.Aura.gameObject : null);
                foreach (var st in bird.Streaks) { DestroyObject(st != null ? st.gameObject : null); }
            }
            birds.Clear();
            if (ringBack != null) { ringBack.enabled = false; }
            if (ringFill != null) { ringFill.enabled = false; }
            if (hum != null) { hum.volume = 0f; hum.Stop(); }
            lastStacks = -1;
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }
            if (UnityEngine.Application.isPlaying) { Object.Destroy(obj); }
            else { Object.DestroyImmediate(obj); }
        }

        public void Dispose()
        {
            ClearAll();
            DestroyObject(root);
            root = null;
            foreach (var sprite in new[] { glow, streak })
            {
                if (sprite != null)
                {
                    var tex = sprite.texture;
                    DestroyObject(sprite);
                    DestroyObject(tex);
                }
            }
            glow = streak = null;
            DestroyObject(lineMaterial);
            DestroyObject(humClip);
            DestroyObject(blipClip);
            lineMaterial = null;
            humClip = blipClip = null;
            views.Clear();
        }
    }
}
