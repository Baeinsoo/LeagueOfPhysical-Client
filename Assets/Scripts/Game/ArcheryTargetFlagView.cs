using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 그 라운드 과녁 위에 다는 바람 깃발(실제 양궁 규정과 같은 자리: 과녁 위 30cm). 조준 중에도 과녁 바로 위라 보인다.
    /// 과녁이 움직이면 따라가고, 과녁이 없을 때는 숨긴다. 한 발 승부에서만 만든다.
    /// </summary>
    public class ArcheryTargetFlagView : IStartable, System.IDisposable
    {
        private const float AboveFace = 0.3f;
        private const float PoleHeight = 0.5f;

        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryArrowStickSystem stickSystem;

        private ArcheryTargetFlagDriver driver;
        private GameObject flag;
        private Transform cloth;
        private Mesh clothMesh;
        private Material poleMaterial;
        private Material clothMaterial;

        public ArcheryTargetFlagView(ArcheryCourse course, ArcheryWorld world, GameFramework.Runner.IRunner runner,
                                     ArcheryArrowStickSystem stickSystem)
        {
            this.course = course;
            this.world = world;
            this.runner = runner;
            this.stickSystem = stickSystem;
        }

        public void Start()
        {
            driver = new GameObject(nameof(ArcheryTargetFlagDriver)).AddComponent<ArcheryTargetFlagDriver>();
            driver.View = this;
        }

        public void Dispose()
        {
            if (driver != null)
            {
                driver.View = null;
                Object.Destroy(driver.gameObject);
                driver = null;
            }
            if (flag != null) { Object.Destroy(flag); flag = null; }
            if (clothMesh != null) { Object.Destroy(clothMesh); clothMesh = null; }
            if (poleMaterial != null) { Object.Destroy(poleMaterial); poleMaterial = null; }
            if (clothMaterial != null) { Object.Destroy(clothMaterial); clothMaterial = null; }
        }

        public void Apply()
        {
            var lane = course.SharedLane;
            if (course.IsShootOff == false || lane.HasValue == false
                || runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                Hide();
                return;
            }
            double interval = runner.tickUpdater.interval;
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long tick = (long)System.Math.Floor(renderTick);
            long start = world.GameplayStartTick;
            int index = course.IndexAt(tick, start);
            if (index < 0 || index >= course.StepCount
                || stickSystem.TryGetTarget(index, 0, out var target) == false
                || ArcheryTargetMotion.IsAlive(target, renderTick, (float)interval) == false)
            {
                Hide();
                return;
            }

            if (flag == null)
            {
                Build();
            }
            flag.SetActive(true);
            Vector3 forward = lane.Value.Forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 center = ArcheryTargetMotion.PositionAt(target, renderTick, (float)interval);
            flag.transform.SetPositionAndRotation(center + Vector3.up * (target.Radius + AboveFace),
                                                  Quaternion.LookRotation(-forward));
            //  HUD 바람 화살표와 같은 값(양수 = 사수 기준 오른쪽).
            float wind = Vector3.Dot(course.WindAt(tick, start), ArcheryTargetMotion.ShooterRightAxis(-lane.Value.Forward));
            cloth.localRotation = ArcheryFlagPose.LocalRotation(ArcheryFlagPose.At(wind, Time.time, 0f));
        }

        private void Hide()
        {
            if (flag != null)
            {
                flag.SetActive(false);
            }
        }

        private void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            poleMaterial = new Material(shader);
            poleMaterial.color = new Color(0.36f, 0.27f, 0.21f);
            poleMaterial.SetColor("_BaseColor", poleMaterial.color);
            clothMaterial = new Material(shader);
            clothMaterial.color = new Color(1f, 0.31f, 0.37f);   // #FF4F5E
            clothMaterial.SetColor("_BaseColor", clothMaterial.color);
            flag = new GameObject("ArcheryTargetFlag");

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var poleCollider = pole.GetComponent<Collider>();
            poleCollider.enabled = false;
            Object.Destroy(poleCollider);   // 판정 무관 — 화살이 걸리면 안 된다
            pole.transform.SetParent(flag.transform, false);
            pole.transform.localPosition = new Vector3(0f, PoleHeight * 0.5f, 0f);
            pole.transform.localScale = new Vector3(0.03f, PoleHeight, 0.03f);
            pole.GetComponent<Renderer>().sharedMaterial = poleMaterial;

            cloth = new GameObject("Cloth").transform;
            cloth.SetParent(flag.transform, false);
            cloth.localPosition = new Vector3(0f, PoleHeight, 0f);
            clothMesh = new Mesh { name = "TargetFlagCloth" };
            clothMesh.vertices = new[] { Vector3.zero, new Vector3(0.4f, -0.1f, 0f), new Vector3(0f, -0.25f, 0f) };
            clothMesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };   // 양면
            clothMesh.RecalculateNormals();
            cloth.gameObject.AddComponent<MeshFilter>().sharedMesh = clothMesh;
            var renderer = cloth.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = clothMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
