using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 몸개그 그리기(spec §7.4): 몸마다 <see cref="CardRushGag.Phase"/> → <see cref="CardRushGag.PoseAt"/>을 자식 <c>Model</c>에만 준다.
    /// 뿌리 Transform은 시뮬·보간 몫이라 건드리지 않는다(<see cref="KnockbackFlightFxView"/>와 같은 규칙). 그림 틱도 그 뷰와 같다 —
    /// 내 몸은 예측 시각에서 한 틱 뒤(그려지는 자리), 남의 몸은 보간 재생 시각.
    /// <para>물보라·별도 (자세, t01)의 함수라 멈춤이 끝나는 틱에 같이 사라진다. 시뮬 상태는 읽기만 한다.</para>
    /// </summary>
    public class CardRushRunnerFxView : IStartable, ITickable, IDisposable
    {
        public readonly struct ModelRest
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public ModelRest(Transform model) { Position = model.localPosition; Rotation = model.localRotation; Scale = model.localScale; }
        }

        private sealed class Entry
        {
            public Transform Visual;
            public Transform Model;
            public Transform Head;
            public ModelRest Rest;
            public bool Posed;
            public CardRushGagFx Fx;
        }

        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actors;
        private readonly IPlayerContext playerContext;
        private readonly EntityRenderClock renderClock;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> gone = new List<string>();
        private CardRushGagFxMaterials materials;
        private GameObject root;

        public CardRushRunnerFxView(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry, ActorRegistry actors,
                                    IPlayerContext playerContext, EntityRenderClock renderClock)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.actors = actors;
            this.playerContext = playerContext;
            this.renderClock = renderClock;
        }

        public void Start()
        {
            materials = new CardRushGagFxMaterials();
            root = new GameObject("CardRushGagFx");
        }

        public void Tick()
        {
            var rules = world.Rules;
            double interval = renderClock.SecondsPerTick;
            if (root == null || rules == null || rules.HasRules == false || interval <= 0d)
            {
                return;
            }

            string me = playerContext.entityId;
            foreach (var actor in actors.Values)
            {
                var visual = actor.visualGameObject;
                var runner = entityRegistry.Get(actor.entityId)?.Get<CardRushRunner>();
                if (visual == null || runner == null)
                {
                    continue;
                }

                //  내 몸 그림은 예측 시각의 한 틱 뒤에 서 있다(KnockbackFlightFxView와 같은 식).
                double drawnTick = renderClock.TimeFor(actor.entityId) / interval - (actor.entityId == me ? 1d : 0d);
                var (kind, t01) = CardRushGag.Phase(rules.Theme, runner.State, drawnTick, world.Timing, rules.StepCount);

                if (entries.TryGetValue(actor.entityId, out var entry) == false || entry.Visual != visual.transform)
                {
                    if (kind == CardRushGagKind.None)
                    {
                        continue;   // 쉬는 몸은 만들 것도 없다
                    }
                    entry = Dress(actor.entityId, visual.transform, entry);
                    if (entry == null)
                    {
                        continue;
                    }
                }

                if (kind == CardRushGagKind.None && entry.Posed == false)
                {
                    continue;   // 이미 원래 자세 — 다른 연출과 다투지 않게 쓰지도 않는다
                }

                var pose = CardRushGag.PoseAt(kind, t01, rules.StepSpacing);
                ApplyPose(entry.Visual, entry.Model, entry.Rest, pose, rules.Forward);
                entry.Posed = kind != CardRushGagKind.None;
                entry.Fx.Show(kind, t01, entry.Visual.position, rules.Forward, rules.StepSpacing, HeadPosition(entry));
            }

            gone.Clear();
            foreach (var pair in entries)
            {
                if (actors.Contains(pair.Key) == false || pair.Value.Visual == null)
                {
                    gone.Add(pair.Key);
                }
            }
            foreach (var id in gone)
            {
                entries[id].Fx.Destroy();
                entries.Remove(id);
            }
        }

        private Entry Dress(string id, Transform visual, Entry old)
        {
            old?.Fx.Destroy();
            entries.Remove(id);
            var model = visual.Find(ChibiRig.ModelChildName);
            if (model == null)
            {
                return null;
            }
            var animator = ChibiRig.AnimatorOf(visual.gameObject);
            var entry = new Entry
            {
                Visual = visual,
                Model = model,
                Head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null,
                Rest = new ModelRest(model),
                Fx = new CardRushGagFx(root.transform, "Gag " + id, materials),
            };
            entries[id] = entry;
            return entry;
        }

        /// <summary>머리 위 별 자리 — 사람형이면 머리 뼈, 아니면 몸 위쪽 대략.</summary>
        public static Vector3 HeadPosition(Transform model, Transform head)
            => head != null ? head.position : model.position + model.up * 1.1f;

        private static Vector3 HeadPosition(Entry e) => HeadPosition(e.Model, e.Head);

        /// <summary>
        /// 자세를 모델에 준다. 오프셋·기울기는 진행 방향 기준(월드)이라 몸 뿌리의 회전·크기를 벗겨 모델 로컬로 바꾼다.
        /// 찌그러짐은 부피를 지키게 y × s, 옆 × 1/√s. <see cref="CardRushPose.Rest"/>면 원래 값이 그대로 들어간다.
        /// </summary>
        public static void ApplyPose(Transform visual, Transform model, in ModelRest rest, in CardRushPose pose, Vector3 forward)
        {
            var f = CardRushLanes.Flat(forward);
            var right = Vector3.Cross(Vector3.up, f);
            var worldOffset = right * pose.Offset.x + Vector3.up * pose.Offset.y + f * pose.Offset.z;
            model.localPosition = rest.Position + visual.InverseTransformVector(worldOffset);

            var tilt = Quaternion.AngleAxis(pose.EulerDeg.y, Vector3.up)
                     * Quaternion.AngleAxis(pose.EulerDeg.x, right)
                     * Quaternion.AngleAxis(pose.EulerDeg.z, f);
            model.localRotation = Quaternion.Inverse(visual.rotation) * tilt * visual.rotation * rest.Rotation;

            float s = Mathf.Max(0.05f, pose.Squash);
            float side = 1f / Mathf.Sqrt(s);
            model.localScale = Vector3.Scale(rest.Scale, new Vector3(side, s, side));
        }

        public void Dispose()
        {
            foreach (var entry in entries.Values)
            {
                if (entry.Model != null && entry.Posed)
                {
                    ApplyPose(entry.Visual, entry.Model, entry.Rest, CardRushPose.Rest, Vector3.forward);
                }
                entry.Fx.Destroy();
            }
            entries.Clear();
            CardRushGagFx.DestroyObject(root);
            root = null;
            materials?.Dispose();
            materials = null;
        }
    }

    /// <summary>물보라(물빛 한 계열)·별(노랑 한 계열) 재질. 효과 하나에 한 색 계열만 — 번쩍임 없음.</summary>
    public sealed class CardRushGagFxMaterials : IDisposable
    {
        public static readonly Color Droplet = new Color(0.90f, 0.97f, 1f);
        public static readonly Color Ring = new Color(0.62f, 0.85f, 0.97f);
        public static readonly Color Star = new Color(1f, 0.84f, 0.29f);

        public Material Water { get; }
        public Material StarMaterial { get; }
        public Mesh StarMesh { get; }

        public CardRushGagFxMaterials()
        {
            Water = MakeTransparent();
            StarMaterial = MakeOpaque(Star);
            StarMesh = MakeStar(0.5f, 0.22f);
        }

        private static Material MakeTransparent()
        {
            var urp = Shader.Find("Universal Render Pipeline/Unlit");
            if (urp == null)
            {
                return new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            }
            var m = new Material(urp) { hideFlags = HideFlags.DontSave };
            // 반투명 — URP Unlit의 투명 설정을 코드로 켠다(KnockbackFlightFxView 먼지와 같다).
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private static Material MakeOpaque(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }

        //  다섯 뿔 별(평면, 앞뒤 면). 반지름 outer/inner.
        private static Mesh MakeStar(float outer, float inner)
        {
            var verts = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                verts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            var tris = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % 10;
                tris.Add(0); tris.Add(b); tris.Add(a);
                tris.Add(0); tris.Add(a); tris.Add(b);
            }
            var mesh = new Mesh { name = "CardRushStar", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Dispose()
        {
            CardRushGagFx.DestroyObject(Water);
            CardRushGagFx.DestroyObject(StarMaterial);
            CardRushGagFx.DestroyObject(StarMesh);
        }
    }

    /// <summary>
    /// 몸 하나의 물보라·별. 저장하는 시간이 없다 — 매번 (몸개그 종류, t01)에서 자리를 다시 계산해, 멈춤 끝 틱에 같이 꺼진다.
    /// </summary>
    public sealed class CardRushGagFx
    {
        /// <summary>강물 수면(맵 spec: 돌다리 강물 판 y −0.6). 몸 뿌리 높이(레인 바닥 0) 기준.</summary>
        public const float WaterSurfaceY = -0.6f;

        public const int DropletCount = 12;
        public const int StarCount = 3;

        //  물보라: 빠지는 순간(0.25)부터 0.35 동안 튀고, 물결은 허우적 끝(0.75)까지 퍼졌다 사라진다.
        private const float SplashStart = 0.25f, SplashDropletSpan = 0.35f, RingEnd = 0.80f;
        //  별: 나자빠지는 동안(0.20~0.75) 머리 위를 두 바퀴 돈다.
        private const float StarStart = 0.20f, StarEnd = 0.75f, StarTurns = 2f, StarRadius = 0.42f, StarLift = 0.38f, StarSize = 0.7f;

        private readonly GameObject root;
        private readonly Transform[] droplets = new Transform[DropletCount];
        private readonly Renderer[] dropletRenderers = new Renderer[DropletCount];
        private readonly Transform ring;
        private readonly Renderer ringRenderer;
        private readonly Transform[] stars = new Transform[StarCount];
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private bool visible = true;

        public CardRushGagFx(Transform parent, string name, CardRushGagFxMaterials materials)
        {
            root = new GameObject(name);
            root.transform.SetParent(parent, false);
            for (int i = 0; i < DropletCount; i++)
            {
                var go = Primitive(PrimitiveType.Sphere, "Droplet", materials.Water);
                droplets[i] = go.transform;
                dropletRenderers[i] = go.GetComponent<Renderer>();
            }
            var ringGo = Primitive(PrimitiveType.Cylinder, "Ripple", materials.Water);
            ring = ringGo.transform;
            ringRenderer = ringGo.GetComponent<Renderer>();
            for (int i = 0; i < StarCount; i++)
            {
                var go = new GameObject("Star");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = materials.StarMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = materials.StarMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                stars[i] = go.transform;
            }
            SetVisible(false);
        }

        private GameObject Primitive(PrimitiveType type, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroyObject(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>그 순간의 효과를 놓는다. 풍덩·박치기 밖이면 다 숨긴다.</summary>
        public void Show(CardRushGagKind kind, float t01, Vector3 bodyRoot, Vector3 forward, float stepSpacing, Vector3 head)
        {
            bool splash = kind == CardRushGagKind.Splash && t01 >= SplashStart && t01 < RingEnd;
            bool bonk = kind == CardRushGagKind.Bonk && t01 >= StarStart && t01 < StarEnd;
            if (splash == false && bonk == false)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);

            for (int i = 0; i < DropletCount; i++) droplets[i].gameObject.SetActive(splash);
            ring.gameObject.SetActive(splash);
            for (int i = 0; i < StarCount; i++) stars[i].gameObject.SetActive(bonk);

            if (splash)
            {
                var f = CardRushLanes.Flat(forward);
                var center = bodyRoot + f * (0.5f * stepSpacing);
                center.y = bodyRoot.y + WaterSurfaceY;
                PlaceSplash(t01, center, f);
            }
            if (bonk)
            {
                PlaceStars(t01, head);
            }
        }

        private void PlaceSplash(float t01, Vector3 center, Vector3 forward)
        {
            var right = Vector3.Cross(Vector3.up, forward);
            float u = (t01 - SplashStart) / SplashDropletSpan;
            for (int i = 0; i < DropletCount; i++)
            {
                if (u >= 1f)
                {
                    droplets[i].gameObject.SetActive(false);
                    continue;
                }
                //  방울마다 정해진 방향·세기(씨앗 없는 고정 표) — 매번 같은 모양으로 튄다.
                float angle = i * (2f * Mathf.PI / DropletCount) + 0.37f * (i % 3);
                float reach = 1.0f + 0.8f * ((i * 7) % 5) / 4f;
                float height = 1.2f + 0.8f * ((i * 3) % 4) / 3f;
                var dir = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                droplets[i].position = center + dir * (reach * u) + Vector3.up * (4f * height * u * (1f - u));
                float size = Mathf.Lerp(0.32f, 0.12f, u);
                droplets[i].localScale = new Vector3(size, size, size);
                Tint(dropletRenderers[i], CardRushGagFxMaterials.Droplet, 0.95f * (1f - u * u));
            }

            float w = Mathf.Clamp01((t01 - SplashStart) / (RingEnd - SplashStart));
            float radius = Mathf.Lerp(0.5f, 1.9f, Mathf.Sqrt(w));
            ring.position = center + Vector3.up * 0.02f;
            ring.rotation = Quaternion.identity;
            ring.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            Tint(ringRenderer, CardRushGagFxMaterials.Ring, 0.7f * (1f - w));
        }

        private void PlaceStars(float t01, Vector3 head)
        {
            float w = (t01 - StarStart) / (StarEnd - StarStart);
            //  나타날 때·사라질 때 크기로만 들고 난다(깜빡임 없이).
            float grow = Mathf.Clamp01(w / 0.12f) * Mathf.Clamp01((1f - w) / 0.12f);
            var cam = Camera.main;
            var face = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
            for (int i = 0; i < StarCount; i++)
            {
                float a = w * StarTurns * 2f * Mathf.PI + i * (2f * Mathf.PI / StarCount);
                stars[i].position = head + Vector3.up * StarLift + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * StarRadius;
                stars[i].rotation = face * Quaternion.AngleAxis(a * Mathf.Rad2Deg * 0.5f, Vector3.forward);
                float size = StarSize * grow;
                stars[i].localScale = new Vector3(size, size, size);
            }
        }

        private void Tint(Renderer renderer, Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private void SetVisible(bool on)
        {
            if (visible == on || root == null)
            {
                return;
            }
            visible = on;
            root.SetActive(on);
        }

        /// <summary>지금 켜진 물보라 방울·별 수 — 시험·진단용.</summary>
        public int ActiveDroplets { get { int n = 0; if (visible) foreach (var d in droplets) if (d.gameObject.activeSelf) n++; return n; } }
        public int ActiveStars { get { int n = 0; if (visible) foreach (var s in stars) if (s.gameObject.activeSelf) n++; return n; } }

        public void Destroy() => DestroyObject(root);

        //  플레이 중이 아니면(EditMode 시험·미리보기) Destroy가 안 먹는다. LOP엔 Application 싱글톤이 있어 UnityEngine을 붙인다.
        internal static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
