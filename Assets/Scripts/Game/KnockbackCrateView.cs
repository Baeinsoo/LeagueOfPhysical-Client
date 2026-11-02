using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 보급 상자 그림. 서버 상태의 상자 한 개만으로 그린다 — 예고 동안은 바닥 그림자가 커지고 상자가 위에서 내려오며,
    /// 떨어진 뒤엔 떠다니며 천천히 돈다. 상태에서 사라지면(주웠다) 0.15초 작아지며 사라진다.
    /// 상자는 한 번에 하나뿐이라 오브젝트를 처음에 한 번만 만들어 계속 재사용한다.
    /// </summary>
    public class KnockbackCrateView : IStartable, System.IDisposable
    {
        private const float ShadowHeight = 0.04f;
        private const int ShadowSegments = 32;
        private static readonly Color CrateYellow = new Color(1f, 0.85f, 0.15f);
        private static readonly Color CrateOrange = new Color(1f, 0.45f, 0.05f);

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackClientState state;
        private readonly KnockbackConfig config;

        private GameObject root;
        private GameObject shadow;
        private GameObject crate;
        private Mesh shadowMesh;
        private Material shadowMaterial;
        private Material yellowMaterial;
        private Material orangeMaterial;
        private KnockbackLateUpdateDriver driver;

        private int shownCrateId = -1;
        private Vector2 shownPosition;
        private float vanishStartedAt = -1f;

        public KnockbackCrateView(GameFramework.Runner.IRunner runner, KnockbackClientState state, KnockbackConfig config)
        {
            this.runner = runner;
            this.state = state;
            this.config = config;
        }

        public void Start()
        {
            root = new GameObject("KnockbackCrate");
            shadowMesh = BuildDiscMesh();
            shadowMaterial = KnockbackAimIndicatorView.CreateTransparentMaterial();
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            yellowMaterial = new Material(unlit) { color = CrateYellow };
            orangeMaterial = new Material(unlit) { color = CrateOrange };

            shadow = new GameObject("CrateShadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            var sr = shadow.AddComponent<MeshRenderer>();
            sr.sharedMaterial = shadowMaterial;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;

            crate = CreateCube("CrateBody", root.transform, yellowMaterial, Vector3.one * KnockbackCrateDisplay.CrateSize);
            // 노랑 몸에 주황 띠 두 줄(가로·세로)을 둘러 멀리서도 눈에 띄게 한다.
            CreateCube("BeltA", crate.transform, orangeMaterial, new Vector3(1.04f, 0.22f, 1.04f));
            CreateCube("BeltB", crate.transform, orangeMaterial, new Vector3(0.22f, 1.04f, 1.04f));

            shadow.SetActive(false);
            crate.SetActive(false);
            driver = KnockbackLateUpdateDriver.Create("KnockbackCrateViewDriver", Place);
        }

        private void Place()
        {
            if (runner?.tickUpdater == null || crate == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            var info = state.Crate;
            if (info.HasValue)
            {
                var c = info.Value;
                if (c.Id != shownCrateId)
                {
                    shownCrateId = c.Id;
                    vanishStartedAt = -1f;
                }
                shownPosition = c.Position;
                vanishStartedAt = -1f;
                DrawLive(c, renderTick);
                return;
            }

            if (shownCrateId == -1)
            {
                return;
            }
            DrawVanish();
        }

        private void DrawLive(KnockbackCrateInfo c, double renderTick)
        {
            int warnTicks = config.CrateWarnTicks;
            float p = KnockbackCrateDisplay.WarnProgress01(renderTick, c.LandTick, warnTicks);
            bool landed = renderTick >= c.LandTick;
            float size = KnockbackCrateDisplay.CrateSize;

            shadow.SetActive(!landed);
            if (!landed)
            {
                float r = KnockbackCrateDisplay.ShadowRadius(p);
                shadow.transform.position = new Vector3(c.Position.x, ShadowHeight, c.Position.y);
                shadow.transform.localScale = new Vector3(r, 1f, r);
                shadowMaterial.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.35f, 0.7f, p));
            }

            float y = landed
                ? size * 0.5f + KnockbackCrateDisplay.HoverAmplitude + KnockbackCrateDisplay.HoverOffset(Time.unscaledTime)
                : size * 0.5f + KnockbackCrateDisplay.FallHeight(renderTick, c.LandTick, warnTicks);
            crate.SetActive(true);
            crate.transform.SetPositionAndRotation(
                new Vector3(c.Position.x, y, c.Position.y),
                Quaternion.Euler(0f, Time.unscaledTime * KnockbackCrateDisplay.SpinDegreesPerSecond, 0f));
            crate.transform.localScale = Vector3.one * size;
        }

        private void DrawVanish()
        {
            if (vanishStartedAt < 0f)
            {
                vanishStartedAt = Time.unscaledTime;
            }
            float scale = KnockbackCrateDisplay.VanishScale(Time.unscaledTime - vanishStartedAt);
            shadow.SetActive(false);
            if (scale <= 0f)
            {
                crate.SetActive(false);
                shownCrateId = -1;
                vanishStartedAt = -1f;
                return;
            }
            crate.transform.localScale = Vector3.one * (KnockbackCrateDisplay.CrateSize * scale);
        }

        private static GameObject CreateCube(string name, Transform parent, Material material, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // 반지름 1짜리 납작한 원판(+y를 본다). 크기는 스케일로 바꾼다.
        private static Mesh BuildDiscMesh()
        {
            var verts = new Vector3[ShadowSegments + 1];
            var tris = new int[ShadowSegments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < ShadowSegments; i++)
            {
                float a = i * Mathf.PI * 2f / ShadowSegments;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                int t = i * 3;
                tris[t] = 0;
                tris[t + 1] = (i + 1) % ShadowSegments + 1;
                tris[t + 2] = i + 1;
            }
            var mesh = new Mesh { name = "CrateShadow", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
            if (shadowMesh != null) Object.Destroy(shadowMesh);
            if (shadowMaterial != null) Object.Destroy(shadowMaterial);
            if (yellowMaterial != null) Object.Destroy(yellowMaterial);
            if (orangeMaterial != null) Object.Destroy(orangeMaterial);
        }
    }
}
