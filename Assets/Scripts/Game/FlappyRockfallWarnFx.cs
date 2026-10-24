using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace LOP
{
    /// <summary>
    /// 낙석 예고(10-24 사람 판). 바위는 예고 없이 천장에서 나와 초속 9 m로 떨어져, 통로 위쪽 약 3 m에선 보고 반응할 틈이 없었다.
    /// 떨어지기 <see cref="WarnSeconds"/> 전부터 그 줄의 천장 균열 아래로 먼지·돌가루가 점점 세게 떨어진다 — 피하기가 "반응"에서 "읽기"가 된다.
    ///
    /// <para><b>그림만이다.</b> 판정·박자(<see cref="FlappyRockfallCurve"/>)는 그대로고, 박자는 시뮬과 같은 곡선을 내 예측 틱으로 읽는다
    /// (바위도 그 틱으로 서 있다). 그래서 예고가 끝나는 틱에 바위가 정확히 나온다.</para>
    /// </summary>
    public class FlappyRockfallWarnFx : ITickable, IDisposable
    {
        /// <summary>떨어지기 몇 초 전부터 예고하나. 숨는 시간(0.24초)보다 길어 앞 바위가 바닥 근처일 때 시작한다 — 균열은 천장이라 겹쳐 보이지 않는다.</summary>
        public const float WarnSeconds = 0.45f;
        private const int PoolSize = 48;
        private const float SpreadX = 0.55f;            // 균열 폭(±1 m) 안쪽에서
        private const float DustZ = -0.9f;              // 판정면·바위(−0.65) 앞, 천장 바위 띠(−1.35) 뒤
        private const float DustRatePerSecond = 70f;    // 예고 최고 세기에서 초당 먼지 수
        private static readonly Color DustColor = new Color(0.78f, 0.7f, 0.6f, 1f);
        private static readonly Color GritColor = new Color(0.42f, 0.36f, 0.3f, 1f);

        private readonly Func<long> currentTick;
        private readonly Func<double> secondsPerTick;
        private readonly Func<IReadOnlyList<FlappyRockfall>> findRockfalls;

        private GameObject root;
        private Sprite dot;
        private Texture2D dotTexture;
        private IReadOnlyList<FlappyRockfall> rockfalls = Array.Empty<FlappyRockfall>();
        private float rescanIn;
        private readonly Dictionary<FlappyRockfall, float> spawnDebt = new Dictionary<FlappyRockfall, float>();
        private readonly List<Mote> pool = new List<Mote>();
        private System.Random random = new System.Random(1013);

        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Life, MaxLife;
            public bool Alive;
        }

        [VContainer.Inject]
        public FlappyRockfallWarnFx(EntityRenderClock renderClock)
            : this(() => renderClock.TickFor(null), () => renderClock.SecondsPerTick, FindInScene)
        {
        }

        internal FlappyRockfallWarnFx(Func<long> currentTick, Func<double> secondsPerTick, Func<IReadOnlyList<FlappyRockfall>> findRockfalls)
        {
            this.currentTick = currentTick;
            this.secondsPerTick = secondsPerTick;
            this.findRockfalls = findRockfalls;
        }

        internal int ActiveDust
        {
            get { int n = 0; foreach (var m in pool) { if (m.Alive) n++; } return n; }
        }

        internal IEnumerable<Vector3> DustPositions()
        {
            foreach (var m in pool) { if (m.Alive) yield return m.Renderer.transform.position; }
        }

        /// <summary>
        /// 예고 세기 0~1: 떨어지기 <paramref name="warnSeconds"/> 전 0에서 시작해 직전에 1, 떨어지기 시작한 틱부터 0.
        /// <see cref="FlappyRockfallCurve.FallingAt"/>와 같은 정수 틱 계산을 쓴다 — 주기 경계에서 어긋나지 않게.
        /// 앞 바위가 아직 바닥 근처로 떨어지는 중이어도 다음 바위 예고는 시작한다(숨는 시간 0.24초는 예고로 짧다).
        /// </summary>
        internal static float WarnLevel(float periodSeconds, float fallSpeed, float dropDistance, float phaseDegrees,
                                        long tick, float tickSeconds, float warnSeconds)
        {
            if (periodSeconds <= 0f || fallSpeed <= 0f || tickSeconds <= 0f || warnSeconds <= 0f)
            {
                return 0f;
            }
            long ticksPerPeriod = Math.Max(1L, (long)Math.Round(periodSeconds / (double)tickSeconds));
            long phaseTicks = (long)Math.Round(phaseDegrees / 360.0 * ticksPerPeriod);
            long u = ((tick + phaseTicks) % ticksPerPeriod + ticksPerPeriod) % ticksPerPeriod;
            double untilFall = (ticksPerPeriod - u) * (double)tickSeconds;
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
            rescanIn -= deltaTime;
            if (rockfalls.Count == 0 || rescanIn <= 0f || HasDestroyed())
            {
                //  맵은 따로 올라온다(라운드마다 다시 로드) — 비어 있으면 가끔 다시 찾는다.
                rockfalls = findRockfalls() ?? Array.Empty<FlappyRockfall>();
                rescanIn = 1f;
            }
            long tick = currentTick();
            foreach (var rf in rockfalls)
            {
                if (rf == null)
                {
                    continue;
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
