using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using TG = LOP.TriGrab;

namespace LOP
{
    /// <summary>그림이 쓰는 두 시각. 둘 다 소수 틱이다.</summary>
    public static class TriGrabRenderTicks
    {
        /// <summary>내 몸·내 손을 그리는 시각 — 내 몸 보간과 같은 "한 틱 전".</summary>
        public static double Local(GameFramework.Runner.IRunner runner)
        {
            var t = runner?.tickUpdater;
            if (t == null || t.interval <= 0d) return double.NaN;
            return (t.elapsedTime - t.interval) / t.interval;
        }

        /// <summary>
        /// 남의 몸을 그리는 시각(원격 보간 재생 시계). 카드·두더지·남의 손도 이 시각으로 그려야 남의 몸과 어긋나 보이지 않는다.
        /// </summary>
        public static double Remote(EntityRenderClock clock)
        {
            if (clock == null || clock.SecondsPerTick <= 0d) return double.NaN;
            return clock.TimeFor(null) / clock.SecondsPerTick;
        }
    }

    /// <summary>
    /// id마다 하나씩 꺼내 쓰고, 이번 프레임에 안 쓴 것은 숨겨 다음 id에 다시 준다(지우지 않는다).
    /// </summary>
    public sealed class TriGrabPool<T> where T : class
    {
        private readonly Func<T> make;
        private readonly Action<T> hide;
        private readonly Dictionary<int, T> used = new Dictionary<int, T>();
        private readonly Stack<T> free = new Stack<T>();
        private readonly HashSet<int> touched = new HashSet<int>();
        private readonly List<int> drop = new List<int>();

        public TriGrabPool(Func<T> make, Action<T> hide)
        {
            this.make = make;
            this.hide = hide;
        }

        public int Made { get; private set; }
        public int InUse => used.Count;
        public IEnumerable<T> All => used.Values;

        /// <summary>이 id의 그림. 처음이면 숨겨 둔 것을 다시 쓰거나 새로 만든다. <paramref name="fresh"/>는 이번에 새로 맡았나.</summary>
        public T Get(int id, out bool fresh)
        {
            touched.Add(id);
            if (used.TryGetValue(id, out var item))
            {
                fresh = false;
                return item;
            }
            fresh = true;
            if (free.Count > 0) item = free.Pop();
            else
            {
                item = make();
                Made++;
            }
            used[id] = item;
            return item;
        }

        /// <summary>이번 프레임에 <see cref="Get"/>하지 않은 것을 숨겨 돌려놓는다.</summary>
        public void EndFrame()
        {
            drop.Clear();
            foreach (var pair in used)
            {
                if (!touched.Contains(pair.Key)) drop.Add(pair.Key);
            }
            foreach (var id in drop)
            {
                var item = used[id];
                used.Remove(id);
                if (item != null)
                {
                    hide?.Invoke(item);
                    free.Push(item);
                }
            }
            touched.Clear();
        }
    }

    /// <summary>
    /// 원판(판 윗면)과 동전 카드. 카드 정체는 상태, 자리는 <see cref="TriGrabCardTrack"/>을 원격 몸과 같은 시각으로 보간한다.
    /// 끌려가는 카드는 그 손끝에 붙는다(내 손이면 앞서 그린 손끝). 새 카드는 위에서 떨어지며 커진다(PopK).
    /// </summary>
    public class TriGrabCardsView : IStartable, IDisposable
    {
        /// <summary>판 윗면 높이(m). 맵 받침(0.05)보다 조금 위.</summary>
        public const float PlateTop = 0.08f;
        public const float CoinThickness = 0.12f;
        /// <summary>새 카드가 떨어지기 시작하는 높이(m) — 시안 14.</summary>
        public const float PopDrop = 1.4f;
        private static readonly Color PlateColor = new Color(0.16f, 0.14f, 0.25f);
        private static readonly Color RimColor = new Color(1f, 0.62f, 0.11f);
        private static readonly Color CoinBaseColor = new Color(0.05f, 0.04f, 0.09f);

        private sealed class Coin
        {
            public GameObject Root;
            public Transform Base;
            public Renderer BaseRenderer;
            public TriGrabCardFace Face;
        }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly EntityRenderClock renderClock;
        private readonly TriGrabClientState state;
        private readonly TriGrabCardTrack track;
        private readonly TriGrabMyHook myHook;
        private readonly IPlayerContext playerContext;

        private TriGrabPool<Coin> coins;
        private GameObject root;
        private Material plateMaterial, rimMaterial, baseMaterial;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private KnockbackLateUpdateDriver driver;

        public TriGrabCardsView(GameFramework.Runner.IRunner runner, EntityRenderClock renderClock, TriGrabClientState state,
                                TriGrabCardTrack track, TriGrabMyHook myHook, IPlayerContext playerContext)
        {
            this.runner = runner;
            this.renderClock = renderClock;
            this.state = state;
            this.track = track;
            this.myHook = myHook;
            this.playerContext = playerContext;
        }

        /// <summary>새 카드 크기 배율과 떠 있는 높이(시안 syncCards). Live가 아니면 다 나타난 것으로 본다.</summary>
        public static void Pop(TG.TriGrabCard c, double renderTick, bool live, out float scale, out float lift)
        {
            float k = live ? TG.TriGrabCards.PopK(c, (long)Math.Floor(renderTick)) : 1f;
            k = Mathf.Clamp01(k);
            scale = 0.35f + 0.65f * k;
            lift = (1f - k) * PopDrop;
        }

