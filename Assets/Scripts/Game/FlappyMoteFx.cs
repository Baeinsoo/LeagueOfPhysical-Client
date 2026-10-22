using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 빛 알갱이 연출(클라 전용). 무더기를 그리고, 새가 지나가면 알갱이 복사본이 그 새에게 빨려 든다 —
    /// 남의 새도 똑같이 보인다. 내 새는 먹은 개수("+12알")와 박았을 때 흩어지는 알갱이를 더 보여 준다.
    ///
    /// <para><b>판정·게이지와 무관하다.</b> 게이지는 시뮬(<see cref="FlappyWorld"/>)이 정한다. 여기서는 같은 규칙
    /// (<see cref="FlappyMoteField.CountCrossed"/>)을 <b>화면에 그려진 자리</b>로 한 번 더 돌린다 — 내 새는 예측 자리,
    /// 남의 새는 보간 자리. 그래서 그림이 한두 알 어긋나도 결과에는 영향이 없다.</para>
    /// </summary>
    public class FlappyMoteFx : ITickable, System.IDisposable
    {
        private const float MoteSize = 0.36f;          // 번짐까지 포함한 지름(m)
        private const float BigMoteSize = 0.44f;
        private const float CopySize = 0.22f;
        private const float CopyLifetime = 0.7f;
        private const float BurstGap = 0.3f;           // 이만큼 안 먹으면 한 무더기를 다 지났다고 보고 개수를 띄운다
        private const int PlentyCount = 14;
        private const float HintLead = 14f;            // 무더기 이만큼 앞에서 안내를 띄운다(m)
        private const int HintClusters = 2;
        private const float BlipGap = 0.035f;

        private static readonly Color MoteColor = new Color(0.72f, 0.95f, 1f, 1f);
        private static readonly Color BigColor = new Color(1f, 0.86f, 0.45f, 1f);

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyMoteField field;
        private readonly FlappyMapRulesField rules;
        private readonly FlappyConfig config;
        private readonly FlappyMoteFeed feed;

        private GameObject root;
        private Sprite dot;
        private AudioSource audio;
        private AudioClip blip;

        private readonly HashSet<FlappyMoteSet> builtSets = new HashSet<FlappyMoteSet>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly Dictionary<(float, float), Mote> moteAt = new Dictionary<(float, float), Mote>();
        private readonly Dictionary<string, Track> tracks = new Dictionary<string, Track>();
        private readonly List<Flyer> flyers = new List<Flyer>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private readonly List<(float x, float y)> hits = new List<(float x, float y)>();
        private readonly List<float> clusterStarts = new List<float>();
        private int hintsShown;

        private int burstCount;
        private float sinceLastHit;
        private float sinceBlip;
        private bool wasStunned;
        private float time;

        //  테스트 seam — 실제로는 씬의 LOPEntityView, 테스트는 가짜를 끼운다.
        internal System.Func<string, UnityEngine.Transform> viewLookup;
        internal int FlyingCount => flyers.Count;
        internal int MoteCount => motes.Count;

        private class Mote
        {
            public SpriteRenderer Renderer;
            public Vector3 Home;
            public float Phase;
            public float Flash;
            public bool Big;
        }

        private class Track
        {
            public UnityEngine.Transform View;
            public float MaxX;
            public float LastY;
            public bool Started;
        }

        private class Flyer
        {
            public SpriteRenderer Renderer;
            public UnityEngine.Transform Target;   // null이면 흩어지는 알갱이(중력으로 떨어진다)
            public Vector3 Velocity;
            public float Age;
            public float Life;
        }

        public FlappyMoteFx(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                            FlappyMoteField field, FlappyMapRulesField rules, FlappyConfig config, FlappyMoteFeed feed)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.field = field;
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
            if (rules.MoteCharge == false || field.Sets.Count == 0)
            {
                ClearAll();   // 알갱이 없는 맵으로 바뀌었으면 옛 그림을 걷는다
                return;
            }
            time += deltaTime;
            EnsureBuilt();
            TrackBirds(deltaTime);
            WatchMyLoss();
            AnimateMotes(deltaTime);
            AnimateFlyers(deltaTime);
        }

        private void EnsureBuilt()
        {
            var sets = field.Sets;
            bool same = builtSets.Count == sets.Count;
            for (int s = 0; same && s < sets.Count; s++)
            {
                same = builtSets.Contains(sets[s]);
            }
            if (same)
            {
                return;
            }
            //  맵이 다시 올라왔다(라운드가 여러 판) — 옛 무더기·날던 알갱이·새마다 간 거리를 걷고 새로 그린다.
            ClearAll();
            EnsureRoot();
            for (int s = 0; s < sets.Count; s++)
            {
                var set = sets[s];
                if (builtSets.Add(set) == false)
                {
                    continue;
                }
                float minX = float.MaxValue;
                for (int i = 0; i < set.Count; i++)
                {
                    var go = new GameObject("Mote");
                    go.transform.SetParent(root.transform, false);
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = dot;
                    var home = new Vector3(set.Xs[i], set.Ys[i], 0f);
                    go.transform.position = home;
                    go.transform.localScale = Vector3.one * (set.Big ? BigMoteSize : MoteSize);
                    var mote = new Mote { Renderer = renderer, Home = home, Phase = Random.Range(0f, 6.28f), Big = set.Big };
                    renderer.color = set.Big ? BigColor : MoteColor;
                    motes.Add(mote);
                    moteAt[(set.Xs[i], set.Ys[i])] = mote;
                    minX = Mathf.Min(minX, set.Xs[i]);
                }
                if (set.Count > 0)
                {
                    clusterStarts.Add(minX);
                }
            }
            clusterStarts.Sort();
        }

        private void TrackBirds(float deltaTime)
        {
            float reach = config.BodyRadius + rules.MoteReach;
            sinceLastHit += deltaTime;
            sinceBlip += deltaTime;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (tracks.TryGetValue(entity.Id, out var track) == false)
                {
                    track = new Track();
                    tracks[entity.Id] = track;
                }
                //  매 프레임 다시 묻는다 — 그림(visualGameObject)은 비동기로 늦게 붙고, 움직이는 것은 그림뿐이다.
                //  처음 받은 것을 붙들면 그림이 붙기 전의 루트(안 움직인다)에 묶여 판 내내 스폰 자리에 멈춘다.
                track.View = viewLookup(entity.Id);
                if (track.View == null)
                {
                    continue;
                }
                //  그림 자리는 발밑이다 — 시뮬과 같이 몸 중심(공 반지름만큼 위)으로 잰다.
                Vector3 p = track.View.position + Vector3.up * config.BodyRadius;
                bool mine = entity.Id == playerContext.entityId;
                if (track.Started == false)
                {
                    track.Started = true;
                    track.MaxX = p.x;
                    track.LastY = p.y;
                    continue;
                }
                //  가장 멀리 간 자리부터만 센다 — 되감기 교정으로 그림이 살짝 뒤로 가도 같은 알갱이를 두 번 안 먹는다.
                if (p.x > track.MaxX)
                {
                    hits.Clear();
                    int n = field.CountCrossed(track.MaxX, track.LastY, p.x, p.y, reach, hits);
                    for (int i = 0; i < n; i++)
                    {
                        SpawnCopy(hits[i], track.View, mine);
                    }
                    if (mine && n > 0)
                    {
                        burstCount += n;
                        sinceLastHit = 0f;
                        if (sinceBlip >= BlipGap)
                        {
                            PlayBlip(burstCount);
                            sinceBlip = 0f;
                        }
                    }
                    track.MaxX = p.x;
                }
                track.LastY = p.y;
                if (mine)
                {
                    MaybeHint(p);
                }
            }
            if (burstCount > 0 && sinceLastHit >= BurstGap)
            {
                var me = MyView();
                if (me != null)
                {
                    bool plenty = burstCount >= PlentyCount;
                    feed.Publish(new FlappyMotePopup(me.position + Vector3.up * 1.2f,
                        plenty ? $"+{burstCount}알 듬뿍!" : $"+{burstCount}알",
                        plenty ? FlappyMotePopupKind.Plenty : FlappyMotePopupKind.Count));
                }
                burstCount = 0;
            }
        }

        private void MaybeHint(Vector3 p)
        {
            if (hintsShown >= HintClusters || hintsShown >= clusterStarts.Count)
            {
                return;
            }
            float start = clusterStarts[hintsShown];
            if (p.x >= start - HintLead && p.x < start)
            {
                feed.Publish(new FlappyMotePopup(new Vector3(start, p.y + 1.6f, 0f),
                    "가까이 지나가면 알갱이가 따라온다", FlappyMotePopupKind.Hint));
                hintsShown++;
            }
            else if (p.x >= start)
            {
                hintsShown++;   // 이미 지나쳤다(늦게 들어왔다) — 뒤늦게 띄우지 않는다
            }
        }

        //  박아서 스턴이 걸리는 순간 잃은 만큼 알갱이가 흩어진다. 규칙이 "절반 잃기"라 잃은 양 = 남은 양이다 —
        //  게이지 감소량으로 재면 같은 틱에 쓴 대시 한 칸이 섞이거나(1 감소와 구별 불가) 되감기 교정의 흔들림이 섞인다.
        private void WatchMyLoss()
        {
            var entity = string.IsNullOrEmpty(playerContext.entityId) ? null : entityRegistry.Get(playerContext.entityId);
            if (entity == null)
            {
                return;
            }
            float charge = entity.Get<FlappyDash>()?.Charge ?? 0f;
            bool stunned = (entity.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;
            bool justStunned = stunned && wasStunned == false;
            wasStunned = stunned;
            float drop = charge;
            if (justStunned && drop > 0.01f)
            {
                var me = MyView();
                if (me != null)
                {
                    int n = Mathf.Clamp(Mathf.RoundToInt(drop / Mathf.Max(0.001f, rules.MoteValue)), 4, 30);
                    for (int i = 0; i < n; i++)
                    {
                        float a = Random.Range(0f, Mathf.PI * 2f);
                        float speed = Random.Range(2f, 6f);
                        var r = Rent(me.position + Vector3.up * config.BodyRadius, MoteColor, CopySize);
                        flyers.Add(new Flyer
                        {
                            Renderer = r, Target = null, Age = 0f, Life = Random.Range(0.6f, 0.9f),
                            Velocity = new Vector3(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed + 2f, 0f),
                        });
                    }
                }
            }
        }

        private void SpawnCopy((float x, float y) at, UnityEngine.Transform target, bool mine)
        {
            Color color = MoteColor;
            if (moteAt.TryGetValue(at, out var mote))
            {
                mote.Flash = 1f;
                color = mote.Big ? BigColor : MoteColor;
            }
            if (mine == false)
            {
                color.a = 0.7f;   // 남의 것은 조금 옅게 — 내 것이 먼저 읽히게
            }
            var r = Rent(new Vector3(at.x, at.y, 0f), color, CopySize);
            flyers.Add(new Flyer { Renderer = r, Target = target, Age = 0f, Life = CopyLifetime });
        }

        private void AnimateMotes(float deltaTime)
        {
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                m.Flash = Mathf.Max(0f, m.Flash - deltaTime * 4f);
                float twinkle = 0.65f + 0.35f * Mathf.Sin(time * 5f + m.Phase);
                var c = m.Renderer.color;
                c.a = Mathf.Clamp01(twinkle + m.Flash);
                m.Renderer.color = c;
                var t = m.Renderer.transform;
                t.position = m.Home + new Vector3(Mathf.Sin(time * 1.5f + m.Phase) * 0.04f,
                                                  Mathf.Cos(time * 2f + m.Phase) * 0.04f, 0f);
                float size = (m.Big ? BigMoteSize : MoteSize) * (1f + m.Flash * 0.6f);
                t.localScale = Vector3.one * size;
            }
        }

        private void AnimateFlyers(float deltaTime)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.Age += deltaTime;
                var t = f.Renderer.transform;
                bool done = f.Age >= f.Life;
                if (f.Target != null)
                {
                    //  처음엔 천천히, 점점 빨리 — 새에게 "빨려 드는" 느낌.
                    float k = Mathf.Min(1f, deltaTime * (3f + f.Age * 26f));
                    Vector3 center = f.Target.position + Vector3.up * config.BodyRadius;
                    t.position = Vector3.Lerp(t.position, center, k);
                    done |= (t.position - center).sqrMagnitude < 0.09f;
                }
                else
                {
                    f.Velocity += Vector3.down * 18f * deltaTime;
                    t.position += f.Velocity * deltaTime;
                    var c = f.Renderer.color;
                    c.a = Mathf.Clamp01(1f - f.Age / f.Life);
                    f.Renderer.color = c;
                }
                if (done)
                {
                    Return(f.Renderer);
                    flyers.RemoveAt(i);
                }
            }
        }

        private SpriteRenderer Rent(Vector3 position, Color color, float size)
        {
            EnsureRoot();
            SpriteRenderer r;
            if (pool.Count > 0)
            {
                r = pool.Pop();
                r.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("MoteFlyer");
                go.transform.SetParent(root.transform, false);
                r = go.AddComponent<SpriteRenderer>();
                r.sprite = dot;
            }
            r.color = color;
            r.transform.position = position;
            r.transform.localScale = Vector3.one * size;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            r.gameObject.SetActive(false);
            pool.Push(r);
        }

        private void PlayBlip(int count)
        {
            if (audio == null || blip == null)
            {
                return;
            }
            //  먹을수록 음이 조금씩 오른다 — "또로록" 쌓이는 소리.
            audio.pitch = 1f + 0.03f * Mathf.Min(count, 24);
            audio.PlayOneShot(blip, 0.35f);
        }

        private UnityEngine.Transform MyView()
        {
            if (string.IsNullOrEmpty(playerContext.entityId))
            {
                return null;
            }
            return tracks.TryGetValue(playerContext.entityId, out var t) ? t.View : null;
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }
            root = new GameObject("FlappyMotes");
            dot = MakeDot();
            audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            blip = MakeBlip();
        }

        //  가운데가 밝고 가장자리로 번지는 점 하나. 그림 파일 없이 만든다.
        private static Sprite MakeDot()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FlappyMoteDot" };
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float core = Mathf.Clamp01(1f - d / 0.28f);
                    float glow = Mathf.Clamp01(1f - d);
                    float a = Mathf.Clamp01(core + glow * glow * 0.55f);
                    byte white = (byte)(255 * Mathf.Clamp01(0.75f + core * 0.25f));
                    pixels[y * size + x] = new Color32(white, white, white, (byte)(255 * a));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        //  짧은 "또" 소리 — 빨리 꺼지는 사인파.
        private static AudioClip MakeBlip()
        {
            const int rate = 44100;
            int length = (int)(rate * 0.06f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                data[i] = Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 55f);
            }
            var clip = AudioClip.Create("FlappyMoteBlip", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        //  찾은 뷰는 들고 있고, 못 찾은 새가 있으면 씬 훑기는 0.5초에 한 번만 한다(매 프레임 훑으면 비싸다).
        //  그림이 아직 안 붙었으면 null — 루트는 움직이지 않으므로 대신 쓰지 않는다.
        private readonly Dictionary<string, LOPEntityView> views = new Dictionary<string, LOPEntityView>();
        private float nextScan;

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
            if (builtSets.Count == 0 && flyers.Count == 0)
            {
                return;
            }
            for (int i = 0; i < motes.Count; i++)
            {
                DestroyObject(motes[i].Renderer.gameObject);
            }
            for (int i = 0; i < flyers.Count; i++)
            {
                Return(flyers[i].Renderer);
            }
            motes.Clear();
            moteAt.Clear();
            flyers.Clear();
            builtSets.Clear();
            clusterStarts.Clear();
            tracks.Clear();
            hintsShown = 0;
            burstCount = 0;
            wasStunned = false;
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
            if (root != null)
            {
                if (UnityEngine.Application.isPlaying) { Object.Destroy(root); }
                else { Object.DestroyImmediate(root); }
                root = null;
            }
            if (dot != null)
            {
                var tex = dot.texture;
                if (UnityEngine.Application.isPlaying) { Object.Destroy(dot); Object.Destroy(tex); }
                else { Object.DestroyImmediate(dot); Object.DestroyImmediate(tex); }
                dot = null;
            }
            motes.Clear();
            moteAt.Clear();
            flyers.Clear();
            pool.Clear();
            builtSets.Clear();
            tracks.Clear();
        }
    }
}
