using UnityEngine;
using VContainer.Unity;
using LOP.UI;

namespace LOP
{
    /// <summary>
    /// 발사 버튼을 잡고 있는 동안 바닥에 조준선(끌 때)이나 자동 조준 대상 고리(누르고만 있을 때)를 그린다.
    /// 그려진 몸이 움직인 뒤에 놓아야 떨리지 않아 LateUpdate 발판(3100)으로 돈다.
    /// 메시·재질은 한 번만 만들어 재사용한다(매 프레임 할당 없음).
    /// </summary>
    public class KnockbackAimIndicatorView : IStartable, System.IDisposable
    {
        private const float Height = 0.03f;
        private const float LineWidth = 0.4f;
        private const float RingOuter = 0.9f;
        private const float RingInner = 0.7f;
        private const int RingSegments = 32;
        private static readonly Color AimColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color CancelColor = new Color(0.6f, 0.6f, 0.6f, 0.3f);
        private static readonly Color TargetColor = new Color(1f, 0.85f, 0.3f, 0.7f);

        private readonly KnockbackAimState aim;
        private readonly ActorRegistry actors;
        private readonly KnockbackClientState state;
        private readonly KnockbackConfig config;
        private readonly IGameDataStore gameDataStore;

        private Mesh lineMesh;
        private Mesh ringMesh;
        private Material material;
        private GameObject line;
        private GameObject ring;
        private KnockbackLateUpdateDriver driver;

        public KnockbackAimIndicatorView(KnockbackAimState aim, ActorRegistry actors, KnockbackClientState state,
                                         KnockbackConfig config, IGameDataStore gameDataStore)
        {
            this.aim = aim;
            this.actors = actors;
            this.state = state;
            this.config = config;
            this.gameDataStore = gameDataStore;
        }

        /// <summary>이 표시 종류에 칠할 색. 그리지 않는 종류는 null.</summary>
        public static Color? ColorOf(KnockbackPadViewModel.AimKind kind)
        {
            switch (kind)
            {
                case KnockbackPadViewModel.AimKind.Aim: return AimColor;
                case KnockbackPadViewModel.AimKind.Cancel: return CancelColor;
                case KnockbackPadViewModel.AimKind.AutoTarget: return TargetColor;
                default: return null;
            }
        }

        public void Start()
        {
            lineMesh = BuildLineMesh();
            ringMesh = BuildRingMesh();
            material = CreateTransparentMaterial();
            line = CreatePiece("AimLine", lineMesh);
            ring = CreatePiece("AimTargetRing", ringMesh);
            driver = KnockbackLateUpdateDriver.Create("KnockbackAimIndicatorDriver", Place);
        }

        private void Place()
        {
            line.SetActive(false);
            ring.SetActive(false);

            string me = gameDataStore?.userEntityId;
            var kind = aim.Kind;
            if (kind == KnockbackPadViewModel.AimKind.None || string.IsNullOrEmpty(me) || state.IsEliminated(me))
            {
                return;
            }
            var actor = actors.Get(me);
            if (actor == null || actor.visualGameObject == null)
            {
                return;
            }
            var body = actor.visualGameObject.transform.position;
            // 떨어지는 중에는 바닥 표시가 의미 없다.
            if (body.y < -0.05f)
            {
                return;
            }

            var color = ColorOf(kind).Value;
            material.color = color;
            if (kind == KnockbackPadViewModel.AimKind.AutoTarget)
            {
                ring.transform.position = new Vector3(aim.TargetXZ.x, Height, aim.TargetXZ.y);
                ring.SetActive(true);
            }
            else
            {
                line.transform.SetPositionAndRotation(new Vector3(body.x, Height, body.z), Quaternion.Euler(0f, aim.AimYaw, 0f));
                line.SetActive(true);
            }
        }

        // +z 방향으로 사거리만큼 뻗는 납작한 띠. 시작점이 원점이라 몸 발밑에 놓고 yaw만 돌리면 된다.
        private Mesh BuildLineMesh()
        {
            float half = LineWidth * 0.5f;
            var mesh = new Mesh { name = "AimLine" };
            mesh.vertices = new[]
            {
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f),
                new Vector3(half, 0f, config.ShotRange), new Vector3(-half, 0f, config.ShotRange),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRingMesh()
        {
            var verts = new Vector3[RingSegments * 2];
            var tris = new int[RingSegments * 6];
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[i * 2] = dir * RingInner;
                verts[i * 2 + 1] = dir * RingOuter;
                int next = (i + 1) % RingSegments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = next * 2 + 1; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = next * 2; tris[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = "AimRing", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateTransparentMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private GameObject CreatePiece(string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (line != null) Object.Destroy(line);
            if (ring != null) Object.Destroy(ring);
            if (lineMesh != null) Object.Destroy(lineMesh);
            if (ringMesh != null) Object.Destroy(ringMesh);
            if (material != null) Object.Destroy(material);
        }
    }
}
