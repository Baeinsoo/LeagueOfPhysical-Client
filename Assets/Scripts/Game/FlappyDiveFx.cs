using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 다이브 충전 연출(클라 전용, spec 2026-10-23-flappy-dive-charge §2). 날갯짓을 참고 빠르게 떨어지는 동안
    /// 새 둘레에 하늘색 기운이 감기고 위로 빛줄기가 끌린다 — 남의 새도 보인다. 내 새는 충전음·"대시 1칸!"·박아서 잃을 때 흩어짐.
    /// (둘레 고리 게이지는 10-24 사람 판 "굳이 없어도 된다"로 뺐다 — 게이지는 대시 버튼이 보여 준다.)
    ///
    /// <para><b>판정·게이지와 무관하다.</b> 게이지는 시뮬(<see cref="FlappyDashSystem"/>)이 정한다. 충전 중인지는 시뮬과 같은 문턱으로
    /// 보되, 내 새는 시뮬 속도로, 남의 새는 그림 자리의 세로 속도로 본다(남의 새는 시뮬하지 않는다).</para>
    /// </summary>
    public class FlappyDiveFx : ITickable, System.IDisposable
    {
        private const int StreakCount = 8;
        private const float AuraSize = 2.2f;

        private static readonly Color Cyan = new Color(0.5f, 0.94f, 1f, 1f);
        //  세기 단계 색(10-24 사람 판 "채워지는 정도에 따라 강약·색을 다르게"): 옅은 하늘 → 진한 하늘(최고 낙하). 금빛은 뺐다.
        private static readonly Color LightCyan = new Color(0.78f, 0.97f, 1f, 1f);
        private static readonly Color DeepCyan = new Color(0.22f, 0.78f, 1f, 1f);

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyMapRulesField rules;
        private readonly FlappyConfig config;
        private readonly FlappyMoteFeed feed;

        //  충전음(반짝 차오름, 10-26 사용자 선택): 작은 종소리가 5음 음계를 따라 올라간다 — 시제품
        //  dive-sounds.html의 HUM.chime과 같은 음계·박자·세기 공식.
        private static readonly float[] ChimeScale =
        {
            523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.66f, 1318.5f, 1568f, 1760f,
        };
        private const float ChimeMainDuration = 0.35f;
        private const float ChimeOctaveDuration = 0.2f;
        private const float ChimeOctaveRatio = 0.32f;   // 시제품: 옥타브 게인은 세기와 무관하게 원음의 ~0.32배로 거의 일정하다
        private const float ChimeDecayRate = 8f;        // 0.35초에 걸쳐 지수로 잦아든다(이전 삐- 소리와 같은 단순 지수 감쇠)
        //  시제품 웹오디오 게인(0.05~0.11)은 유니티 AudioSource 기준으로 작게 들려 기존 효과음(대시 1칸 0.4·흩어짐 0.25) 선에 맞춘다.
        private const float ChimeVolumeScale = 3f;

        private GameObject root;
        private Sprite glow;
        private Sprite streak;
        private AudioSource sfx;
        private AudioClip[] chimeClips;
        private AudioClip blipClip;
        private readonly Dictionary<string, Bird> birds = new Dictionary<string, Bird>();
        private readonly Dictionary<string, LOPEntityView> views = new Dictionary<string, LOPEntityView>();
        private float nextScan;
        private float time;
        private float nextChimeTime;        // this.time 기준 다음 종소리 시각(시제품의 next와 같다)
        private int announcedStacks = -1;   // "대시 1칸!"을 띄운 칸 수 — 경계에서 흔들려도 한 번만
        private float lastCharge = -1f;
        private bool wasStunned;
        private readonly List<Scatter> scatters = new List<Scatter>();

        //  박아서 잃은 게이지만큼 새에서 사방으로 흩어지는 알갱이.
        private class Scatter
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Age, Life;
        }

        //  테스트 seam — 실제로는 씬의 LOPEntityView 그림, 테스트는 가짜를 끼운다.
        internal System.Func<string, UnityEngine.Transform> viewLookup;
        internal float StrengthOf(string entityId) => birds.TryGetValue(entityId, out var b) ? b.Strength : 0f;
        internal float AuraAlphaOf(string entityId) => birds.TryGetValue(entityId, out var b) && b.Aura != null ? b.Aura.color.a : 0f;
        internal Color AuraColorOf(string entityId) => birds.TryGetValue(entityId, out var b) && b.Aura != null ? b.Aura.color : Color.clear;
        internal int ActiveStreaksOf(string entityId) => birds.TryGetValue(entityId, out var b) ? b.ActiveStreaks : 0;
        internal float AuraScaleOf(string entityId) => birds.TryGetValue(entityId, out var b) && b.Aura != null ? b.Aura.transform.localScale.x : 0f;
        internal Vector3 StreakPositionOf(string entityId, int index) => birds[entityId].Streaks[index].transform.position;
        internal int ScatterCount => scatters.Count;
        internal int ChimeNoteCount { get; private set; }   // 테스트 seam — 반짝 차오름 종소리가 울린 횟수

        private class Bird
        {
            public UnityEngine.Transform View;
            public float LastY;
            public bool HasLast;
            public float RenderVy;
            public float Strength;   // 0 = 충전 안 함, 0.4~1 = 충전 세기
            public bool Seen;        // 이번 프레임에 그림을 찾았나
            public Color Tint = Cyan; // 지금 색(단계 색으로 부드럽게 옮겨 간다)
            public int ActiveStreaks;
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
            foreach (var b in birds.Values) { b.Seen = false; }
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
                bird.Seen = true;
                bool mine = entity.Id == playerContext.entityId;
                Vector3 center = view.position + Vector3.up * config.BodyRadius;

                //  충전 세기: 시뮬과 같은 문턱. 내 새는 시뮬 속도, 남의 새는 그림이 움직인 세로 속도(부드럽게).
                if (bird.HasLast && deltaTime > 0f)
                {
                    float vy = (center.y - bird.LastY) / deltaTime;
                    //  스냅 교정·외삽 되돌림으로 그림이 한 프레임에 튀면(최고 낙하의 1.5배 넘게) 속도로 치지 않는다 — 가짜 기운이 깜빡인다.
                    if (Mathf.Abs(vy) <= config.MaxFallSpeed * 1.5f)
                    {
                        bird.RenderVy = Mathf.Lerp(bird.RenderVy, vy, Mathf.Min(1f, deltaTime * 20f));
                    }
                }
                bird.LastY = center.y;
                bird.HasLast = true;
                float fall = mine ? -(entity.Get<GameFramework.World.Velocity>()?.Linear.Y ?? 0f) : -bird.RenderVy;
                bool dashing = (entity.Get<FlappyDash>()?.DashRemaining ?? 0f) > 0f;
                bool stunned = (entity.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;   // 남의 새는 스냅이 스턴을 안 채워 늘 false
                //  10-26 사람 판: 게이지가 꽉 차도 다이빙 중엔 기운·빛줄기를 계속 보여준다 — 더 안 차는 것과 "떨어지는 느낌"은 별개다.
                //  소리만 끈다(UpdateMine의 fraction 문턱이 이미 꽉 찼을 때 종소리를 막는다).
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
                    UpdateMine(entity, center, bird.Strength);
                }
            }

            //  그림이 사라진 새(퇴장·그림 교체): 기운·빛줄기를 끄고, 엔티티가 없어졌으면 지운다.
            var gone = new List<string>();
            foreach (var pair in birds)
            {
                if (pair.Value.Seen)
                {
                    continue;
                }
                pair.Value.Strength = 0f;
                FadeOut(pair.Value, deltaTime);
                if (entityRegistry.Get(pair.Key) == null)
                {
                    gone.Add(pair.Key);
                }
            }
            foreach (var id in gone)
            {
                DestroyBird(birds[id]);
                birds.Remove(id);
            }
            AnimateScatters(deltaTime);
            //  10-26: 충전음이 한 번씩 튕기는 종소리(PlayOneShot)로 바뀌어 전처럼 계속 울리는 소리가 없다 —
            //  내 새가 안 보일 땐 UpdateMine을 안 불러 충전 조건이 저절로 꺼진다. mineSeen을 여기서 더 쓸 일이 없다.
        }

        private void FadeOut(Bird bird, float deltaTime)
        {
            var ac = bird.Aura.color;
            ac.a = Mathf.MoveTowards(ac.a, 0f, deltaTime * 6f);
            bird.Aura.color = ac;
            foreach (var st in bird.Streaks)
            {
                var sc = st.color;
                sc.a = 0f;
                st.color = sc;
            }
            bird.HasLast = false;   // 다시 붙을 때 낡은 높이로 튀지 않게
            bird.Tint = LightCyan;
        }

        private void DestroyBird(Bird bird)
        {
            DestroyObject(bird.Aura != null ? bird.Aura.gameObject : null);
            foreach (var st in bird.Streaks) { DestroyObject(st != null ? st.gameObject : null); }
        }

        /// <summary>세기(0 = 충전 안 함, 0.4 = 문턱 ~ 1 = 최고 낙하)를 0~1 단계로.</summary>
        internal static float Level(float strength) => strength <= 0f ? 0f : Mathf.Clamp01((strength - 0.4f) / 0.6f);

        /// <summary>단계 색: 옅은 하늘(문턱) → 진한 하늘(최고 낙하). 금빛·번쩍은 과해서 뺐다(10-24 사람 판) —
        /// 다이브엔 금빛 보너스가 없어 금색이 없는 보상을 약속하기도 했다.</summary>
        internal static Color TierColor(float level) => Color.Lerp(LightCyan, DeepCyan, Mathf.Clamp01(level));

        private void AnimateBird(Bird bird, Vector3 center, float deltaTime)
        {
            bool on = bird.Strength > 0f;
            float level = Level(bird.Strength);
            //  색은 단계 색으로 부드럽게 옮겨 간다. 꺼지면 다음 충전이 옅은 하늘부터 시작하게 그쪽으로 되돌린다.
            Color target = on ? TierColor(level) : LightCyan;
            bird.Tint = Color.Lerp(bird.Tint, target, Mathf.Min(1f, deltaTime * 12f));

            var ac = bird.Tint;
            float alpha = on ? 0.3f + 0.35f * level : 0f;
            ac.a = Mathf.MoveTowards(bird.Aura.color.a, Mathf.Clamp01(alpha), deltaTime * 6f);
            bird.Aura.color = ac;
            bird.Aura.transform.position = new Vector3(center.x, center.y, center.z + 0.1f);
            //  강약: 문턱에선 작게, 최고 낙하에선 몸 둘레보다 조금 크게 — 빠른 낙하 중이라 화면이 이미 바쁘다.
            float size = AuraSize * (0.75f + 0.45f * level) + 0.1f * Mathf.Sin(time * 18f);
            bird.Aura.transform.localScale = Vector3.one * size;

            //  빛줄기: 떨어지는 반대(위)로 끌린다. 세기에 따라 2줄 → 8줄, 빠르고 진하게.
            bird.ActiveStreaks = on ? 2 + Mathf.RoundToInt(6f * level) : 0;
            for (int i = 0; i < bird.Streaks.Length; i++)
            {
                bird.Phase[i] = (bird.Phase[i] + deltaTime * (2f + 4f * level)) % 1f;
                //  켜진 줄 수만큼 몸 둘레를 고르게 나눈다 — 고정 8칸으로 나누면 줄이 적을 때 한쪽에 몰린다.
                float ang = i / (float)Mathf.Max(1, bird.ActiveStreaks) * Mathf.PI * 2f;
                var st = bird.Streaks[i];
                st.transform.position = new Vector3(center.x + Mathf.Cos(ang) * 0.45f - 0.15f,
                                                    center.y + 0.4f + bird.Phase[i] * (1.4f + 0.8f * level), center.z + 0.2f);
                var sc = bird.Tint;
                sc.a = i < bird.ActiveStreaks ? Mathf.Sin(bird.Phase[i] * Mathf.PI) * (0.35f + 0.55f * level) : 0f;
                st.color = sc;
            }
        }

        private void UpdateMine(GameFramework.World.Entity entity, Vector3 center, float strength)
        {
            float charge = entity.Get<FlappyDash>()?.Charge ?? 0f;
            int stacks = Mathf.FloorToInt(charge + 1e-6f);
            float fraction = charge >= FlappyDash.MaxCharge ? 1f : charge - stacks;

            //  박아서 잃었다 — 스턴이 막 걸린 순간, 직전 프레임보다 줄어든 만큼 알갱이가 흩어진다.
            bool stunned = (entity.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;
            if (stunned && wasStunned == false && lastCharge >= 0f)
            {
                float lost = LostOnStun(lastCharge - charge, rules.StunLoss);
                if (lost > 0.01f)
                {
                    SpawnScatter(center, lost);
                }
            }
            wasStunned = stunned;
            lastCharge = charge;

            //  충전음(반짝 차오름, 10-26): 차는 동안(fraction < 1f가 꽉 찼을 때 끈다 — 기운은 그대로, 소리만) 종이
            //  (0.2 − 0.11×세기)초마다 한 번씩 울린다. 음은 칸 안 몫(fraction)으로 음계를 오른다 — 칸이 찰수록 높아진다.
            bool charging = strength > 0f && fraction < 1f;
            if (charging && time >= nextChimeTime)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(fraction * ChimeScale.Length), 0, ChimeScale.Length - 1);
                float vol = (0.05f + 0.06f * strength) * ChimeVolumeScale;
                sfx.PlayOneShot(chimeClips[idx], vol);
                ChimeNoteCount++;
                nextChimeTime = time + (0.2f - 0.11f * strength);
            }

            //  칸이 막 찼다 — 글자와 짧은 소리(버튼 튕김은 FlapPadView가 한다). 되감기 교정으로 경계에서 흔들려(1.002 → 0.998 → 1.004)
            //  두 번 뜨지 않게, 띄운 칸 수는 게이지가 반 칸 넘게 줄어야(대시 사용·박음) 내린다.
            if (announcedStacks < 0)
            {
                announcedStacks = stacks;
            }
            else if (stacks > announcedStacks)
            {
                feed.Publish(new FlappyMotePopup(center + Vector3.up * 1.4f, "대시 1칸!", FlappyMotePopupKind.Count));
                sfx.PlayOneShot(blipClip, 0.4f);
                announcedStacks = stacks;
            }
            else if (charge < announcedStacks - 0.5f)
            {
                announcedStacks = stacks;
            }
        }

        /// <summary>
        /// 스턴이 막 걸린 순간 줄어든 양에서 박아서 잃은 몫만 뽑는다. 같은 틱에 쓴 대시 한 칸(정수 부분)은 빼고 —
        /// 박아서 잃는 양은 늘 한 칸보다 작다 — 서버 교정으로 크게 바뀐 몫은 상한(StunLoss)에서 자른다(태스크 리뷰 10-24).
        /// </summary>
        internal static float LostOnStun(float drop, float stunLoss)
        {
            if (drop <= 0f)
            {
                return 0f;
            }
            float frac = drop - Mathf.Floor(drop + 1e-4f);
            return Mathf.Clamp(frac, 0f, stunLoss);
        }

        private void SpawnScatter(Vector3 center, float lost)
        {
            //  0.3칸이면 약 9알 — 잃은 양이 눈에 들어오게.
            int n = Mathf.Clamp(Mathf.RoundToInt(lost * 30f), 3, 24);
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(2f, 6f);
                var r = MakeSprite("DiveLoss", glow, 3, Cyan);
                r.transform.position = center;
                r.transform.localScale = Vector3.one * 0.35f;
                scatters.Add(new Scatter { Renderer = r, Velocity = new Vector3(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed + 2f, 0f), Life = Random.Range(0.6f, 0.9f) });
            }
            sfx.PlayOneShot(blipClip, 0.25f);
        }

        private void AnimateScatters(float deltaTime)
        {
            for (int i = scatters.Count - 1; i >= 0; i--)
            {
                var sc = scatters[i];
                sc.Age += deltaTime;
                sc.Velocity += Vector3.down * 18f * deltaTime;
                sc.Renderer.transform.position += sc.Velocity * deltaTime;
                var c = sc.Renderer.color;
                c.a = Mathf.Clamp01(1f - sc.Age / sc.Life);
                sc.Renderer.color = c;
                if (sc.Age >= sc.Life)
                {
                    DestroyObject(sc.Renderer.gameObject);
                    scatters.RemoveAt(i);
                }
            }
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
            sfx = root.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            //  충전음이 종소리 PlayOneShot으로 바뀌어 더는 돌려 트는(loop) 클립이 없다 — 음계 10개를 한 번만 구워 둔다.
            chimeClips = new AudioClip[ChimeScale.Length];
            for (int i = 0; i < ChimeScale.Length; i++)
            {
                chimeClips[i] = MakeChimeNote(ChimeScale[i], "FlappyDiveChime" + i);
            }
            blipClip = MakeSlotPop();
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

        //  충전음 한 음: 종(원음) + 한 옥타브 위를 옅게 겹친다 — 시제품 HUM.chime과 같은 비율(약 0.32배)로,
        //  세기(strength)에 따른 볼륨은 재생할 때 PlayOneShot의 volumeScale로 준다(클립 자체는 세기와 무관).
        private static AudioClip MakeChimeNote(float freq, string name)
        {
            const int rate = 44100;
            int length = (int)(rate * ChimeMainDuration);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float main = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * ChimeDecayRate);
                float octave = t <= ChimeOctaveDuration
                    ? Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * Mathf.Exp(-t * ChimeDecayRate * (ChimeMainDuration / ChimeOctaveDuration)) * ChimeOctaveRatio
                    : 0f;
                data[i] = main + octave;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        //  칸 완성음 "톡 + 반짝"(시제품 SLOT.pop, 10-26 사용자 선택): 세모파 톡 한 번 + 사인 반짝 + 반짝이 셋이
        //  차례로 울린다. 다섯 소리를 한 클립에 겹쳐 굽는다 — PlayOneShot 한 번으로 전부 재생되게.
        private static AudioClip MakeSlotPop()
        {
            const int rate = 44100;
            const float total = 0.16f + 0.3f;   // 마지막 반짝 시작(0.16) + 길이(0.3)
            int length = (int)(rate * total);
            var data = new float[length];
            AddPopTone(data, rate, 420f, 0.07f, 0.28f, 0f, triangle: true);
            AddPopTone(data, rate, 980f, 0.05f, 0.12f, 0.01f);
            AddPopTone(data, rate, 1568f, 0.3f, 0.06f, 0.06f);
            AddPopTone(data, rate, 2093f, 0.3f, 0.06f, 0.11f);
            AddPopTone(data, rate, 2637f, 0.3f, 0.06f, 0.16f);
            var clip = AudioClip.Create("FlappyDiveSlotPop", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        //  칸 완성음의 소리 한 조각을 섞어 더한다 — 지속시간에 반비례한 감쇠율(이전 삐- 소리의 exp(-14t)/0.18초와
        //  같은 꼬리 느낌)로 짧은 소리는 빠르게, 긴 반짝임은 느리게 잦아든다.
        private static void AddPopTone(float[] data, int rate, float freq, float duration, float volume, float startTime, bool triangle = false)
        {
            int start = Mathf.RoundToInt(startTime * rate);
            int length = Mathf.RoundToInt(duration * rate);
            float decayRate = 2.52f / duration;
            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float t = i / (float)rate;
                float wave = triangle
                    ? 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(2f * Mathf.PI * freq * t))
                    : Mathf.Sin(2f * Mathf.PI * freq * t);
                data[start + i] += wave * volume * Mathf.Exp(-t * decayRate);
            }
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
            if (birds.Count == 0 && scatters.Count == 0 && root == null)
            {
                return;
            }
            foreach (var bird in birds.Values) { DestroyBird(bird); }
            birds.Clear();
            foreach (var sc in scatters) { DestroyObject(sc.Renderer != null ? sc.Renderer.gameObject : null); }
            scatters.Clear();
            lastCharge = -1f;
            wasStunned = false;
            announcedStacks = -1;
            nextChimeTime = 0f;
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
            if (chimeClips != null)
            {
                foreach (var clip in chimeClips) { DestroyObject(clip); }
                chimeClips = null;
            }
            DestroyObject(blipClip);
            blipClip = null;
            views.Clear();
        }
    }
}
