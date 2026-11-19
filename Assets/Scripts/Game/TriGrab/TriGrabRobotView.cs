using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 선수 몸마다 도형 로봇(바퀴 두 줄·좌석 색 몸통·어깨 팔 뿌리)을 붙이고 치비를 그 위 조종석에 세운다.
    /// 치비는 그림(모델)만 들어 올린다 — 몸의 실제 자리는 건드리지 않는다. 팔은 갈고리·조준 방향을, 없으면 회전판 가운데를 본다.
    /// 재장전 중엔 몸통 위 고리가 차오른다(서버가 보낸 재장전 끝 틱으로).
    /// </summary>
    public class TriGrabRobotView : IStartable, System.IDisposable
    {
        private const string FaceMaterialKey = "Assets/Characters/Chibi/Materials/ChibiFace.mat";
        /// <summary>치비를 들어 올리는 높이(m) — 조종석 바닥.</summary>
        public const float CockpitLift = 0.62f;
        public const float ArmHeight = 0.62f;
        private const int RingSegments = 40;
        private const float RingRadius = 0.95f;
        private const float RingHeight = 1.05f;
        /// <summary>오답 재장전(65틱)처럼 긴 재장전은 빨강, 빗나감(13틱)은 노랑.</summary>
        public const long LongReloadTicks = 30;

        private static readonly Color TreadColor = new Color(0.17f, 0.2f, 0.28f);
        private static readonly Color ArmColor = new Color(0.55f, 0.59f, 0.7f);
        private static readonly Color GlassColor = new Color(0.75f, 0.9f, 1f, 0.35f);

        private sealed class Robot
        {
            public GameObject Visual;
            public Transform Model;
            public Vector3 ModelRest;
            public GameObject Root;
            public Renderer Body;
            public Transform ArmPivot;
            public LineRenderer Ring;
            public long RingTotal;
        }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly TriGrabClientState state;
        private readonly TriGrabMyHook myHook;
        private readonly TriGrabAim aim;
        private readonly IPlayerContext playerContext;
        private readonly ActorRegistry actors;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CosmeticCatalog catalog;
        private readonly AppearanceColorPalette palette;

        private readonly Dictionary<string, Robot> robots = new Dictionary<string, Robot>();
        private readonly Dictionary<string, GameObject> dressed = new Dictionary<string, GameObject>();
        private readonly List<string> gone = new List<string>();
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Vector3[] ringPoints = new Vector3[RingSegments + 1];
        private Material treadMaterial, bodyMaterial, armMaterial, glassMaterial, ringMaterial;
        private Material faceMaterial;
        private KnockbackLateUpdateDriver driver;

        public TriGrabRobotView(GameFramework.Runner.IRunner runner, TriGrabClientState state, TriGrabMyHook myHook, TriGrabAim aim,
                                IPlayerContext playerContext, ActorRegistry actors, GameFramework.World.EntityRegistry entityRegistry,
                                CosmeticCatalog catalog, AppearanceColorPalette palette)
        {
            this.runner = runner;
            this.state = state;
            this.myHook = myHook;
            this.aim = aim;
            this.playerContext = playerContext;
            this.actors = actors;
            this.entityRegistry = entityRegistry;
            this.catalog = catalog;
            this.palette = palette;
        }

        /// <summary>팔이 볼 방향(바닥 xz). 갈고리가 나가 있으면 그 방향, 아니면 회전판 가운데 쪽.</summary>
        public static Vector2 ArmDirection(Vector2 bodyPosition, TriGrabHook hook, bool aiming, Vector2 aimDir)
        {
            if (hook != null && hook.Dir.sqrMagnitude > 1e-6f) return hook.Dir;
            if (aiming && aimDir.sqrMagnitude > 1e-6f) return aimDir.normalized;
            var toCenter = -bodyPosition;
            return toCenter.sqrMagnitude > 1e-6f ? toCenter.normalized : Vector2.up;
        }

        /// <summary>재장전 고리를 보이나와 채운 몫(0~1). 갈고리가 나가 있는 동안은 안 보인다(그동안은 갈고리가 보인다).</summary>
        public static bool ReloadRing(long renderTick, long reloadUntilTick, bool hookOut, long total, out float fill01)
        {
            fill01 = 0f;
            long left = reloadUntilTick - renderTick;
            if (hookOut || left <= 0 || total <= 0) return false;
            fill01 = Mathf.Clamp01(1f - left / (float)total);
            return true;
        }

        public void Start()
        {
            treadMaterial = CardRushDraw.Opaque("TriGrab_Tread", TreadColor);
            bodyMaterial = CardRushDraw.Opaque("TriGrab_Body", Color.white);
            armMaterial = CardRushDraw.Opaque("TriGrab_Arm", ArmColor);
            glassMaterial = CardRushDraw.Transparent("TriGrab_Glass", GlassColor);
            ringMaterial = CardRushDraw.Transparent("TriGrab_Ring", Color.white);
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>(FaceMaterialKey).Completed += h =>
            {
                faceMaterial = h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? h.Result : null;
                dressed.Clear();   // 재질보다 몸이 먼저 떴으면 얼굴 없이 입었다 — 다시 입힌다
            };
            driver = KnockbackLateUpdateDriver.Create("TriGrabRobotDriver", Place);
        }

        private void Place()
        {
            double renderTickD = TriGrabCarouselView.RenderTickOf(runner);
            if (double.IsNaN(renderTickD)) return;
            long renderTick = (long)System.Math.Floor(renderTickD);
            string me = playerContext.entityId;

            for (int seat = 0; seat < state.PlayerIds.Count; seat++)
            {
                string id = state.PlayerIds[seat];
                if (!actors.TryGet(id, out var actor) || actor == null || actor.visualGameObject == null) continue;
                var visual = actor.visualGameObject;

                if (ChibiDresser.IsChibi(visual) && (!dressed.TryGetValue(id, out var last) || last != visual))
                {
                    ChibiDresser.Dress(visual, faceMaterial, entityRegistry.Get(id)?.Get<PlayerLook>(), catalog, palette);
                    dressed[id] = visual;
                }

                if (!robots.TryGetValue(id, out var robot) || robot.Visual != visual)
                {
                    if (robot != null) Remove(robot);
                    robot = Build(visual, seat);
                    robots[id] = robot;
                }

                //  다른 그림 코드가 모델 자리를 되돌려도 매 프레임 조종석 높이로 다시 올린다.
                if (robot.Model != null) robot.Model.localPosition = robot.ModelRest + Vector3.up * CockpitLift;

                bool mine = id == me;
                var hook = mine ? myHook.Current(me, renderTick) : state.HookOf(id);
                bool hookOut = TriGrabHookView.IsDrawn(hook, renderTickD);
                var p = visual.transform.position;
                var dir = ArmDirection(new Vector2(p.x, p.z), hookOut ? hook : null, mine && aim.Active, aim.Dir);
                robot.ArmPivot.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);

                long reloadUntil = state.TryGetPlayer(id, out var score) ? score.ReloadUntilTick : 0;
                long left = reloadUntil - renderTick;
                if (hookOut || left <= 0) robot.RingTotal = 0;
                else if (robot.RingTotal == 0) robot.RingTotal = left;
                PlaceRing(robot, renderTick, reloadUntil, hookOut);
            }

            gone.Clear();
            foreach (var pair in robots)
            {
                if (pair.Value.Visual == null || state.SeatOf(pair.Key) < 0) gone.Add(pair.Key);
            }
            foreach (var id in gone)
            {
                Remove(robots[id]);
                robots.Remove(id);
            }
        }

        private void PlaceRing(Robot robot, long renderTick, long reloadUntil, bool hookOut)
        {
            if (!ReloadRing(renderTick, reloadUntil, hookOut, robot.RingTotal, out float fill))
            {
                robot.Ring.enabled = false;
                return;
            }
            robot.Ring.enabled = true;
            var color = robot.RingTotal > LongReloadTicks ? TriGrabPalette.Wrong : TriGrabPalette.Ink(1);
            CardRushDraw.Tint(robot.Ring, block, color, 0.95f);
            int n = Mathf.Max(2, Mathf.CeilToInt(RingSegments * fill) + 1);
            var center = robot.Root.transform.position + Vector3.up * RingHeight;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 0.5f - (fill * Mathf.PI * 2f) * i / (n - 1);
                ringPoints[i] = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RingRadius;
            }
            robot.Ring.positionCount = n;
            robot.Ring.SetPositions(ringPoints);
        }

        private Robot Build(GameObject visual, int seat)
        {
            var root = new GameObject("TriGrabRobot");
            root.transform.SetParent(visual.transform, false);
            CardRushDraw.Box("TreadL", root.transform, new Vector3(-0.5f, 0.17f, 0f), new Vector3(0.32f, 0.34f, 1.35f), treadMaterial);
            CardRushDraw.Box("TreadR", root.transform, new Vector3(0.5f, 0.17f, 0f), new Vector3(0.32f, 0.34f, 1.35f), treadMaterial);
            var body = CardRushDraw.Box("Body", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.95f, 0.4f, 1.05f), bodyMaterial);
            var renderer = body.GetComponent<Renderer>();
            CardRushDraw.Tint(renderer, block, TriGrabPalette.Seat(seat), 1f);
            //  조종석 유리(반투명) — 치비가 그 안에 서 있는 것처럼 보이게.
            var glass = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Glass", root.transform, glassMaterial);
            glass.transform.localPosition = new Vector3(0f, 0.75f, -0.05f);
            glass.transform.localScale = new Vector3(0.75f, 0.12f, 0.75f);

            //  팔 뿌리는 몸 방향과 상관없이 월드 방향을 본다 — 몸이 걷는 쪽과 쏘는 쪽이 다를 수 있다.
            var pivot = new GameObject("ArmPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, ArmHeight, 0f);
            CardRushDraw.Box("Arm", pivot, new Vector3(0f, 0f, 0.55f), new Vector3(0.28f, 0.24f, 0.5f), armMaterial);

            var ringGo = new GameObject("ReloadRing");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.widthMultiplier = 0.09f;
            ring.sharedMaterial = ringMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.enabled = false;

            var model = visual.transform.Find(ChibiRig.ModelChildName);
            var rest = model != null ? model.localPosition : Vector3.zero;
            if (model != null) model.localPosition = rest + Vector3.up * CockpitLift;

            return new Robot
            {
                Visual = visual, Model = model, ModelRest = rest, Root = root, Body = renderer, ArmPivot = pivot, Ring = ring,
            };
        }

        private static void Remove(Robot robot)
        {
            if (robot.Model != null) robot.Model.localPosition = robot.ModelRest;
            if (robot.Root != null) Object.Destroy(robot.Root);
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            foreach (var robot in robots.Values) Remove(robot);
            robots.Clear();
            foreach (var m in new[] { treadMaterial, bodyMaterial, armMaterial, glassMaterial, ringMaterial })
            {
                if (m != null) Object.Destroy(m);
            }
        }
    }
}
