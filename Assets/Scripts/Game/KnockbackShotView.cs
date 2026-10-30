using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 넉백 탄의 그림. 월드의 탄 목록과 화면 시각(소수 틱)만으로 자리를 계산해 흰 구를 놓는다.
    /// 탄이 화면 속 그려진 몸(쏜 사람 빼고)에 닿으면 바로 숨긴다 — 서버 확정(KnockbackHitEvent)이 오기 전의 연출이라
    /// 서버가 빗나갔다고 하더라도 되돌리지 않고 그냥 사라진 채 둔다. 남이 쏜 탄도 같다: 탄은 앞선 예측 시계로,
    /// 남의 몸은 뒤처진 보간 시계로 그려져서, 숨기지 않으면 탄이 맞을 몸을 몇 m 뚫고 지나간 뒤에야 사라진다.
    /// </summary>
    public class KnockbackShotView : IStartable, System.IDisposable
    {
        private const float Diameter = 0.56f;
        private const float BodyHitRadius = 0.88f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackWorld world;
        private readonly KnockbackConfig config;
        private readonly ActorRegistry actors;
        private readonly KnockbackClientState state;

        private readonly Dictionary<(string shooterId, long fireTick), GameObject> spheres =
            new Dictionary<(string, long), GameObject>();
        private readonly HashSet<(string, long)> hidden = new HashSet<(string, long)>();
        private readonly HashSet<(string, long)> alive = new HashSet<(string, long)>();
        private readonly List<(string, long)> scratch = new List<(string, long)>();
        private readonly List<(string id, Vector2 position)> drawnBodies = new List<(string, Vector2)>();
        private readonly Stack<GameObject> pool = new Stack<GameObject>();

        private Material material;
        private Transform root;
        private KnockbackLateUpdateDriver driver;

        public KnockbackShotView(GameFramework.Runner.IRunner runner, KnockbackWorld world, KnockbackConfig config,
                                 ActorRegistry actors, KnockbackClientState state)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.actors = actors;
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
            CollectDrawnBodies();
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
                if (TouchesDrawnBody(p, shot.ShooterId, drawnBodies, BodyHitRadius))
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

        /// <summary>
        /// 정수 틱 식을 소수 틱으로 넓힌 것. 화면 시각이 발사 틱보다 앞서지 않았으면 총구에 둔다.
        /// 사거리 끝에서 멈춘다 — 탄은 사거리를 넘는 틱까지 살아 있지만(그 틱 선분도 판정) 판정은 사거리까지만 본다.
        /// </summary>
        internal Vector2 PositionAt(in KnockbackShot shot, double renderTick)
        {
            float ticks = Mathf.Max(0f, (float)(renderTick - shot.FireTick));
            float distance = Mathf.Min(config.ShotSpeed * ticks * KnockbackConfig.TickInterval, config.ShotRange);
            return shot.Origin + shot.Direction * distance;
        }

        private void CollectDrawnBodies()
        {
            drawnBodies.Clear();
            foreach (var actor in actors.All)
            {
                if (state.IsEliminated(actor.entityId) || actor.visualGameObject == null)
                {
                    continue;
                }
                var v = actor.visualGameObject.transform.position;
                drawnBodies.Add((actor.entityId, new Vector2(v.x, v.z)));
            }
        }

        /// <summary>탄 자리가 쏜 사람이 아닌 그려진 몸에 닿았나. 그려진 몸 목록은 탈락·그림 없는 몸을 뺀 것이다.</summary>
        public static bool TouchesDrawnBody(Vector2 p, string shooterId,
                                            IReadOnlyList<(string id, Vector2 position)> bodies, float radius)
        {
            float sq = radius * radius;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].id == shooterId) continue;
                if ((bodies[i].position - p).sqrMagnitude <= sq) return true;
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
