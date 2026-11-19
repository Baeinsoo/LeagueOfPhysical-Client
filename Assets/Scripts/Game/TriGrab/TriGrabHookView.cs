using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 갈고리 그림. 서버 상태의 갈고리와 내가 미리 쏜 갈고리를 같은 식(<see cref="TriGrabHook.TipAt"/>)에 렌더 틱을 넣어 그린다.
    /// 사슬(가는 원기둥)은 팔 뿌리(몸 중심 + 방향 × 몸 반지름)에서 끝까지, 끝엔 집게. 잡은 카드는 끝에 붙어 돌아오며
    /// 정답이면 초록, 오답이면 빨강 테두리. 끄는 동안엔 조준선도 그린다.
    /// </summary>
    public class TriGrabHookView : IStartable, System.IDisposable
    {
        public const float HookHeight = TriGrabCarouselView.CardHeight;
        private const float ChainWidth = 0.09f;
        private const float CarriedScale = TriGrabCarouselView.CardScale * 0.85f;
        private static readonly Color ChainColor = new Color(0.55f, 0.6f, 0.72f);
        private static readonly Color ClawColor = new Color(0.92f, 0.94f, 0.97f);

        private sealed class Drawn
        {
            public GameObject Root;
            public Transform Chain;
            public Transform ClawLeft, ClawRight;
            public TriGrabCardFace Card;
        }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly TriGrabClientState state;
        private readonly TriGrabMyHook myHook;
        private readonly TriGrabAim aim;
        private readonly IPlayerContext playerContext;

        private readonly List<Drawn> drawn = new List<Drawn>();
        private readonly List<TriGrabHook> visible = new List<TriGrabHook>();
        private GameObject root;
        private Material chainMaterial, clawMaterial;
        private LineRenderer aimLine;
        private KnockbackLateUpdateDriver driver;

        public TriGrabHookView(GameFramework.Runner.IRunner runner, TriGrabClientState state, TriGrabMyHook myHook,
                               TriGrabAim aim, IPlayerContext playerContext)
        {
            this.runner = runner;
            this.state = state;
            this.myHook = myHook;
            this.aim = aim;
            this.playerContext = playerContext;
        }

        /// <summary>그 렌더 틱에 그리는가 — 쏜 틱부터 돌아온 틱까지.</summary>
        public static bool IsDrawn(TriGrabHook hook, double renderTick)
            => hook != null && renderTick >= hook.FireTick && renderTick <= hook.EndTick;

        /// <summary>그릴 갈고리 목록. 남의 것은 서버 상태, 내 것은 미리 쏜 것(확인 전)이나 서버 것.</summary>
        public static void Collect(TriGrabClientState state, TriGrabMyHook myHook, string myId, double renderTick, List<TriGrabHook> into)
        {
            into.Clear();
            foreach (var h in state.Hooks)
            {
                if (h.OwnerId != myId && IsDrawn(h, renderTick)) into.Add(h);
            }
            var mine = myHook.Current(myId, (long)System.Math.Floor(renderTick));
            if (IsDrawn(mine, renderTick)) into.Add(mine);
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
            double renderTick = TriGrabCarouselView.RenderTickOf(runner);
            var camera = Camera.main;
            if (double.IsNaN(renderTick) || camera == null || root == null) return;

            Collect(state, myHook, playerContext.entityId, renderTick, visible);
            while (drawn.Count < visible.Count) drawn.Add(Make(drawn.Count));
            for (int i = 0; i < drawn.Count; i++)
            {
                var d = drawn[i];
                if (i >= visible.Count)
                {
                    if (d.Root.activeSelf) d.Root.SetActive(false);
                    d.Card?.Hide();
                    continue;
                }
                var hook = visible[i];
                if (!d.Root.activeSelf) d.Root.SetActive(true);
                var start = To3(hook.Origin + hook.Dir * TriGrabConfig.BodyRadius);
                var tip = To3(TriGrabClientState.TipAt(hook, renderTick));
                PlaceChain(d, start, tip, hook.Extending((long)System.Math.Floor(renderTick)));

                if (hook.Carried.HasValue)
                {
                    if (d.Card == null) d.Card = TriGrabCardFace.Create("Carried", d.Root.transform.parent);
                    var c = hook.Carried.Value;
                    d.Card?.Show(c, TriGrabCarouselView.InkAt(c, renderTick), true, tip, camera.transform.rotation,
                                 CarriedScale, hook.Result, 0f);
                }
                else
                {
                    d.Card?.Hide();
                }
            }
            PlaceAim();
        }

        private static Vector3 To3(Vector2 p) => new Vector3(p.x, HookHeight, p.y);

        private void PlaceChain(Drawn d, Vector3 start, Vector3 tip, bool open)
        {
            var span = tip - start;
            float length = span.magnitude;
            if (length < 1e-3f)
            {
                d.Chain.gameObject.SetActive(false);
            }
            else
            {
                d.Chain.gameObject.SetActive(true);
                //  유니티 원기둥은 y축으로 높이 2라 절반만 늘린다.
                d.Chain.position = (start + tip) * 0.5f;
                d.Chain.rotation = Quaternion.FromToRotation(Vector3.up, span / length);
                d.Chain.localScale = new Vector3(ChainWidth, length * 0.5f, ChainWidth);
            }

            var forward = length < 1e-3f ? Vector3.forward : span / length;
            var look = Quaternion.LookRotation(forward, Vector3.up);
            //  뻗는 동안은 집게를 벌리고, 돌아올 땐 오므린다(시안과 같다).
            float spread = open ? 38f : 14f;
            PlaceClaw(d.ClawLeft, tip, look * Quaternion.Euler(0f, -spread, 0f));
            PlaceClaw(d.ClawRight, tip, look * Quaternion.Euler(0f, spread, 0f));
        }

        private static void PlaceClaw(Transform claw, Vector3 tip, Quaternion rotation)
        {
            claw.rotation = rotation;
            claw.position = tip + rotation * new Vector3(0f, 0f, 0.22f);
        }

        private Drawn Make(int index)
        {
            var go = new GameObject("Hook" + index);
            go.transform.SetParent(root.transform, false);
            var chain = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Chain", go.transform, chainMaterial).transform;
            var left = CardRushDraw.Box("ClawL", go.transform, Vector3.zero, new Vector3(0.1f, 0.1f, 0.45f), clawMaterial).transform;
            var right = CardRushDraw.Box("ClawR", go.transform, Vector3.zero, new Vector3(0.1f, 0.1f, 0.45f), clawMaterial).transform;
            return new Drawn { Root = go, Chain = chain, ClawLeft = left, ClawRight = right };
        }

        private static LineRenderer MakeAimLine(Transform parent)
        {
            var go = new GameObject("AimLine");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.08f;
            line.sharedMaterial = CardRushDraw.Transparent("TriGrab_Aim", new Color(1f, 1f, 1f, 0.6f));
            line.startColor = new Color(1f, 1f, 1f, 0.75f);
            line.endColor = new Color(1f, 1f, 1f, 0.15f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        private void PlaceAim()
        {
            if (aimLine == null) return;
            aimLine.enabled = aim.Active;
            if (!aim.Active) return;
            var from = aim.Origin + aim.Dir * TriGrabConfig.BodyRadius;
            var to = aim.Origin + aim.Dir * (TriGrabConfig.BodyRadius + TriGrabConfig.HookLength);
            aimLine.SetPosition(0, new Vector3(from.x, 0.15f, from.y));
            aimLine.SetPosition(1, new Vector3(to.x, 0.15f, to.y));
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
