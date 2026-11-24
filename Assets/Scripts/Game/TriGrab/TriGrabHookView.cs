using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using TG = LOP.TriGrab;

namespace LOP
{
    /// <summary>
    /// 손 그림. 남의 손은 서버 손을 원격 몸과 같은 시각으로, 내 손은 쏜 순간 미리 그린 손(서버 손이 오면 그것)을 내 시각으로
    /// <see cref="TriGrabHookPath.TipAt"/>에 넣어 그린다. 사슬은 몸 가장자리에서 손끝까지, 끝엔 집게.
    /// 끄는 동안엔 내 몸에서 사거리 끝까지 조준선을 그린다.
    /// </summary>
    public class TriGrabHookView : IStartable, System.IDisposable
    {
        public const float HookHeight = 0.6f;
        private const float ChainWidth = 0.09f;
        private static readonly Color ChainColor = new Color(0.79f, 0.77f, 0.86f);
        private static readonly Color ClawColor = new Color(0.56f, 0.53f, 0.66f);

        public readonly struct Drawn
        {
            public readonly TG.TriGrabHook Hook;
            public readonly double Tick;
            public Drawn(TG.TriGrabHook hook, double tick) { Hook = hook; Tick = tick; }
        }

        private sealed class HookArt
        {
            public GameObject Root;
            public Transform Chain;
            public Transform ClawLeft, ClawRight;
        }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly EntityRenderClock renderClock;
        private readonly TriGrabClientState state;
        private readonly TriGrabMyHook myHook;
        private readonly TriGrabAim aim;
        private readonly IPlayerContext playerContext;

        private readonly List<HookArt> arts = new List<HookArt>();
        private readonly List<Drawn> visible = new List<Drawn>();
        private GameObject root;
        private Material chainMaterial, clawMaterial;
        private LineRenderer aimLine;
        private KnockbackLateUpdateDriver driver;

        public TriGrabHookView(GameFramework.Runner.IRunner runner, EntityRenderClock renderClock, TriGrabClientState state,
                               TriGrabMyHook myHook, TriGrabAim aim, IPlayerContext playerContext)
        {
            this.runner = runner;
            this.renderClock = renderClock;
            this.state = state;
            this.myHook = myHook;
            this.aim = aim;
            this.playerContext = playerContext;
        }

        /// <summary>그릴 손과 그 손을 그릴 시각. 남의 것은 서버 손과 서버에서 막 지워진 손(원격 시각), 내 것은 미리 그린 것이나 서버 것(내 시각).</summary>
        public static void Collect(TriGrabClientState state, TriGrabMyHook myHook, string myId, double remoteTick, double localTick,
                                   List<Drawn> into)
        {
            into.Clear();
            foreach (var h in state.Hooks)
            {
                if (h.Owner != myId && TriGrabHookPath.IsOut(h, remoteTick)) into.Add(new Drawn(h, remoteTick));
            }
            //  서버에선 지워졌어도 원격 시각으로는 아직 돌아오는 중인 남의 손.
            foreach (var (h, until) in state.LeftHooks)
            {
                if (h.Owner != myId && remoteTick >= h.FireTick && remoteTick < until) into.Add(new Drawn(h, remoteTick));
            }
            var mine = myHook.Current(myId, (long)System.Math.Floor(localTick));
            if (TriGrabHookPath.IsOut(mine, localTick)) into.Add(new Drawn(mine, localTick));
        }

        public void Start()
        {
            root = new GameObject("TriGrabHooks");
            chainMaterial = CardRushDraw.Opaque("TriGrab_Chain", ChainColor);
            clawMaterial = CardRushDraw.Opaque("TriGrab_Claw", ClawColor);
            aimLine = MakeAimLine(root.transform);
            driver = KnockbackLateUpdateDriver.Create("TriGrabHookDriver", Place);
        }

