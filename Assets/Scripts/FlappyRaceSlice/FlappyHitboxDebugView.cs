using GameFramework.Runner;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// [진단용] <b>판정 모양</b>을 게임 화면에 그대로 그린다 — 새의 몸과 맵 콜라이더.
    ///
    /// <para><b>왜 필요한가</b>: 겉모습과 판정이 어긋나 있어도 눈으로는 "닿은 것 같은데"까지만
    /// 알 수 있고, 어느 쪽이 얼마나 어긋났는지는 알 수 없다. 실제로 그 어긋남이 있었다 —
    /// 판정 구는 <b>발밑 기준</b>(pos ~ pos+높이)인데 겉모습 프리팹은 원점을 중심으로 대칭이라,
    /// 새가 0.45m 낮게 그려지고 판정이 위로 튀어나왔다.</para>
    ///
    /// <para><b>Gizmos 토글에 기대지 않는다</b>: <c>Debug.DrawLine</c>이나 <c>OnDrawGizmos</c>는
    /// 씬 뷰(또는 게임 뷰의 Gizmos 스위치)에서만 보인다. 실제로 플레이하면서 봐야 하는 물건이라
    /// 카메라가 그리는 단계에 직접 선을 넣는다.</para>
    ///
    /// <para><b>판정 모양을 <i>그려지는 자세</i> 위에 얹는다</b> — 시뮬 상태(정수 틱)가 아니라
    /// 보간된 겉모습 트랜스폼을 기준으로 삼는다. 시뮬 자세로 그리면 세로 속도가 빠를 때 한 틱
    /// (최대 0.6m)만큼 그림과 어긋나 <b>선이 덜덜 떨린다</b> — 그 떨림은 정상(시뮬과 렌더는
    /// 일부러 다른 시각을 본다)이지만, 여기서 묻는 것은 "그려지는 새가 자기 판정을 정직하게
    /// 보여 주나"라서 둘을 같은 시각에 세워야 답이 나온다.</para>
    /// </summary>
    public class FlappyHitboxDebugView : VContainer.Unity.IStartable, System.IDisposable
    {
        public const string EditorPrefsKey = "LOP.Debug.Hitbox";

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyConfig config;
        private GameObject host;

        public FlappyHitboxDebugView(GameFramework.World.EntityRegistry entityRegistry,
                                     FlappyConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.config = config;
        }

        public void Start()
        {
            host = new GameObject("[Debug] Hitbox");
            var drawer = host.AddComponent<Drawer>();
            drawer.Init(entityRegistry, config);
        }

        public void Dispose()
        {
            if (host != null)
            {
                Object.Destroy(host);
                host = null;
            }
        }

        /// <summary>카메라가 그리는 단계에 즉시모드 선을 넣는다.</summary>
        private class Drawer : MonoBehaviour
        {
            //  판정면이 있는 평면. 맵 콜라이더는 z [-1.25, 1.25]로 이 평면을 감싸고 있다.
            private const float PlaneZ = 0f;
            //  새 주위 이 거리 안의 콜라이더만 그린다 — 코스 전체를 그리면 선이 수천 개가 된다.
            private const float DrawRange = 30f;
            private const int CircleSegments = 24;

            private static readonly Color BodyColor = new Color(0.2f, 1f, 0.35f, 1f);
            private static readonly Color MapColor = new Color(1f, 0.35f, 0.2f, 1f);

            private GameFramework.World.EntityRegistry registry;
            private FlappyConfig config;
            private Material material;
            private readonly Collider[] hits = new Collider[256];
            private LOPActor[] actors = System.Array.Empty<LOPActor>();
            private float nextActorScan;

            public void Init(GameFramework.World.EntityRegistry registry, FlappyConfig config)
            {
                this.registry = registry;
                this.config = config;
            }

            private void OnDestroy()
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            private void OnRenderObject()
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorPrefs.GetBool(EditorPrefsKey, false) == false)
                {
                    return;
                }
#else
                return;
#endif
                if (registry == null)
                {
                    return;
                }
                //  새가 스폰·탈락으로 드나들므로 주기적으로 다시 모은다. 매 프레임 FindObjects는
                //  비싸고, 이건 진단용이라 0.5초면 충분하다.
                if (Time.unscaledTime >= nextActorScan)
                {
                    actors = Object.FindObjectsByType<LOPActor>(FindObjectsInactive.Exclude,
                                                                FindObjectsSortMode.None);
                    nextActorScan = Time.unscaledTime + 0.5f;
                }
                if (material == null)
                {
                    //  유니티가 내장으로 들고 있는 색선 셰이더. 즉시모드 선의 표준 처방이다.
                    var shader = Shader.Find("Hidden/Internal-Colored");
                    if (shader == null)
                    {
                        return;
                    }
                    material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    material.SetInt("_ZWrite", 0);
                    //  깊이 검사를 끈다 — 파이프 앞에 있든 뒤에 있든 판정선은 늘 보여야 한다.
                    material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }

                material.SetPass(0);
                GL.PushMatrix();
                GL.Begin(GL.LINES);

                Vector3 anchor = Vector3.zero;
                bool haveAnchor = false;
                foreach (LOPActor actor in actors)
                {
                    if (actor == null || actor.visualGameObject == null)
                    {
                        continue;
                    }
                    GameFramework.World.Entity e = registry.Get(actor.entityId);
                    if (e == null || e.Get<FlappyDash>() == null)
                    {
                        continue;   // 새만 그린다(대시를 들고 있는 것이 새다)
                    }
                    //  그려지는 자세를 그대로 쓴다 — 보간기(PredictedEntityInterpolator 등)가
                    //  매 프레임 여기에 소수 틱 자세를 넣는다.
                    Vector3 feet = actor.visualGameObject.transform.position;
                    feet.z = PlaneZ;
                    //  판정 모양: 반지름 config.BodyRadius, 중심이 발밑에서 그만큼 위.
                    //  KinematicMover.Cast가 p1=p2=발밑+반지름으로 두므로 구 하나다.
                    DrawCircle(feet + Vector3.up * config.BodyRadius, config.BodyRadius, BodyColor);
                    //  발밑 십자 — 엔티티 위치가 어디인지 보여 준다(겉모습 중심과 다르다).
                    GL.Color(BodyColor);
                    Line(feet + Vector3.left * 0.25f, feet + Vector3.right * 0.25f);
                    Line(feet + Vector3.down * 0.25f, feet + Vector3.up * 0.25f);
                    if (haveAnchor == false)
                    {
                        anchor = feet;
                        haveAnchor = true;
                    }
                }

                if (haveAnchor)
                {
                    DrawMapColliders(anchor);
                }

                GL.End();
                GL.PopMatrix();
            }

            //  맵 콜라이더의 <b>판정 평면 단면</b>을 그린다. 겉모습은 z로 당겨져 있어 화면에서
            //  더 크게 보이는데, 실제로 막는 것은 이 단면이다.
            private void DrawMapColliders(Vector3 around)
            {
                int count = Physics.OverlapBoxNonAlloc(
                    new Vector3(around.x, around.y, PlaneZ),
                    new Vector3(DrawRange, DrawRange, 0.1f), hits, Quaternion.identity,
                    LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
                GL.Color(MapColor);
                for (int i = 0; i < count; i++)
                {
                    Bounds b = hits[i].bounds;
                    var a = new Vector3(b.min.x, b.min.y, PlaneZ);
                    var c = new Vector3(b.max.x, b.max.y, PlaneZ);
                    Line(a, new Vector3(c.x, a.y, PlaneZ));
                    Line(new Vector3(c.x, a.y, PlaneZ), c);
                    Line(c, new Vector3(a.x, c.y, PlaneZ));
                    Line(new Vector3(a.x, c.y, PlaneZ), a);
                }
            }

            private void DrawCircle(Vector3 center, float radius, Color color)
            {
                GL.Color(color);
                Vector3 prev = center + new Vector3(radius, 0f, 0f);
                for (int i = 1; i <= CircleSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / CircleSegments;
                    var next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                    Line(prev, next);
                    prev = next;
                }
            }

            private static void Line(Vector3 from, Vector3 to)
            {
                GL.Vertex(from);
                GL.Vertex(to);
            }
        }
    }
}
