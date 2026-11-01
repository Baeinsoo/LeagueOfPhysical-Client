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
        private const float TrailSeconds = 0.18f;
        private const float TrailWidthRatio = 0.8f;
        private const float TrailHeadAlpha = 0.6f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackWorld world;
        private readonly KnockbackConfig config;
        private readonly ActorRegistry actors;
        private readonly KnockbackClientState state;
        private readonly KnockbackPillars pillars;

        private readonly Dictionary<(string shooterId, long fireTick), GameObject> spheres =
            new Dictionary<(string, long), GameObject>();
        private readonly HashSet<(string, long)> hidden = new HashSet<(string, long)>();
        private readonly HashSet<(string, long)> alive = new HashSet<(string, long)>();
        private readonly List<(string, long)> scratch = new List<(string, long)>();
        private readonly List<Vector2> standingPillars = new List<Vector2>();
        private readonly List<(string id, Vector2 position)> drawnBodies = new List<(string, Vector2)>();
        private readonly Stack<GameObject> pool = new Stack<GameObject>();
        private readonly Dictionary<(string, long), LineRenderer> trails = new Dictionary<(string, long), LineRenderer>();
        private readonly Stack<LineRenderer> trailPool = new Stack<LineRenderer>();
        private readonly Dictionary<(string, long), float> firstSeenSeconds = new Dictionary<(string, long), float>();
        private readonly Vector3[] trailPoints = new Vector3[2];
        private Material trailMaterial;

        private Material material;
        private Transform root;
        private KnockbackLateUpdateDriver driver;

        public KnockbackShotView(GameFramework.Runner.IRunner runner, KnockbackWorld world, KnockbackConfig config,
                                 ActorRegistry actors, KnockbackClientState state, KnockbackPillars pillars)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.actors = actors;
            this.state = state;
            this.pillars = pillars;
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
            // 기둥은 틱 단위 사실이라 정수 틱으로 묻는다(바닥 그림과 같은 값).
            pillars.CollectStanding((long)System.Math.Floor(renderTick), standingPillars);
            float pillarReach = pillars.Radius + config.ShotRadius;
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
                if (InsideStandingPillar(p, standingPillars, pillarReach)
                    || TouchesDrawnBody(p, shot.ShooterId, drawnBodies, BodyHitRadius, state.TeamOf))
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
                UpdateTrail(key, shot, renderTick);
            }

            // 목록에서 빠진 탄(맞음·사거리 끝)은 그림도 걷는다.
            scratch.Clear();
            foreach (var key in spheres.Keys)
            {
                if (!alive.Contains(key)) scratch.Add(key);
            }
            for (int i = 0; i < scratch.Count; i++) Release(scratch[i]);
            hidden.RemoveWhere(k => !alive.Contains(k));

            scratch.Clear();
            foreach (var key in firstSeenSeconds.Keys)
            {
                if (!alive.Contains(key)) scratch.Add(key);
            }
            for (int i = 0; i < scratch.Count; i++) firstSeenSeconds.Remove(scratch[i]);
        }

        // 남의 탄은 비행 중간에 처음 보인다. 그때 이미 흘러 있던 시간을 적어 두고 꼬리를 총구부터 그어 순간이동처럼 안 보이게 한다.
        private void UpdateTrail((string, long) key, in KnockbackShot shot, double renderTick)
        {
            float seconds = Mathf.Max(0f, (float)(renderTick - shot.FireTick)) * KnockbackConfig.TickInterval;
            if (!firstSeenSeconds.TryGetValue(key, out float firstSeen))
            {
                firstSeen = seconds;
                firstSeenSeconds[key] = seconds;
            }

            var (head, tail) = TrailEnds(shot.Origin, shot.Direction, config.ShotSpeed, config.ShotRange,
                                         seconds, firstSeen, TrailSeconds);
            if ((head - tail).sqrMagnitude < 1e-6f)
            {
                ReleaseTrail(key);
                return;
            }

            if (!trails.TryGetValue(key, out var line))
            {
                line = RentTrail();
                trails[key] = line;
            }
            trailPoints[0] = new Vector3(head.x, 1f, head.y);
            trailPoints[1] = new Vector3(tail.x, 1f, tail.y);
            line.SetPositions(trailPoints);
        }

        /// <summary>
        /// 꼬리선의 머리(지금 탄 자리)와 끝. 끝은 <see cref="ArcheryArrowDisplay.TrailTailSeconds"/>가 정한 시각의 탄 자리다.
        /// 둘 다 사거리에서 멈추므로 머리가 멈추면 끝이 따라와 길이가 0이 된다.
        /// </summary>
        public static (Vector2 head, Vector2 tail) TrailEnds(Vector2 origin, Vector2 direction, float speed, float range,
                                                             float seconds, float firstSeen, float trailSeconds)
        {
            float tailSeconds = ArcheryArrowDisplay.TrailTailSeconds(seconds, firstSeen, trailSeconds);
            float headDistance = Mathf.Min(speed * Mathf.Max(0f, seconds), range);
            float tailDistance = Mathf.Min(speed * Mathf.Max(0f, tailSeconds), range);
            return (origin + direction * headDistance, origin + direction * tailDistance);
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

        /// <summary>탄 자리가 쏜 사람이 아닌 그려진 몸에 닿았나. 그려진 몸 목록은 탈락·그림 없는 몸을 뺀 것이다. 같은 팀 몸은 지나간다(서버 판정과 같게).</summary>
        public static bool TouchesDrawnBody(Vector2 p, string shooterId,
                                            IReadOnlyList<(string id, Vector2 position)> bodies, float radius,
                                            System.Func<string, int> teamOf)
        {
            float sq = radius * radius;
            int shooterTeam = teamOf(shooterId);
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].id == shooterId) continue;
                if (KnockbackTeams.SameTeam(shooterTeam, teamOf(bodies[i].id))) continue;
                if ((bodies[i].position - p).sqrMagnitude <= sq) return true;
            }
            return false;
        }

        /// <summary>탄 자리가 서 있는 기둥 안인가(기둥 반지름 + 탄 반지름). 서버가 기둥에 막힌 탄을 지우기 전의 연출이다.</summary>
        public static bool InsideStandingPillar(Vector2 p, IReadOnlyList<Vector2> standing, float reach)
        {
            float sq = reach * reach;
            for (int i = 0; i < standing.Count; i++)
            {
                if ((standing[i] - p).sqrMagnitude <= sq) return true;
            }
            return false;
        }

        private void EnsureObjects()
        {
            if (root != null) return;
            root = new GameObject("KnockbackShots").transform;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            // 선의 점 색(알파)을 그대로 쓰는 셰이더라야 꼬리가 흐려진다 — Lit은 알파를 무시한다.
            trailMaterial = new Material(Shader.Find("Sprites/Default"));
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

        private LineRenderer RentTrail()
        {
            if (trailPool.Count > 0)
            {
                var pooled = trailPool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            var go = new GameObject("KnockbackShotTrail");
            go.transform.SetParent(root, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = trailMaterial;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = Diameter * TrailWidthRatio;
            line.endWidth = 0f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(TrailHeadAlpha, 0f), new GradientAlphaKey(0f, 1f) });
            line.colorGradient = gradient;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void ReleaseTrail((string, long) key)
        {
            if (trails.TryGetValue(key, out var line))
            {
                line.gameObject.SetActive(false);
                trailPool.Push(line);
                trails.Remove(key);
            }
        }

        private void Release((string, long) key)
        {
            ReleaseTrail(key);
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
            if (trailMaterial != null) Object.Destroy(trailMaterial);
        }
    }
}