        private void Place()
        {
            double remote = TriGrabRenderTicks.Remote(renderClock);
            double local = TriGrabRenderTicks.Local(runner);
            if (double.IsNaN(remote) || double.IsNaN(local) || root == null) return;

            Collect(state, myHook, playerContext.entityId, remote, local, visible);
            while (arts.Count < visible.Count) arts.Add(Make(arts.Count));
            for (int i = 0; i < arts.Count; i++)
            {
                var art = arts[i];
                if (i >= visible.Count)
                {
                    if (art.Root.activeSelf) art.Root.SetActive(false);
                    continue;
                }
                var d = visible[i];
                if (!art.Root.activeSelf) art.Root.SetActive(true);
                var origin = TriGrabHookPath.ToUnity(d.Hook.Origin);
                var dir = TriGrabHookPath.ToUnity(d.Hook.Dir);
                var start = To3(origin + dir * TG.TriGrabConfig.BodyRadius);
                var tip = To3(TriGrabHookPath.TipAt(d.Hook, d.Tick));
                PlaceChain(art, start, tip, dir, TriGrabHookPath.Extending(d.Hook, (long)System.Math.Floor(d.Tick)));
            }
            PlaceAim();
        }

        private static Vector3 To3(Vector2 p) => new Vector3(p.x, HookHeight, p.y);

        private static void PlaceChain(HookArt art, Vector3 start, Vector3 tip, Vector2 dir, bool open)
        {
            var span = tip - start;
            float length = span.magnitude;
            //  손끝이 몸 가장자리 안쪽이면(막 나가거나 다 돌아옴) 사슬은 숨기고 집게만.
            bool chain = length > 1e-3f && Vector3.Dot(span, new Vector3(dir.x, 0f, dir.y)) > 0f;
            art.Chain.gameObject.SetActive(chain);
            if (chain)
            {
                //  유니티 원기둥은 y축으로 높이 2라 절반만 늘린다.
                art.Chain.position = (start + tip) * 0.5f;
                art.Chain.rotation = Quaternion.FromToRotation(Vector3.up, span / length);
                art.Chain.localScale = new Vector3(ChainWidth, length * 0.5f, ChainWidth);
            }

            var look = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
            //  뻗는 동안은 집게를 벌리고, 돌아올 땐 오므린다(시안과 같다).
            float spread = open ? 38f : 14f;
            PlaceClaw(art.ClawLeft, tip, look * Quaternion.Euler(0f, -spread, 0f));
            PlaceClaw(art.ClawRight, tip, look * Quaternion.Euler(0f, spread, 0f));
        }

        private static void PlaceClaw(Transform claw, Vector3 tip, Quaternion rotation)
        {
            claw.rotation = rotation;
            claw.position = tip + rotation * new Vector3(0f, 0f, 0.18f);
        }

        private HookArt Make(int index)
        {
            var go = new GameObject("Hook" + index);
            go.transform.SetParent(root.transform, false);
            var chain = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Chain", go.transform, chainMaterial).transform;
            var left = CardRushDraw.Box("ClawL", go.transform, Vector3.zero, new Vector3(0.09f, 0.09f, 0.36f), clawMaterial).transform;
            var right = CardRushDraw.Box("ClawR", go.transform, Vector3.zero, new Vector3(0.09f, 0.09f, 0.36f), clawMaterial).transform;
            return new HookArt { Root = go, Chain = chain, ClawLeft = left, ClawRight = right };
        }

        private static LineRenderer MakeAimLine(Transform parent)
        {
            var go = new GameObject("AimLine");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.08f;
            line.sharedMaterial = CardRushDraw.Transparent("TriGrab_Aim", new Color(1f, 0.62f, 0.11f, 0.6f));
            line.startColor = new Color(1f, 0.62f, 0.11f, 0.8f);
            line.endColor = new Color(1f, 0.62f, 0.11f, 0.2f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        private void PlaceAim()
        {
            if (aimLine == null) return;
            aimLine.enabled = aim.Active;
            if (!aim.Active) return;
            var from = aim.Origin + aim.Dir * TG.TriGrabConfig.BodyRadius;
            var to = aim.Origin + aim.Dir * TG.TriGrabConfig.HookRange;
            aimLine.SetPosition(0, new Vector3(from.x, TriGrabCardsView.PlateTop + 0.3f, from.y));
            aimLine.SetPosition(1, new Vector3(to.x, TriGrabCardsView.PlateTop + 0.3f, to.y));
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
            if (chainMaterial != null) Object.Destroy(chainMaterial);
            if (clawMaterial != null) Object.Destroy(clawMaterial);
            if (aimLine != null && aimLine.sharedMaterial != null) Object.Destroy(aimLine.sharedMaterial);
        }
    }
}
