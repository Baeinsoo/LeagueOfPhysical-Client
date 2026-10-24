using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 말 활쏘기 원의 그림: 지금 원은 파란 반투명 벽, 다음 원은 바닥 위 흰 선.
    /// 화살과 같은 화면 시각을 쓴다. 그림일 뿐이라 콜라이더가 없다(있으면 화살·조준이 막힌다).
    /// 같은 시각을 HUD(문구·미니맵)에도 <see cref="MountedArcheryClientState"/>로 건넨다.
    /// </summary>
    public class MountedZoneView : ILateTickable, System.IDisposable
    {
        private const int Segments = 64;
        private const float WallHeight = 40f;
        private const float LineY = 0.3f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly MountedArcheryWorld world;
        private readonly MountedArcheryConfig config;
        private readonly MountedArcheryClientState state;

        private GameObject wall;
        private LineRenderer line;
        private Mesh wallMesh;
        private Material wallMaterial;
        private Material lineMaterial;
        private readonly Vector3[] points = new Vector3[Segments];

        public MountedZoneView(GameFramework.Runner.IRunner runner, MountedArcheryWorld world,
                               MountedArcheryConfig config, MountedArcheryClientState state)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.state = state;
        }

        public void LateTick()
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

            long start = world.GameplayStartTick;
            long firstReveal = start == long.MaxValue
                ? long.MaxValue
                : start + (long)System.Math.Ceiling(config.ZoneFirstRevealSeconds / interval);
            state.SetZoneClock(renderTick, (float)interval, firstReveal, config.ZoneStages.Count, config.ZoneInitialRadius);

            EnsureObjects();
            if (wall == null)
            {
                return;
            }

            var current = state.CircleAtRender(renderTick);
            wall.transform.position = new Vector3(current.center.x, 0f, current.center.y);
            wall.transform.localScale = new Vector3(current.radius, WallHeight, current.radius);

            bool hasNext = state.TryGetNextCircle((long)System.Math.Floor(renderTick), out var next);
            line.enabled = hasNext;
            if (hasNext)
            {
                for (int i = 0; i < Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    points[i] = new Vector3(next.center.x + Mathf.Cos(a) * next.radius, LineY,
                                            next.center.y + Mathf.Sin(a) * next.radius);
                }
                line.SetPositions(points);
            }
        }

        private void EnsureObjects()
        {
            if (wall != null)
            {
                return;
            }
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return;   // 셰이더가 빌드에 없으면 원을 안 그린다 — 판정은 서버가 한다
            }

            wallMaterial = MakeTransparent(shader, new Color(0.15f, 0.4f, 1f, 0.28f));
            lineMaterial = new Material(shader);
            lineMaterial.SetColor("_BaseColor", Color.white);

            wallMesh = BuildTube();
            wall = new GameObject("MountedZoneWall");
            wall.AddComponent<MeshFilter>().sharedMesh = wallMesh;
            var renderer = wall.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = wallMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var lineObject = new GameObject("MountedZoneNextLine");
            lineObject.transform.SetParent(wall.transform, false);
            line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = 0.4f;
            line.sharedMaterial = lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
        }

        private static Material MakeTransparent(Shader shader, Color color)
        {
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);   // 원 안에서도 벽이 보여야 한다
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            return m;
        }

        // 반지름 1·높이 1짜리 뚜껑 없는 통. 크기는 스케일로 준다.
        private static Mesh BuildTube()
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = p;
                vertices[i * 2 + 1] = p + Vector3.up;
            }
            for (int i = 0; i < Segments; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "MountedZoneTube", vertices = vertices, triangles = triangles };
            // 크기를 스케일로 키우므로 경계 상자도 넉넉히 — 작으면 카메라가 벽 쪽을 안 보는 줄 알고 잘라낸다.
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(4f, 1f, 4f));
            return mesh;
        }

        public void Dispose()
        {
            if (wall != null) Object.Destroy(wall);
            if (wallMesh != null) Object.Destroy(wallMesh);
            if (wallMaterial != null) Object.Destroy(wallMaterial);
            if (lineMaterial != null) Object.Destroy(lineMaterial);
        }
    }
}