        /// <summary>
        /// 그 카드를 그릴 바닥 자리. 끌던 손이 그 카드에 닿은 틱(돌아선 틱) 이후의 시각이면 손끝, 그 전이면 자리 기록 보간.
        /// 사라진 카드도 사라진 틱 전까지는 그린다 — 상태는 바로 오지만 카드는 조금 과거 시각으로 그리기 때문이다.
        /// 손 시각은 내 손이면 내 시각, 남의 손이면 원격 시각. 그릴 수 없으면 false.
        /// </summary>
        public static bool TryCardPosition(TG.TriGrabCard c, TriGrabClientState state, TriGrabCardTrack track,
                                           TriGrabMyHook myHook, string myId, double remoteTick, double localTick,
                                           out Vector2 pos, out bool carried)
        {
            carried = false;
            pos = default;
            TG.TriGrabHook hook = null;
            if (c.Gone)
            {
                hook = state.LastCarrierOf(c.Id);
                double t = hook != null && hook.Owner == myId ? localTick : remoteTick;
                if (t >= state.GoneTickOf(c.Id)) return false;
            }
            else if (c.CarriedBy != null)
            {
                hook = c.CarriedBy == myId ? myHook.Current(myId, (long)Math.Floor(localTick)) : state.HookOf(c.CarriedBy);
                if (hook != null && hook.CardId != c.Id) hook = null;
            }
            if (hook != null)
            {
                double t = hook.Owner == myId ? localTick : remoteTick;
                if (t >= hook.TurnTick)
                {
                    pos = TriGrabHookPath.TipAt(hook, t);
                    carried = true;
                    return true;
                }
            }
            return track.TryGet(c.Id, remoteTick, out pos);
        }

        public void Start()
        {
            root = new GameObject("TriGrabCards");
            plateMaterial = CardRushDraw.Opaque("TriGrab_Plate", PlateColor);
            rimMaterial = CardRushDraw.Opaque("TriGrab_Rim", RimColor);
            baseMaterial = CardRushDraw.Opaque("TriGrab_CoinBase", CoinBaseColor);
            BuildPlate();
            coins = new TriGrabPool<Coin>(MakeCoin, HideCoin);
            driver = KnockbackLateUpdateDriver.Create("TriGrabCardsDriver", Place);
        }

        //  맵 받침(반지름 4.6)은 울타리 안쪽(5.5)보다 작아, 카드가 받침 밖에 있으면 판 밖처럼 보인다 — 울타리까지 판을 덮는다.
        private void BuildPlate()
        {
            float r = TG.TriGrabConfig.DiscRadius;
            var plate = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Plate", root.transform, plateMaterial);
            plate.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
            plate.transform.localPosition = new Vector3(0f, PlateTop - 0.02f, 0f);
            var rim = new GameObject("Rim");
            rim.transform.SetParent(root.transform, false);
            var line = rim.AddComponent<LineRenderer>();
            const int n = 72;
            line.positionCount = n;
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.18f;
            line.sharedMaterial = rimMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * (r - 0.09f), PlateTop + 0.005f, Mathf.Sin(a) * (r - 0.09f)));
            }
        }

        private Coin MakeCoin()
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(root.transform, false);
            var b = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Base", go.transform, baseMaterial);
            return new Coin
            {
                Root = go,
                Base = b.transform,
                BaseRenderer = b.GetComponent<Renderer>(),
                Face = TriGrabCardFace.Create("Face", go.transform),
            };
        }

        private static void HideCoin(Coin coin)
        {
            if (coin.Base != null) coin.Base.gameObject.SetActive(false);
            coin.Face?.Hide();
        }

        private void Place()
        {
            double remote = TriGrabRenderTicks.Remote(renderClock);
            double local = TriGrabRenderTicks.Local(runner);
            var camera = Camera.main;
            if (double.IsNaN(remote) || double.IsNaN(local) || camera == null || root == null) return;

            var phase = state.PhaseAt((long)Math.Floor(remote));
            bool live = phase == TG.TriGrabPhase.Live;
            bool resolved = phase == TG.TriGrabPhase.Resolved;
            string me = playerContext.entityId;
            var facing = camera.transform.rotation;
            float r = TG.TriGrabConfig.CoinRadius;

            foreach (var c in state.Cards)
            {
                //  정체는 신뢰 소식이라 그리는 시각보다 먼저 올 수 있다 — 생긴 틱 전엔 그리지 않는다.
                if (remote < c.BornTick) continue;
                if (!TryCardPosition(c, state, track, myHook, me, remote, local, out var p, out bool carried)) continue;
                var coin = coins.Get(c.Id, out _);
                Pop(c, remote, live && !carried, out float scale, out float lift);
                float y = carried ? TriGrabHookView.HookHeight - 0.1f : PlateTop + lift;

                coin.Base.gameObject.SetActive(!(resolved && !c.Ok));
                coin.Base.position = new Vector3(p.x, y + CoinThickness * 0.5f * scale, p.y);
                float baseDiameter = (r + 0.045f) * 2f * scale * (resolved && c.Ok ? 1.12f : 1f);
                coin.Base.localScale = new Vector3(baseDiameter, CoinThickness * 0.5f * scale, baseDiameter);
                CardRushDraw.Tint(coin.BaseRenderer, block, resolved && c.Ok ? TriGrabPalette.Correct : CoinBaseColor, 1f);

                float pop = live ? 1f - Mathf.Clamp01((float)((remote - c.BornTick) / TG.TriGrabConfig.PopTicks)) : 0f;
                coin.Face?.Show(c, new Vector3(p.x, y + CoinThickness * scale + 0.005f, p.y), facing,
                                r * 2f * scale, resolved && c.Ok, resolved && !c.Ok, pop);
            }
            coins.EndFrame();
        }

        public void Dispose()
        {
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (var m in new[] { plateMaterial, rimMaterial, baseMaterial })
            {
                if (m != null) UnityEngine.Object.Destroy(m);
            }
        }
    }
}
