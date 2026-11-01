using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace LOP
{
    /// <summary>
    /// 낙석 예고(10-24 사람 판). 바위는 예고 없이 천장에서 나와 초속 9 m로 떨어져, 통로 위쪽 약 3 m에선 보고 반응할 틈이 없었다.
    /// 두 단계로 알린다(11-01 사용자 "가볍게" — 붉은 띠·천장 등은 안 넣는다).
    /// <list type="number">
    /// <item>떨어지기 <see cref="WarnSeconds"/> 전부터 그 줄의 천장 균열 아래로 먼지·돌가루가 점점 세게 떨어진다.</item>
    /// <item>마지막 <see cref="PeekSeconds"/> 동안 그림용 바위(판정 없는 복사본)가 균열에서 <see cref="PeekDepthMax"/>까지 비집고 나오며
    /// 좌우로 떨고, 내 새 바로 다음 줄이 <see cref="RumbleRange"/> 안이면 그 줄만 우르르(<see cref="FlappySfx.Rumble"/>)를 낙하마다 한 번 낸다.</item>
    /// </list>
    ///
    /// <para><b>그림·소리만이다.</b> 판정·박자(<see cref="FlappyRockfallCurve"/>)는 그대로고, 박자는 시뮬과 같은 곡선을 내 예측 틱으로 읽는다
    /// (바위도 그 틱으로 서 있다). 그래서 예고가 끝나는 틱에 바위가 정확히 나온다. 진짜 바위(<see cref="FlappyRockfall.Rock"/>)는
    /// 건드리지 않는다 — 복사본은 이 연출의 뿌리 아래에 따로 있다.</para>
    /// </summary>
    public class FlappyRockfallWarnFx : ITickable, IDisposable
    {
        /// <summary>떨어지기 몇 초 전부터 예고하나. 숨는 시간(0.24초)보다 길어 앞 바위가 바닥 근처일 때 시작한다 — 균열은 천장이라 겹쳐 보이지 않는다.
        /// 0.45 → 0.7초(11-01 공정성 분석): 낙하 0.64초 전엔 어느 높이에서도 이미 못 피하는 상태가 0%라, 예고를 보고 0.2초 뒤에 움직여도
        /// 천장 근처까지 다 피할 수 있다(0.45초였을 땐 천장 띠의 6%가 못 피했다).</summary>
        public const float WarnSeconds = 0.7f;
        /// <summary>그림용 바위가 비집고 나와 떠는 시간(떨어지기 직전 몇 초).</summary>
        public const float PeekSeconds = 0.35f;
        /// <summary>그림용 바위 아랫면이 천장선 밑으로 나오는 최대 깊이(m).</summary>
        public const float PeekDepthMax = 0.45f;
        /// <summary>떨기 최대 폭(m, 좌우 한쪽) · 빠르기(Hz). 나올수록 세진다 — 색은 안 바꾸고 크기로만.</summary>
        public const float ShakeAmplitude = 0.05f, ShakeHz = 14f;
        /// <summary>우르르는 내 새 바로 다음 줄(앞쪽으로 가장 가까운 줄)이 이 거리(m) 안일 때만.</summary>
        public const float RumbleRange = 18f;
        //  복사본을 진짜 바위보다 조금 뒤에 — 떨어지기 시작한 두세 틱 겹칠 때 같은 면끼리 깜빡이지 않게.
        private const float PeekBehindZ = 0.02f;
        private const int PoolSize = 48;
        private const float SpreadX = 0.55f;            // 균열 폭(±1 m) 안쪽에서
        private const float DustZ = -0.9f;              // 판정면·바위(−0.65) 앞, 천장 바위 띠(−1.35) 뒤
        private const float DustRatePerSecond = 70f;    // 예고 최고 세기에서 초당 먼지 수
        private static readonly Color DustColor = new Color(0.78f, 0.7f, 0.6f, 1f);
        private static readonly Color GritColor = new Color(0.42f, 0.36f, 0.3f, 1f);

        private readonly Func<long> currentTick;
        private readonly Func<double> secondsPerTick;
        private readonly Func<IReadOnlyList<FlappyRockfall>> findRockfalls;
        private readonly Func<float?> myBirdX;
        private readonly IFlappyAudio audio;

        private GameObject root;
        private Sprite dot;
        private Texture2D dotTexture;
        private IReadOnlyList<FlappyRockfall> rockfalls = Array.Empty<FlappyRockfall>();
        private float searchClock, nextSearchAt;
        private int searches;
        private const int MaxSearches = 6;   // 스코프 시작 + 1초 간격 5초 — 낙석 없는 맵에서 영원히 뒤지지 않게
        private readonly Dictionary<FlappyRockfall, float> spawnDebt = new Dictionary<FlappyRockfall, float>();
        private readonly Dictionary<FlappyRockfall, GameObject> peeks = new Dictionary<FlappyRockfall, GameObject>();
        private readonly Dictionary<FlappyRockfall, long> rumbledCycle = new Dictionary<FlappyRockfall, long>();
        private readonly List<float> rowXs = new List<float>();
        private readonly List<Mote> pool = new List<Mote>();
        private System.Random random = new System.Random(1013);

        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Life, MaxLife;
            public bool Alive;
        }

        //  바위는 내 예측 틱으로 선다(FlappyWorld가 그 틱에 PoseForTick) — 예고도 같은 틱을 읽는다.
        //  EntityRenderClock.TickFor는 내 엔티티 id가 아니면 남의 새용 지연 시계(~0.23초 뒤)를 돌려주므로 쓰지 않는다(10-24 리뷰).
        [VContainer.Inject]
        public FlappyRockfallWarnFx(GameFramework.Runner.IRunner runner, EntityRenderClock renderClock,
                                    ActorRegistry actorRegistry, IGameDataStore gameDataStore, IFlappyAudio audio)
            : this(() => runner.tickUpdater?.tick ?? 0, () => renderClock.SecondsPerTick, FindInScene,
                   () => MyBirdX(actorRegistry, gameDataStore), audio)
        {
        }

        internal FlappyRockfallWarnFx(Func<long> currentTick, Func<double> secondsPerTick, Func<IReadOnlyList<FlappyRockfall>> findRockfalls,
                                      Func<float?> myBirdX = null, IFlappyAudio audio = null)
        {
            this.currentTick = currentTick;
            this.secondsPerTick = secondsPerTick;
            this.findRockfalls = findRockfalls;
            this.myBirdX = myBirdX ?? (() => null);
            this.audio = audio;
        }

        //  내 새 그림의 x. 판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다(FlappyMyBirdMarker와 같다).
        private static float? MyBirdX(ActorRegistry actorRegistry, IGameDataStore gameDataStore)
        {
            string id = gameDataStore.userEntityId;
            var visual = string.IsNullOrEmpty(id) ? null : actorRegistry.Get(id)?.visualGameObject;
            return visual != null ? visual.transform.position.x : (float?)null;
        }

        internal int ActiveDust
        {
            get { int n = 0; foreach (var m in pool) { if (m.Alive) n++; } return n; }
        }

        internal IEnumerable<Vector3> DustPositions()
        {
            foreach (var m in pool) { if (m.Alive) yield return m.Renderer.transform.position; }
        }

        /// <summary>그 줄의 그림용 바위(없거나 숨었으면 null).</summary>
        internal GameObject PeekOf(FlappyRockfall rf) => peeks.TryGetValue(rf, out var go) && go != null && go.activeSelf ? go : null;

        //  주기 안 틱 위치(정수) — FlappyRockfallCurve.FallingAt과 같은 계산(주기 경계에서 어긋나지 않게).
        private static bool Cycle(float periodSeconds, float phaseDegrees, long tick, float tickSeconds, out long ticksPerPeriod, out long u, out long cycle)
        {
            ticksPerPeriod = u = cycle = 0;
            if (periodSeconds <= 0f || tickSeconds <= 0f)
            {
                return false;
            }
            ticksPerPeriod = Math.Max(1L, (long)Math.Round(periodSeconds / (double)tickSeconds));
            long phaseTicks = (long)Math.Round(phaseDegrees / 360.0 * ticksPerPeriod);
            long t = tick + phaseTicks;
            u = (t % ticksPerPeriod + ticksPerPeriod) % ticksPerPeriod;
            cycle = (t - u) / ticksPerPeriod;
            return true;
        }

        /// <summary>
        /// 다음 낙하까지 남은 시간(초)과 그 낙하의 번호. 떨어지기 시작한 틱(주기 위치 0)엔 한 주기가 남았다고 답한다 —
        /// 그 틱부터는 예고가 아니라 낙하다. 박자를 모르면 false.
        /// </summary>
        internal static bool UntilFall(float periodSeconds, float phaseDegrees, long tick, float tickSeconds, out double seconds, out long nextFall)
        {
            seconds = 0d;
            nextFall = 0;
            if (Cycle(periodSeconds, phaseDegrees, tick, tickSeconds, out long n, out long u, out long cycle) == false)
            {
                return false;
            }
            seconds = (n - u) * (double)tickSeconds;
            nextFall = cycle + 1;
            return true;
        }

        /// <summary>
        /// 그림용 바위 자세: 아랫면이 천장선 밑으로 나온 깊이(m)와 좌우 떨림(m). 보일 때만 true.
        /// 떨어지기 <see cref="PeekSeconds"/> 전부터 0에서 <see cref="PeekDepthMax"/> 쪽으로 빠르게 나왔다가 천천히 다 나오고(1 − (1 − p)²),
        /// 떨림 폭도 나온 만큼 커진다. 진짜 바위가 떨어지기 시작하면 떨림을 멈추고 그 자리에 있다가, 진짜 바위 아랫면이
        /// 복사본 아랫면을 지나면(두세 틱) 사라진다 — 진짜 바위가 0에서 나오느라 그림이 위로 튀어 보이지 않게.
        /// </summary>
        internal static bool PeekPose(float periodSeconds, float fallSpeed, float dropDistance, float phaseDegrees,
                                      long tick, float tickSeconds, out float depth, out float shake)
        {
            depth = 0f;
            shake = 0f;
            if (fallSpeed <= 0f || UntilFall(periodSeconds, phaseDegrees, tick, tickSeconds, out double until, out _) == false)
            {
                return false;
            }
            if (until > PeekSeconds)
            {
                //  막 떨어지기 시작한 두세 틱 — 엿보던 자리에 멈춰 있다가 진짜 바위가 지나가면 사라진다.
                //  창을 먼저 본다: 엿보기 창 앞쪽 몇 틱엔 앞 바위가 아직 바닥 근처로 떨어지는 중이라, 낙하부터 보면 엿보기가 늦게 시작한다.
                if (FlappyRockfallCurve.FallingAt(periodSeconds, fallSpeed, dropDistance, phaseDegrees, tick, tickSeconds, out float fallen)
                    && fallen < PeekDepthMax)
                {
                    depth = PeekDepthMax;
                    return true;
                }
                return false;
            }
            float p = Mathf.Clamp01(1f - (float)(until / PeekSeconds) + tickSeconds / PeekSeconds);
            depth = PeekDepthMax * (1f - (1f - p) * (1f - p));
            float elapsed = PeekSeconds - (float)until;
            shake = ShakeAmplitude * p * Mathf.Sin(2f * Mathf.PI * ShakeHz * elapsed);
            return true;
        }

        /// <summary>우르르를 낼 거리인가: 내 새보다 앞(0 초과)이고 <see cref="RumbleRange"/> 안.</summary>
        internal static bool InRumbleRange(float rowX, float birdX)
        {
            float ahead = rowX - birdX;
            return ahead > 0f && ahead <= RumbleRange;
        }

        /// <summary>
        /// 우르르를 낼 줄 하나: 내 새 앞 <see cref="RumbleRange"/> 안에서 가장 가까운 줄. 없으면 -1.
        /// 11-01 판정: 90°면 범위 안 줄들이 0.5초마다 번갈아 떨어져 우르르가 쉬지 않았다 — 바로 다음 줄만 울린다(그림은 모든 줄 그대로).
        /// </summary>
        internal static int NextRumbleRow(IReadOnlyList<float> rowXs, float birdX)
        {
            int best = -1;
            for (int i = 0; i < rowXs.Count; i++)
            {
                if (InRumbleRange(rowXs[i], birdX) && (best < 0 || rowXs[i] < rowXs[best]))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 예고 세기 0~1: 떨어지기 <paramref name="warnSeconds"/> 전 0에서 시작해 직전에 1, 떨어지기 시작한 틱부터 0.
        /// <see cref="FlappyRockfallCurve.FallingAt"/>와 같은 정수 틱 계산을 쓴다 — 주기 경계에서 어긋나지 않게.
        /// 앞 바위가 아직 바닥 근처로 떨어지는 중이어도 다음 바위 예고는 시작한다(숨는 시간 0.24초는 예고로 짧다).
        /// </summary>
        internal static float WarnLevel(float periodSeconds, float fallSpeed, float dropDistance, float phaseDegrees,
                                        long tick, float tickSeconds, float warnSeconds)
        {
            if (fallSpeed <= 0f || warnSeconds <= 0f
                || UntilFall(periodSeconds, phaseDegrees, tick, tickSeconds, out double untilFall, out _) == false)
            {
                return 0f;
            }
            if (untilFall > warnSeconds)
            {
                return 0f;
            }
            return Mathf.Clamp01(1f - (float)(untilFall / warnSeconds) + tickSeconds / warnSeconds);
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            UpdateMotes(deltaTime);

            double spt = secondsPerTick();
            if (spt <= 0d)
            {
                return;   // 게임 정보 전 — 박자를 모른다
            }
            if (HasDestroyed())
            {
                //  맵이 다시 올라왔다(라운드마다 다시 로드) — 처음부터 다시 찾는다.
                ForgetRows();
                rockfalls = Array.Empty<FlappyRockfall>();
                searches = 0;
                searchClock = nextSearchAt = 0f;
            }
            if (rockfalls.Count == 0)
            {
                //  맵은 따로 올라온다 — 못 찾으면 1초 간격으로 몇 번만 다시 찾고 그만둔다(FlappyHologramFx와 같은 꼴).
                searchClock += deltaTime;
                if (searches >= MaxSearches || searchClock < nextSearchAt)
                {
                    return;
                }
                searches++;
                nextSearchAt = searchClock + 1f;
                rockfalls = findRockfalls() ?? Array.Empty<FlappyRockfall>();
                if (rockfalls.Count == 0)
                {
                    return;
                }
            }
            long tick = currentTick();
            FlappyRockfall rumbleRow = null;
            float? birdX = myBirdX();
            if (birdX.HasValue)
            {
                rowXs.Clear();
                foreach (var rf in rockfalls) { rowXs.Add(rf != null ? RowX(rf) : float.MaxValue); }
                int next = NextRumbleRow(rowXs, birdX.Value);
                rumbleRow = next >= 0 ? rockfalls[next] : null;
            }
            foreach (var rf in rockfalls)
            {
                if (rf == null)
                {
                    continue;
                }
                bool peeking = UpdatePeek(rf, tick, (float)spt);
                //  우르르: 내 새 바로 다음 줄만, 그 줄 낙하마다 한 번.
                if (peeking && audio != null && rf == rumbleRow
                    && UntilFall(rf.Period, rf.Phase, tick, (float)spt, out double until, out long nextFall) && until <= PeekSeconds
                    && (rumbledCycle.TryGetValue(rf, out long last) == false || last != nextFall))
                {
                    rumbledCycle[rf] = nextFall;
                    audio.Play(FlappySfx.Rumble);
                }
                float level = WarnLevel(rf.Period, rf.Speed, rf.Drop, rf.Phase, tick, (float)spt, WarnSeconds);
                if (level <= 0f)
                {
                    spawnDebt[rf] = -1f;   // 예고 밖
                    continue;
                }
                //  세기에 비례해 먼지를 흘린다(처음엔 몇 알, 직전엔 우수수). 굵은 돌가루는 후반에만 섞는다.
                //  예고에 막 들어선 프레임엔 한 알을 바로 떨군다 — 시작이 늦게 보이지 않게.
                float debt = spawnDebt.TryGetValue(rf, out float d) && d >= 0f ? d : 1f;
                debt += DustRatePerSecond * level * level * deltaTime;
                while (debt >= 1f)
                {
                    debt -= 1f;
                    Spawn(rf, level);
                }
                spawnDebt[rf] = debt;
            }
        }

        private static float RowX(FlappyRockfall rf) => rf.Rock != null ? rf.Rock.position.x : rf.transform.position.x;

        //  그림용 바위를 자세대로 놓는다(진짜 바위의 그림 "Look"을 판정 없이 복사 — 굽기가 이미 콜라이더를 뺐지만 한 번 더 뺀다).
        //  돌려주는 값 = 지금 보이나.
        private bool UpdatePeek(FlappyRockfall rf, long tick, float tickSeconds)
        {
            bool visible = PeekPose(rf.Period, rf.Speed, rf.Drop, rf.Phase, tick, tickSeconds, out float depth, out float shake);
            peeks.TryGetValue(rf, out GameObject copy);
            if (visible == false)
            {
                if (copy != null) { copy.SetActive(false); }
                return false;
            }
            if (rf.Rock == null)
            {
                return true;
            }
            Transform look = rf.Rock.Find("Look");
            if (copy == null)
            {
                if (look == null)
                {
                    return true;   // 그림 없는 맵(회색 상자) — 복사할 그림이 없다. 소리는 낸다.
                }
                EnsureRoot();
                copy = Object.Instantiate(look.gameObject, root.transform, false);
                copy.name = "RockfallPeek";
                //  지연 Destroy면 한 프레임 동안 판정 질의(UnityCollisionQuery)에 걸릴 수 있다 — 바로 지운다.
                foreach (Collider c in copy.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c);
                }
                copy.transform.localScale = look.lossyScale;
                peeks[rf] = copy;
            }
            copy.SetActive(true);
            float z = look != null ? look.position.z + PeekBehindZ : copy.transform.position.z;
            copy.transform.SetPositionAndRotation(
                new Vector3(RowX(rf) + shake, rf.transform.position.y + rf.Size * 0.5f - depth, z),
                rf.Rock.rotation);
            return true;
        }

        private void ForgetRows()
        {
            foreach (var copy in peeks.Values) { DestroyObject(copy); }
            peeks.Clear();
            rumbledCycle.Clear();
            spawnDebt.Clear();
        }

        private bool HasDestroyed()
        {
            foreach (var rf in rockfalls) { if (rf == null) return true; }
            return false;
        }

        private void Spawn(FlappyRockfall rf, float level)
        {
            Mote m = Take();
            if (m == null)
            {
                return;
            }
            float x = (rf.Rock != null ? rf.Rock.position.x : rf.transform.position.x) + ((float)random.NextDouble() * 2f - 1f) * SpreadX;
            float ceiling = rf.transform.position.y;
            bool grit = level > 0.5f && random.NextDouble() < 0.3;
            float size = grit ? 0.16f + 0.08f * (float)random.NextDouble() : 0.1f + 0.1f * (float)random.NextDouble();
            m.Renderer.color = grit ? GritColor : DustColor;
            m.Renderer.transform.position = new Vector3(x, ceiling - 0.05f, DustZ);
            m.Renderer.transform.localScale = Vector3.one * size;
            m.Velocity = new Vector3(((float)random.NextDouble() - 0.5f) * 0.6f, -(grit ? 5f : 1.5f + 1.5f * (float)random.NextDouble()), 0f);
            m.MaxLife = m.Life = grit ? 0.5f : 0.45f + 0.2f * (float)random.NextDouble();
            m.Alive = true;
            m.Renderer.enabled = true;
        }

        private Mote Take()
        {
            foreach (var m in pool) { if (m.Alive == false) return m; }
            if (pool.Count >= PoolSize)
            {
                return null;
            }
            EnsureRoot();
            var go = new GameObject("RockfallDust");
            go.transform.SetParent(root.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = dot;
            r.sortingOrder = 1;
            var mote = new Mote { Renderer = r };
            pool.Add(mote);
            return mote;
        }

        private void UpdateMotes(float deltaTime)
        {
            foreach (var m in pool)
            {
                if (m.Alive == false)
                {
                    continue;
                }
                m.Life -= deltaTime;
                if (m.Life <= 0f)
                {
                    m.Alive = false;
                    m.Renderer.enabled = false;
                    continue;
                }
                m.Velocity.y -= 9f * deltaTime;
                m.Renderer.transform.position += m.Velocity * deltaTime;
                Color c = m.Renderer.color;
                c.a = Mathf.Clamp01(m.Life / m.MaxLife * 1.5f) * 0.85f;
                m.Renderer.color = c;
            }
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }
            root = new GameObject("FlappyRockfallWarnFx");
            //  둥근 알갱이 하나(가장자리가 부드럽게).
            const int size = 16;
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01((1f - d) * 2.5f)));
                }
            }
            dotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FlappyRockfallDust" };
            dotTexture.SetPixels32(pixels);
            dotTexture.Apply();
            dot = Sprite.Create(dotTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static IReadOnlyList<FlappyRockfall> FindInScene()
            => Object.FindObjectsByType<FlappyRockfall>(FindObjectsSortMode.None);

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
            ForgetRows();
            DestroyObject(root);
            DestroyObject(dot);
            DestroyObject(dotTexture);
            root = null;
            dot = null;
            dotTexture = null;
            pool.Clear();
            spawnDebt.Clear();
        }
    }
}
