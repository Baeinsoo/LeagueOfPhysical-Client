using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 정해진 라운드에 과녁 뒤를 종종거리며 지나가는 닭(<see cref="ArcheryChickenPath"/>). 화살이 가까이 떨어지면 펄쩍 뛰고
    /// 깃털이 날린다. 판정과는 상관없다 — 닭에는 충돌체가 없다.
    /// </summary>
    public class ArcheryChickenView : IStartable, System.IDisposable
    {
        private const float JumpSeconds = 0.6f;
        private const float FeatherSeconds = 1.2f;

        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly ArcheryConfig config;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryArrowLandings landings;

        private ArcheryChickenDriver driver;
        private Material material;
        private GameObject chicken;
        private Transform body;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftWing;
        private Transform rightWing;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        private ArcheryChickenState state;
        private int round = -1;
        private int scaredRound = -1;
        private float scaredAt = float.NegativeInfinity;
        private readonly List<(Transform t, Vector3 velocity, float bornAt)> feathers = new List<(Transform, Vector3, float)>();

        public event System.Action<string> Scared;

        public ArcheryChickenView(ArcheryCourse course, ArcheryWorld world, ArcheryConfig config,
                                  GameFramework.Runner.IRunner runner, ArcheryArrowLandings landings)
        {
            this.course = course;
            this.world = world;
            this.config = config;
            this.runner = runner;
            this.landings = landings;
        }

        public void Start()
        {
            landings.Landed += OnLanded;
            driver = new GameObject(nameof(ArcheryChickenDriver)).AddComponent<ArcheryChickenDriver>();
            driver.View = this;
        }

        public void Dispose()
        {
            landings.Landed -= OnLanded;
            if (driver != null)
            {
                driver.View = null;
                Object.Destroy(driver.gameObject);
                driver = null;
            }
            foreach (var f in feathers)
            {
                if (f.t != null) { Object.Destroy(f.t.gameObject); }
            }
            feathers.Clear();
            if (chicken != null) { Object.Destroy(chicken); chicken = null; }
            if (material != null) { Object.Destroy(material); material = null; }
        }

        public void Apply()
        {
            UpdateFeathers();
            state = default;
            var lane = course.SharedLane;
            if (course.IsShootOff == false || lane.HasValue == false
                || runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                Hide();
                return;
            }

            double interval = runner.tickUpdater.interval;
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long start = world.GameplayStartTick;
            round = course.IndexAt((long)System.Math.Floor(renderTick), start);
            if (round < 0 || round >= course.StepCount || ArcheryChickenPath.IsChickenRound(round) == false
                || round >= config.Range.Stands.Count)
            {
                Hide();
                return;
            }

            double roundStart = course.RoundCloseTick(round, start) - course.ExposureTicksAt(round);
            Vector3 forward = lane.Value.Forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = ArcheryTargetMotion.ShooterRightAxis(-forward);
            Vector3 stand = lane.Value.Stands[config.Range.Stands[round].StandIndex];
            state = ArcheryChickenPath.At(round, (renderTick - roundStart) * interval, stand, forward, right,
                                          lane.Value.ShooterPosition.y);
            if (state.Visible == false)
            {
                Hide();
                return;
            }

            if (chicken == null)
            {
                Build();
            }
            chicken.SetActive(true);
            float now = Time.time;
            float sinceScare = now - scaredAt;
            bool jumping = scaredRound == round && sinceScare < JumpSeconds;
            float hop = jumping ? Mathf.Sin(sinceScare / JumpSeconds * Mathf.PI) * 0.5f : 0f;
            float bob = Mathf.Abs(Mathf.Sin(state.StepPhase * Mathf.PI)) * 0.03f;
            chicken.transform.SetPositionAndRotation(state.Position + Vector3.up * (hop + bob),
                                                     Quaternion.LookRotation(state.Heading));
            float legSwing = Mathf.Sin(state.StepPhase * Mathf.PI) * 30f;
            leftLeg.localRotation = Quaternion.AngleAxis(legSwing, Vector3.right);
            rightLeg.localRotation = Quaternion.AngleAxis(-legSwing, Vector3.right);
            float flap = jumping ? Mathf.Sin(sinceScare * 40f) * 60f : 0f;
            leftWing.localRotation = Quaternion.AngleAxis(flap, Vector3.forward);
            rightWing.localRotation = Quaternion.AngleAxis(-flap, Vector3.forward);
        }

        private void Hide()
        {
            if (chicken != null)
            {
                chicken.SetActive(false);
            }
        }

        private void OnLanded(ArcheryArrowLanding landing)
        {
            if (landing.Kind != ArcheryLandingKind.Ground || state.Visible == false || scaredRound == round)
            {
                return;
            }
            Vector3 d = landing.Point - state.Position;
            d.y = 0f;
            if (d.magnitude > ArcheryChickenPath.ScareRadius)
            {
                return;
            }
            scaredRound = round;
            scaredAt = Time.time;
            BurstFeathers(state.Position + Vector3.up * 0.35f);
            Scared?.Invoke(landing.ShooterId);
        }

        private void Build()
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            chicken = new GameObject("ArcheryChicken");
            var white = new Color(0.97f, 0.96f, 0.93f);
            body = Part(chicken.transform, PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0f), new Vector3(0.35f, 0.3f, 0.45f), white);
            Part(chicken.transform, PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0.18f), Vector3.one * 0.18f, white);
            Part(chicken.transform, PrimitiveType.Cube, new Vector3(0f, 0.61f, 0.18f), new Vector3(0.04f, 0.08f, 0.1f), new Color(0.9f, 0.15f, 0.15f));
            Part(chicken.transform, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.3f), new Vector3(0.05f, 0.04f, 0.08f), new Color(1f, 0.75f, 0.1f));
            leftLeg = Pivot(chicken.transform, new Vector3(-0.07f, 0.16f, 0f));
            Part(leftLeg, PrimitiveType.Cube, new Vector3(0f, -0.08f, 0f), new Vector3(0.03f, 0.16f, 0.03f), new Color(1f, 0.75f, 0.1f));
            rightLeg = Pivot(chicken.transform, new Vector3(0.07f, 0.16f, 0f));
            Part(rightLeg, PrimitiveType.Cube, new Vector3(0f, -0.08f, 0f), new Vector3(0.03f, 0.16f, 0.03f), new Color(1f, 0.75f, 0.1f));
            leftWing = Pivot(chicken.transform, new Vector3(-0.17f, 0.36f, 0f));
            Part(leftWing, PrimitiveType.Cube, new Vector3(-0.02f, -0.06f, 0f), new Vector3(0.03f, 0.15f, 0.25f), white);
            rightWing = Pivot(chicken.transform, new Vector3(0.17f, 0.36f, 0f));
            Part(rightWing, PrimitiveType.Cube, new Vector3(0.02f, -0.06f, 0f), new Vector3(0.03f, 0.15f, 0.25f), white);
        }

        private static Transform Pivot(Transform parent, Vector3 localPosition)
        {
            var t = new GameObject("Pivot").transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        private Transform Part(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            //  화살이 닭에 걸리면 안 된다(판정 무관) — Destroy는 프레임 끝이라 먼저 끈다.
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            block.Clear();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }

        //  깃털 — 작은 흰 조각 몇 개가 튀었다가 천천히 떨어진다(파티클 셰이더 없이).
        private void BurstFeathers(Vector3 at)
        {
            if (material == null)
            {
                return;
            }
            for (int i = 0; i < 12; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
                go.transform.position = at;
                go.transform.localScale = new Vector3(0.06f, 0.01f, 0.03f);
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                block.Clear();
                block.SetColor("_BaseColor", Color.white);
                renderer.SetPropertyBlock(block);
                Vector3 velocity = Random.insideUnitSphere * 1.5f + Vector3.up * 1.5f;
                feathers.Add((go.transform, velocity, Time.time));
            }
        }

        private void UpdateFeathers()
        {
            float dt = Time.deltaTime;
            for (int i = feathers.Count - 1; i >= 0; i--)
            {
                var f = feathers[i];
                if (f.t == null || Time.time - f.bornAt > FeatherSeconds)
                {
                    if (f.t != null) { Object.Destroy(f.t.gameObject); }
                    feathers.RemoveAt(i);
                    continue;
                }
                f.velocity += Vector3.down * 3f * dt;
                f.t.position += f.velocity * dt;
                f.t.Rotate(400f * dt, 250f * dt, 0f);
                feathers[i] = f;
            }
        }
    }
}
