using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 테마 물건 층 — 슬리퍼·수박·장독·줄·줄넘기 거인. 바닥 층(<see cref="DodgeHazardView"/>)과 같은 도형·같은 렌더 틱을 쓰고,
    /// 크기·움직임은 <see cref="DodgePropPose"/>가 판정 값에서 계산한다. 판정은 서버다 — 여기는 그리기만.
    /// </summary>
    public class DodgePropView : IStartable, ILateTickable, IDisposable
    {
        private const string ChibiKey = "Assets/Characters/Chibi/Chibi.prefab";

        private readonly GameFramework.Runner.IRunner runner;
        private readonly DodgeClientState state;
        private readonly DodgeConfig config;
        private readonly DodgePropKit kit;
        private readonly List<DodgeShape> shapes = new List<DodgeShape>();
        private readonly Dictionary<string, List<Transform>> pools = new Dictionary<string, List<Transform>>();
        private readonly Dictionary<string, int> used = new Dictionary<string, int>();
        private readonly List<GameObject> giants = new List<GameObject>();
        private GameObject root;
        private GameObject chibiPrefab;

        public DodgePropView(GameFramework.Runner.IRunner runner, DodgeClientState state, DodgeConfig config, DodgePropKit kit)
        {
            this.runner = runner;
            this.state = state;
            this.config = config;
            this.kit = kit;
        }

        public void Start()
        {
            // 거인은 선수와 같은 치비 — 원격 그룹에서 한 번 받는다. 못 받으면 거인 없이 줄만.
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(ChibiKey).Completed += h =>
            {
                chibiPrefab = h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded
                    ? h.Result : null;
            };
        }

        public void LateTick()
        {
            if (kit == null || runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long tick = DodgeHazardView.ShapeTick(renderTick, out float frac);

            shapes.Clear();
            foreach (var p in state.Patterns)
            {
                DodgeHazards.Shapes(p, tick, config, shapes);
            }

            root ??= new GameObject("DodgeProps");
            used.Clear();
            int giantPairs = 0;
            foreach (var s in shapes)
            {
                if (DodgePropPose.IsBullet(s.Kind) && s.Type == DodgeShapeType.Circle)
                {
                    DrawSlipper(s, frac, renderTick);
                }
                else if (s.Kind == DodgePatternKind.Bomb && !s.Active)
                {
                    var c = DodgeHazardView.CircleCenter(s, frac);
                    var t = Take("melon", kit.melonMesh, kit.melonMaterial);
                    t.position = new Vector3(c.x, DodgePropPose.MelonDiameter * 0.5f + DodgePropPose.MelonHeight(s.Progress), c.y);
                    t.rotation = Quaternion.identity;
                    t.localScale = Vector3.one * DodgePropPose.MelonDiameter;
                }
                else if (s.Kind == DodgePatternKind.Rock)
                {
                    DrawJar(s, frac, renderTick);
                }
                else if (s.Kind == DodgePatternKind.Laser)
                {
                    DrawRope(s, giantPairs++);
                }
            }
            HideUnused();
            for (int i = giantPairs * 2; i < giants.Count; i++)
            {
                if (giants[i] != null) giants[i].SetActive(false);
            }
        }

        private void DrawSlipper(DodgeShape s, float frac, double renderTick)
        {
            var c = DodgeHazardView.CircleCenter(s, frac);
            int pick = Mathf.Abs((int)(s.X1 * 7f + s.Z1 * 13f)) % kit.slipperMaterials.Length;   // 탄마다 색이 다르게, 매 프레임 같게
            var t = Take("slipper" + pick, kit.slipperMesh, kit.slipperMaterials[pick]);
            float heading = DodgePropPose.HeadingDegrees(new Vector2(s.X1, s.Z1), new Vector2(s.X0, s.Z0));
            t.position = new Vector3(c.x, DodgePropPose.SlipperHeight, c.y);
            t.rotation = Quaternion.Euler(0f, heading + (float)(renderTick * DodgePropPose.SlipperSpinDegreesPerTick), 0f);
            t.localScale = Vector3.one * DodgePropPose.SlipperLength(s.Radius);
        }

        private void DrawJar(DodgeShape s, float frac, double renderTick)
        {
            var c = DodgeHazardView.CircleCenter(s, frac);
            var t = Take("jar", kit.jarMesh, kit.jarMaterial);
            float scale = DodgePropPose.JarScale(s.Radius);
            t.localScale = Vector3.one * scale;
            if (!s.Active)
            {
                // 예고: 들어올 자리에 서서 흔들린다
                t.position = new Vector3(c.x, scale * 0.5f, c.y);
                t.rotation = Quaternion.Euler(DodgePropPose.WobbleDegrees(s.Progress, renderTick), 0f, 0f);
                return;
            }
            // 켜짐: 옆으로 누워 진행 방향으로 구른다(긴 축 y를 진행 방향과 직각으로)
            float heading = DodgePropPose.HeadingDegrees(new Vector2(s.X1, s.Z1), new Vector2(s.X0, s.Z0));
            float roll = DodgePropPose.RollDegrees(renderTick, config.RockSpeed, s.Radius);
            t.position = new Vector3(c.x, scale * 0.5f, c.y);
            t.rotation = Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(roll, 0f, 0f) * Quaternion.Euler(0f, 0f, 90f);
        }

        private void DrawRope(DodgeShape s, int pairIndex)
        {
            var a = new Vector2(s.X0, s.Z0);
            var b = new Vector2(s.X1, s.Z1);
            float h = DodgePropPose.RopeHeight(s.Active, s.Progress) + DodgePropPose.RopeThickness * 0.5f;
            var t = Take("rope", DodgePropMeshes.Primitive(PrimitiveType.Cylinder), kit.ropeMaterial);
            var pa = new Vector3(a.x, h, a.y);
            var pb = new Vector3(b.x, h, b.y);
            t.position = (pa + pb) * 0.5f;
            t.rotation = Quaternion.FromToRotation(Vector3.up, pb - pa);
            t.localScale = new Vector3(DodgePropPose.RopeThickness, (pb - pa).magnitude * 0.5f, DodgePropPose.RopeThickness);

            if (chibiPrefab == null)
            {
                return;
            }
            var (ga, gb) = DodgePropPose.GiantSpots(a, b);
            PlaceGiant(pairIndex * 2, ga, a);
            PlaceGiant(pairIndex * 2 + 1, gb, b);
        }

        private void PlaceGiant(int index, Vector2 spot, Vector2 lookAt)
        {
            while (giants.Count <= index)
            {
                var g = UnityEngine.Object.Instantiate(chibiPrefab, root.transform);
                g.name = "DodgeGiant";
                g.transform.localScale = chibiPrefab.transform.localScale * DodgePropPose.GiantScale;
                ChibiOutfit.Apply(g, ChibiOutfit.ColorsFor("dodge-giant"));
                giants.Add(g);
            }
            var giant = giants[index];
            giant.SetActive(true);
            giant.transform.position = new Vector3(spot.x, 0f, spot.y);
            var look = new Vector3(lookAt.x - spot.x, 0f, lookAt.y - spot.y);
            if (look.sqrMagnitude > 1e-4f) giant.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        private Transform Take(string key, Mesh mesh, Material material)
        {
            if (!pools.TryGetValue(key, out var pool))
            {
                pool = new List<Transform>();
                pools[key] = pool;
            }
            used.TryGetValue(key, out int n);
            used[key] = n + 1;
            if (n < pool.Count)
            {
                pool[n].gameObject.SetActive(true);
                return pool[n];
            }
            // 콜라이더 없이 메시만 — 캐릭터 이동이 소품을 벽으로 여기지 않게.
            var go = new GameObject("DodgeProp_" + key);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pool.Add(go.transform);
            return go.transform;
        }

        private void HideUnused()
        {
            foreach (var kv in pools)
            {
                used.TryGetValue(kv.Key, out int n);
                for (int i = n; i < kv.Value.Count; i++) kv.Value[i].gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            pools.Clear();
            giants.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }
    }
}
