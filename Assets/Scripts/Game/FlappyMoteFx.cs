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
        //  10-23 2.5D 비교(웹 시안 B "빛 영역 + 반응")로 정한 모양: 빛나는 별 알갱이 + 벽 쪽이 진한 빛 띠 +
        //  다가가면 알갱이가 쏠리고 띠가 밝아진다. 0.36 m 점은 23 m 카메라·갈색 조명 속에 묻혔다(사용자 판).
        private const float MoteSize = 0.62f;          // 십자 반짝임까지 포함한 지름(m)
        private const float BigMoteSize = 0.72f;
        private const float CopySize = 0.32f;
        private const float BandHeight = 1.05f;        // 빛 띠 두께 — 먹히는 범위(몸 표면에서 0.5 m)와 거의 맞는다
        private const float BandMargin = 0.2f;         // 띠가 알갱이 바깥으로 더 뻗는 거리(좌우)
        private const float StreakLength = 0.42f;
        private const int BandStreaks = 5;             // 띠 안에서 벽 → 틈 쪽으로 흐르는 결 줄 수
        private const float ReactRange = 1.4f;         // 몸 표면에서 이만큼 안이면 알갱이가 쏠리기 시작한다
        private const float CopyLifetime = 0.7f;
        private const float BurstGap = 0.3f;           // 이만큼 안 먹으면 한 무더기를 다 지났다고 보고 개수를 띄운다
        private const int PlentyCount = 14;
        private const float HintLead = 14f;            // 무더기 이만큼 앞에서 안내를 띄운다(m)
        private const int HintClusters = 2;
        private const float BlipGap = 0.035f;

        private static readonly Color MoteColor = new Color(0.72f, 0.95f, 1f, 1f);
        private static readonly Color BigColor = new Color(1f, 0.86f, 0.45f, 1f);
        private static readonly Color BandColor = new Color(0.45f, 0.92f, 1f, 1f);
        private static readonly Color BigBandColor = new Color(1f, 0.8f, 0.35f, 1f);

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyMoteField field;
        private readonly FlappyMapRulesField rules;
        private readonly FlappyConfig config;
        private readonly FlappyMoteFeed feed;

        private GameObject root;
        private Sprite star;
        private Sprite bandSprite;
        private Sprite streakSprite;
        private readonly List<Band> bands = new List<Band>();
        private Vector3 lastMeCenter;
        private bool warnedNoGapHalf;
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
        private float lastCharge = -1f;
        private float time;

        //  테스트 seam — 실제로는 씬의 LOPEntityView, 테스트는 가짜를 끼운다.
        internal System.Func<string, UnityEngine.Transform> viewLookup;
        internal void UseDefaultViewLookup() => viewLookup = DefaultViewLookup;
        internal int FlyingCount => flyers.Count;
        internal Vector3 FlyerPosition(int index) => flyers[index].Renderer.transform.position;
        internal int MoteCount => motes.Count;
        internal int BandCount => bands.Count;
        internal float PullOf(int index) => motes[index].Pull;
        internal float HeatOf(int index) => bands[index].Heat;
        internal (Vector3 position, float scaleY) BandPose(int index)
            => (bands[index].Renderer.transform.position, bands[index].Renderer.transform.localScale.y);

        private class Mote
        {
            public SpriteRenderer Renderer;
            public Vector3 Home;
            public FlappyMoteSet Set;   // 움직이는 무더기면 케이지 오프셋을 묻는다
            public float Phase;
            public float Flash;
            public float Pull;     // 내 새가 가까워 쏠리는 정도(0~1)
            public bool Big;
        }

        //  무더기 한쪽 끝(위·아래)의 빛 띠. 벽(Lip)에 붙어 틈 쪽으로 BandHeight만큼 뻗는다.
        private class Band
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer[] Streaks;
            public float[] StreakPhase;
            public float X0, X1, Lip;
            public int Side;       // +1 = 아래 끝(위로 뻗는다), -1 = 위 끝(아래로 뻗는다)
            public bool Big;
            public float Heat;     // 내 새가 띠 안에 들어온 정도(0~1)
            public FlappyMoteSet Set;
        }

        private class Track
        {
            public UnityEngine.Transform View;
            public float MaxX;
            public float StartX;
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
            public bool Scatter;   // 박아서 흩어지는 알갱이(중력으로 떨어진다)
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
                    renderer.sprite = star;
                    renderer.sortingOrder = 2;
                    var home = new Vector3(set.Xs[i], set.Ys[i], 0f);
                    go.transform.position = home;
                    go.transform.localScale = Vector3.one * (set.Big ? BigMoteSize : MoteSize);
                    var mote = new Mote { Renderer = renderer, Home = home, Set = set, Phase = Random.Range(0f, 6.28f), Big = set.Big };
                    renderer.color = set.Big ? BigColor : MoteColor;
                    motes.Add(mote);
                    moteAt[(set.Xs[i], set.Ys[i])] = mote;
                    minX = Mathf.Min(minX, set.Xs[i]);
                }
                if (set.Count > 0)
                {
                    clusterStarts.Add(minX);
                }
                BuildBands(set);
            }
            clusterStarts.Sort();
        }

        //  알갱이가 붙은 끝마다 띠 하나. 틈 반 높이를 모르는 무더기(옛 마커)는 띠 없이 알갱이만 그린다.
        private void BuildBands(FlappyMoteSet set)
        {
            if (set.GapHalf <= 0f)
            {
                if (warnedNoGapHalf == false)
                {
                    //  콘텐츠 빌드가 GapHalf를 모르는 Shared로 구워지면 이렇게 된다(마커가 값을 잃는다) — 띠만 조용히 사라지지 않게 한 번 알린다.
                    warnedNoGapHalf = true;
                    Debug.LogWarning("[FlappyMoteFx] 틈 반 높이(GapHalf)가 없는 알갱이 무더기가 있어 빛 띠를 안 그린다 — 맵 콘텐츠를 Shared 최신으로 다시 구웠는지 확인.");
                }
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = float.MaxValue, x1 = float.MinValue;
                for (int i = 0; i < set.Count; i++)
                {
                    bool bottom = set.Ys[i] < set.OriginY;
                    if ((side > 0) != bottom)
                    {
                        continue;
                    }
                    x0 = Mathf.Min(x0, set.Xs[i]);
                    x1 = Mathf.Max(x1, set.Xs[i]);
                }
                if (x0 > x1)
                {
                    continue;
                }
                var band = new Band
                {
                    X0 = x0 - BandMargin, X1 = x1 + BandMargin, Side = side, Big = set.Big, Set = set,
                    Lip = side > 0 ? set.OriginY - set.GapHalf : set.OriginY + set.GapHalf,
                    Streaks = new SpriteRenderer[BandStreaks], StreakPhase = new float[BandStreaks],
                };
                band.Renderer = MakeRenderer("MoteBand", bandSprite, 0);
                band.Renderer.transform.position = new Vector3((band.X0 + band.X1) * 0.5f, band.Lip + side * BandHeight * 0.5f, 0.05f);
                //  띠 그림은 아래가 진하다 — 위 끝의 띠는 뒤집어 벽 쪽이 진하게.
                band.Renderer.transform.localScale = new Vector3(band.X1 - band.X0, BandHeight * side, 1f);
                for (int k = 0; k < BandStreaks; k++)
                {
                    band.Streaks[k] = MakeRenderer("MoteBandStreak", streakSprite, 1);
                    band.StreakPhase[k] = Random.value;
                }
                bands.Add(band);
            }
        }

        private SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
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
                    track.StartX = p.x;
                    track.LastY = p.y;
                    continue;
                }
                //  가장 멀리 간 자리부터만 센다 — 되감기 교정으로 그림이 살짝 뒤로 가도 같은 알갱이를 두 번 안 먹는다.
                if (p.x > track.MaxX)
                {
                    hits.Clear();
                    //  움직이는 무더기는 지금 그려진 자리(케이지 오프셋)로 — 그림과 같은 자리에서 먹는 게 보이게.
                    int n = field.CountCrossedVisual(track.MaxX, track.LastY, p.x, p.y, reach, hits);
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
                //  출발선에서 기다리는 동안(카운트다운 화면이 덮고 있다)은 띄우지 않는다 — 첫 무더기가 스폰에서
                //  안내 거리 안이라 출발 전에 떠서 버려졌다. 실제로 1 m 넘게 나아간 뒤부터 본다.
                if (mine && p.x - track.StartX >= 1f)
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

        //  박아서 스턴이 걸리는 순간 잃은 만큼 알갱이가 흩어진다. 규칙이 "채우던 칸만 잃기"라 잃은 양 = 스턴 직전 게이지의
        //  칸 안 몫이다. 직전 프레임 게이지를 들고 있다가 스턴이 막 걸린 순간의 감소량으로 센다(같은 순간 쓴 대시 한 칸은 빼고).
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
            float before = lastCharge;
            lastCharge = charge;
            if (justStunned == false || before < 0f)
            {
                return;
            }
            float drop = before - (float)System.Math.Floor(before);   // 채우던 몫 — 그만큼을 잃었다
            if (drop <= 0.01f || charge >= before)
            {
                return;
            }
            var me = MyView();
            if (me == null)
            {
                return;
            }
            int n = Mathf.Clamp(Mathf.RoundToInt(drop / Mathf.Max(0.001f, rules.MoteValue)), 1, 30);
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(2f, 6f);
                var r = Rent(me.position + Vector3.up * config.BodyRadius, MoteColor, CopySize);
                flyers.Add(new Flyer
                {
                    Renderer = r, Target = null, Scatter = true, Age = 0f, Life = Random.Range(0.6f, 0.9f),
                    Velocity = new Vector3(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed + 2f, 0f),
                });
            }
        }

        private void SpawnCopy((float x, float y) at, UnityEngine.Transform target, bool mine)
        {
            Color color = MoteColor;
            float offset = 0f;
            if (moteAt.TryGetValue(at, out var mote))
            {
                mote.Flash = 1f;
                color = mote.Big ? BigColor : MoteColor;
                offset = mote.Set.CurrentVisualOffset;   // 승강기 위면 보이는 별 자리(케이지만큼 옮긴 곳)에서 출발
            }
            if (mine == false)
            {
                color.a = 0.7f;   // 남의 것은 조금 옅게 — 내 것이 먼저 읽히게
            }
            var r = Rent(new Vector3(at.x, at.y + offset, 0f), color, CopySize);
            flyers.Add(new Flyer { Renderer = r, Target = target, Age = 0f, Life = CopyLifetime });
        }

        private void AnimateMotes(float deltaTime)
        {
            var me = MyView();
            bool hasMe = me != null && IsMeStunned() == false;
            Vector3 c0 = hasMe ? me.position + Vector3.up * config.BodyRadius : lastMeCenter;
            if (hasMe)
            {
                lastMeCenter = c0;   // 스턴이 걸린 순간에도 쏠렸던 알갱이가 서서히 돌아가게 마지막 자리를 들고 있는다
            }
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                m.Flash = Mathf.Max(0f, m.Flash - deltaTime * 4f);
                Vector3 home = m.Home + Vector3.up * m.Set.CurrentVisualOffset;   // 승강기 위면 케이지와 같이 오르내린다
                //  다가가면 반응: 손 닿는 거리 근처에 들면 미리 밝아지고 내 새 쪽으로 살짝 쏠린다 — "지금 채워지는 곳"을 먹기 전부터 보여 준다.
                float target = 0f;
                if (hasMe && home.x > c0.x - 0.3f)
                {
                    float dist = Vector2.Distance(home, c0) - config.BodyRadius;
                    target = Mathf.Clamp01((ReactRange - dist) / 1f);
                }
                m.Pull = Mathf.MoveTowards(m.Pull, target, deltaTime * 10f);
                float twinkle = 0.7f + 0.3f * Mathf.Sin(time * 5f + m.Phase);
                float boost = m.Flash + m.Pull * 0.8f;
                var c = m.Renderer.color;
                c.a = Mathf.Clamp01(0.75f + 0.25f * twinkle + boost);
                m.Renderer.color = c;
                var t = m.Renderer.transform;
                //  쏠림은 0.45 m까지만 — 되감기 교정으로 그림이 멀리 튀어도 알갱이가 끌려가지 않게.
                Vector3 toward = Vector3.ClampMagnitude((c0 - home) * (0.22f * m.Pull), 0.45f);
                t.position = home + toward + new Vector3(Mathf.Sin(time * 1.5f + m.Phase) * 0.04f,
                                                           Mathf.Cos(time * 2f + m.Phase) * 0.04f, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, (time * 35f + m.Phase * 57f) % 360f);
                float size = (m.Big ? BigMoteSize : MoteSize) * (0.9f + 0.15f * twinkle) * (1f + boost * 0.6f);
                t.localScale = Vector3.one * size;
            }
            for (int i = 0; i < bands.Count; i++)
            {
                var b = bands[i];
                float lip = b.Lip + b.Set.CurrentVisualOffset;
                b.Renderer.transform.position = new Vector3((b.X0 + b.X1) * 0.5f, lip + b.Side * BandHeight * 0.5f, 0.05f);
                float inside = 0f;
                if (hasMe && c0.x >= b.X0 && c0.x <= b.X1)
                {
                    float surface = b.Side > 0 ? (c0.y - config.BodyRadius) - lip : lip - (c0.y + config.BodyRadius);
                    //  벽 선보다 안쪽(벽 쪽)에 몸이 있으면 띠 안이 아니라 벽에 박으러 가는 중이다 — 밝히지 않는다.
                    if (surface >= -0.05f && surface < BandHeight)
                    {
                        inside = Mathf.Clamp01(1f - surface / BandHeight);
                    }
                }
                b.Heat = Mathf.MoveTowards(b.Heat, inside, deltaTime * 8f);
                Color bc = b.Big ? BigBandColor : BandColor;
                bc.a = 0.42f + 0.08f * Mathf.Sin(time * 2.2f + b.X0) + b.Heat * 0.45f;
                b.Renderer.color = bc;
                //  결: 벽에서 틈 쪽으로 흐르다 끝에서 사라진다. 띠 안에 들어오면 빨라지고 밝아진다.
                float width = b.X1 - b.X0;
                for (int k = 0; k < b.Streaks.Length; k++)
                {
                    b.StreakPhase[k] = (b.StreakPhase[k] + deltaTime * (0.35f + b.Heat * 1.4f)) % 1f;
                    float u = b.StreakPhase[k];
                    var st = b.Streaks[k];
                    float x = b.X0 + width * (k + 0.5f) / b.Streaks.Length;
                    //  결 한 줄이 통째로 띠 안에 머물게 중심을 반 길이만큼 안쪽에서 움직인다.
                    float along = StreakLength * 0.5f + (BandHeight - StreakLength) * u;
                    st.transform.position = new Vector3(x, lip + b.Side * along, 0.07f);
                    st.transform.localScale = new Vector3(0.5f, StreakLength, 1f);   // 결 그림은 가로 0.125 m — 반으로 줄여 6 cm 선
                    Color sc = b.Big ? BigBandColor : BandColor;
                    sc.a = Mathf.Sin(u * Mathf.PI) * (0.35f + b.Heat * 0.5f);
                    st.color = sc;
                }
            }
        }

        private bool IsMeStunned()
        {
            var entity = string.IsNullOrEmpty(playerContext.entityId) ? null : entityRegistry.Get(playerContext.entityId);
            return (entity?.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;
        }

        private void AnimateFlyers(float deltaTime)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.Age += deltaTime;
                var t = f.Renderer.transform;
                bool done = f.Age >= f.Life;
                if (f.Scatter == false && f.Target == null)
                {
                    done = true;   // 날아가던 새의 그림이 사라졌다(그림 교체·퇴장) — 떨어뜨리지 말고 거둔다
                }
                else if (f.Scatter == false)
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
                r.sprite = star;
                r.sortingOrder = 3;
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
            star = MakeStar();
            bandSprite = MakeBand();
            streakSprite = MakeStreak();
            audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            blip = MakeBlip();
        }

        //  점 + 십자 반짝임. 2.5D 화면에서 점보다 훨씬 잘 보인다(웹 시안 "빛나는 별").
        private static Sprite MakeStar()
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01(1f - d / 0.15f);
                    float glow = Mathf.Clamp01(1f - d);
                    float flare = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(dy) * 22f) * (1f - Mathf.Abs(dx)),
                                            Mathf.Clamp01(1f - Mathf.Abs(dx) * 22f) * (1f - Mathf.Abs(dy)));
                    float a = Mathf.Clamp01(core + glow * glow * 0.45f + flare * 0.85f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            }
            return SpriteFrom(pixels, size, size, "FlappyMoteStar");
        }

        //  빛 띠: 아래(벽 쪽)가 진하고 위로 옅어진다, 좌우 끝은 부드럽게. 1×1 m로 그려 크기는 Transform으로 맞춘다.
        private static Sprite MakeBand()
        {
            const int w = 64, h = 64;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(1f - y / (float)(h - 1), 1.4f);
                for (int x = 0; x < w; x++)
                {
                    float edge = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / (w * 0.22f));
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a * edge));
                }
            }
            return SpriteFrom(pixels, w, h, "FlappyMoteBand");
        }

        //  띠 안의 결 한 줄: 가운데가 밝고 위아래 끝이 사라지는 가는 선.
        private static Sprite MakeStreak()
        {
            const int w = 8, h = 64;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Sin(Mathf.PI * y / (h - 1));
                for (int x = 0; x < w; x++)
                {
                    float edge = 1f - Mathf.Abs(x - (w - 1) * 0.5f) / (w * 0.5f);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(255 * a * edge));
                }
            }
            return SpriteFrom(pixels, w, h, "FlappyMoteStreak");
        }

        private static Sprite SpriteFrom(Color32[] pixels, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(pixels);
            tex.Apply();
            //  가로 1 m가 되게(pixelsPerUnit = 가로 픽셀 수). 띠·결은 Transform 크기로 늘린다.
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
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
            for (int i = 0; i < bands.Count; i++)
            {
                DestroyObject(bands[i].Renderer.gameObject);
                foreach (var st in bands[i].Streaks) { DestroyObject(st.gameObject); }
            }
            bands.Clear();
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
            lastCharge = -1f;
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
            if (blip != null)
            {
                DestroyObject(blip);
                blip = null;
            }
            foreach (var sprite in new[] { star, bandSprite, streakSprite })
            {
                if (sprite != null)
                {
                    var tex = sprite.texture;
                    DestroyObject(sprite);
                    DestroyObject(tex);
                }
            }
            star = bandSprite = streakSprite = null;
            bands.Clear();
            motes.Clear();
            moteAt.Clear();
            flyers.Clear();
            pool.Clear();
            builtSets.Clear();
            tracks.Clear();
        }
    }
}
