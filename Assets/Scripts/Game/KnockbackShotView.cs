using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 넉백 탄의 그림. 월드의 탄 목록과 화면 시각(소수 틱)만으로 자리를 계산해 흰 구를 놓는다.
    /// 내 탄이 화면 속 남의 그려진 몸에 닿으면 바로 숨긴다 — 서버 확정(KnockbackHitEvent)이 오기 전의 연출이라
    /// 서버가 빗나갔다고 하더라도 되돌리지 않고 그냥 사라진 채 둔다.
    /// </summary>
    public class KnockbackShotView : IStartable, System.IDisposable
    {
        private const float Diameter = 0.56f;
        private const float BodyHitRadius = 0.88f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackWorld world;
        private readonly KnockbackConfig config;
        private readonly ActorRegistry actors;
        private readonly IPlayerContext playerContext;
        private readonly KnockbackClientState state;

        private readonly Dictionary<(string shooterId, long fireTick), GameObject> spheres =
            new Dictionary<(string, long), GameObject>();
        private readonly HashSet<(string, long)> hidden = new HashSet<(string, long)>();
        private readonly HashSet<(string, long)> alive = new HashSet<(string, long)>();
        private readonly List<(string, long)> scratch = new List<(string, long)>();
        private readonly Stack<GameObject> pool = new Stack<GameObject>();

        private Material material;
        private Transform root;
        private KnockbackLateUpdateDriver driver;

        public KnockbackShotView(GameFramework.Runner.IRunner runner, KnockbackWorld world, KnockbackConfig config,
                                 ActorRegistry actors, IPlayerContext playerContext, KnockbackClientState state)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.actors = actors;
            this.playerContext = playerContext;
            this.state = state;
        }

        // 남의 그려진 몸 자리를 읽으므로 보간기 뒤(3100)에서 돈다.
        public void Start()
        {
            driver = KnockbackLateUpdateDriver.Create("KnockbackShotViewDriver", Place);
        }

        private void Place()
        {
            if (runner?.tickUpdater == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            EnsureObjects();
            var shots = world.Shots.Shots;
            alive.Clear();
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                var key = (shot.ShooterId, shot.FireTick);
                alive.Add(key);
                if (hidden.Contains(key))
                {
                    continue;
                }

                var p = PositionAt(shot, renderTick);
                if (shot.ShooterId == playerContext.entityId && TouchesDrawnBody(p))
                {
                    hidden.Add(key);
                    Release(key);
                    continue;
                }

                if (!spheres.TryGetValue(key, out var go))
                {
                    go = Rent();
                    spheres[key] = go;
                }
                go.transform.position = new Vector3(p.x, 1f, p.y);
            }

            // 목록에서 빠진 탄(맞음·사거리 끝)은 그림도 걷는다.
            scratch.Clear();
            foreach (var key in spheres.Keys)
            {
                if (!alive.Contains(key)) scratch.Add(key);
            }
            for (int i = 0; i < scratch.Count; i++) Release(scratch[i]);
            hidden.RemoveWhere(k => !alive.Contains(k));
        }

        /// <summary>정수 틱 식을 소수 틱으로 넓힌 것. 화면 시각이 발사 틱보다 앞서지 않았으면 총구에 둔다.</summary>
        internal Vector2 PositionAt(in KnockbackShot shot, double renderTick)
        {
            float ticks = Mathf.Max(0f, (float)(renderTick - shot.FireTick));
            return shot.Origin + shot.Direction * (config.ShotSpeed * ticks * KnockbackConfig.TickInterval);
        }

        private bool TouchesDrawnBody(Vector2 p)
        {
            float sq = BodyHitRadius * BodyHitRadius;
            foreach (var actor in actors.All)
            {
                if (actor.entityId == playerContext.entityId || state.IsEliminated(actor.entityId) || actor.visualGameObject == null)
                {
                    continue;
                }
                var v = actor.visualGameObject.transform.position;
                float dx = v.x - p.x, dz = v.z - p.y;
                if (dx * dx + dz * dz <= sq) return true;
            }
            return false;
        }

        private void EnsureObjects()
        {
            if (root != null) return;
            root = new GameObject("KnockbackShots").transform;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
        }

        private GameObject Rent()
        {
            GameObject go;
            if (pool.Count > 0)
            {
                go = pool.Pop();
                go.SetActive(true);
                return go;
            }
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "KnockbackShot";
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * Diameter;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private void Release((string, long) key)
        {
            if (spheres.TryGetValue(key, out var go))
            {
                go.SetActive(false);
                pool.Push(go);
                spheres.Remove(key);
            }
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root.gameObject);
            if (material != null) Object.Destroy(material);
        }
    }
}
