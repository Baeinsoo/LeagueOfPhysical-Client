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
    /// <remarks>틱·보간 뒤(IPostLateTickable)에서 칠한다 — 손·얼굴 자리가 이번 프레임 몸 자리를 따른다(최종 리뷰 I1).</remarks>
    public class CardRushRunnerFxView : IStartable, IPostLateTickable, IDisposable
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
            public string Id;
            public CardRushCardPlate Thrown;
        }

        //  던진 카드(spec 2026-11-03 §5.3): 손 = 몸 오른쪽 0.3 m·높이 1.0 m, 튕겨 돌아올 때 포물선 높이 0.3 m.
        public const float HandRight = 0.3f;
        public const float HandHeight = 1.0f;
        public const float BounceArc = 0.3f;
        public const float SlapDrop = 0.6f;       //  얼굴에서 떨어지는 거리
        public const float SlapCardOfHead = 1.0f;
        /// <summary>손 → 문 비행의 위로 휨(m) — 곧게 날면 뒤 위 카메라에서 큰 머리에 가려 안 보였다(실제 판).</summary>
        public const float ThrowArc = 0.7f;
        public const float SlapHold01 = 0.75f;    //  철썩 뒤 이 몫 동안 얼굴에 붙어 있다

        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actors;
        private readonly IPlayerContext playerContext;
        private readonly EntityRenderClock renderClock;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> gone = new List<string>();
        //  몸마다 마지막으로 본 돌기 시작 틱 — 던지면 상태에서 지워지므로 틀린 카드의 던진 색을 다시 세려면 기억해야 한다.
        private readonly Dictionary<string, CardRushSpinMemory> spinStarts = new Dictionary<string, CardRushSpinMemory>();
        private CardRushGagFxMaterials materials;
        private CardRushCoursePalette palette;
        private CardRushCardMaterials cardMaterials;
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
            palette = new CardRushCoursePalette();
            cardMaterials = new CardRushCardMaterials(palette);
            root = new GameObject("CardRushGagFx");
        }

        public void PostLateTick()
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

                var state = runner.State;
                spinStarts.TryGetValue(actor.entityId, out var memory);
                spinStarts[actor.entityId] = CardRushSpinMemory.Observe(memory, state, world.Timing);

                double drawnTick = DrawnTick(renderClock, actor.entityId, me);
                var (kind, t01) = CardRushGag.Phase(rules.Theme, state, drawnTick, world.Timing, rules.StepCount);
                //  던지는 카드는 문 테마만(돌다리는 로비에서 빠졌고 풍덩이 그대로다).
                var (throwKind, since) = rules.Theme == CardRushTheme.DoorDash
                    ? CardRushThrowFx.Phase(state, drawnTick, world.Timing)
                    : (CardRushThrowKind.None, 0d);

                if (entries.TryGetValue(actor.entityId, out var entry) == false || entry.Visual != visual.transform)
                {
                    if (kind == CardRushGagKind.None && throwKind == CardRushThrowKind.None)
                    {
                        continue;   // 쉬는 몸은 만들 것도 없다
                    }
                    entry = Dress(actor.entityId, visual.transform, entry);
                    if (entry == null)
                    {
                        continue;
                    }
                }

                if (kind != CardRushGagKind.None || entry.Posed)
                {
                    var pose = CardRushGag.PoseAt(kind, t01, rules.StepSpacing, world.Timing.StunTicks);
                    ApplyPose(entry.Visual, entry.Model, entry.Rest, pose, rules.Forward);
                    entry.Posed = kind != CardRushGagKind.None;
                    entry.Fx.Show(kind, t01, entry.Visual.position, rules.Forward, rules.StepSpacing, ChibiHeadSpots.Crown(entry.Model, entry.Head),
                                  CardRushGag.SlapFall01(world.Timing.StunTicks));
                }
                //  카드는 자세를 준 뒤에 놓는다 — 철썩 카드는 이번 프레임 머리(젖혀진 자리)에 붙는다.
                DrawThrown(entry, state, runner.LaneOrigin, throwKind, since, actor.entityId == me, rules);
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
                entries[id].Thrown?.Destroy();
                entries.Remove(id);
                spinStarts.Remove(id);
            }
        }

        /// <summary>
        /// 날아가는 카드 한 장(spec 2026-11-03 §5.3). 맞음: 손 → 그 칸 문짝 가운데로 직선 + 두 바퀴(꽂힌 뒤는 <see cref="CardRushCourseView"/>가
        /// 문짝에 그린다). 틀림: 문까지 같고, 문 → 얼굴 포물선, 철썩 뒤 얼굴에서 떨어지며 작아져 사라진다. 색: 맞음 = 그 칸 요구 색,
        /// 틀림 = 던진 색(원장 판정, <see cref="CardRushThrowFx.TryThrownColor"/>) — 돌기 시작 틱을 못 봤으면 뒷면.
        /// </summary>
        private void DrawThrown(Entry entry, in CardRushRunnerState s, Vector3 laneOrigin, CardRushThrowKind kind, double since,
                                bool mine, CardRushMapRulesField rules)
        {
            if (kind == CardRushThrowKind.None)
            {
                entry.Thrown?.SetVisible(false);
                return;
            }
            var course = world.Course;
            if (course == null)
            {
                return;
            }
            entry.Thrown ??= new CardRushCardPlate(root.transform, "Thrown " + entry.Id, cardMaterials);

            int step = CardRushThrowFx.IsRightThrow(s, world.Timing) ? s.Step - 1 : s.Step;
            //  꽂힌 판이 켜진 틱이면(코스 그림 틱) 날아가는 판은 끈다 — 한 프레임 두 장 방지.
            if (CardRushThrowFx.FlyingHiddenByStuck(s, (long)System.Math.Floor(DrawnTick(renderClock, entry.Id, playerContext.entityId)), world.Timing))
            {
                entry.Thrown.SetVisible(false);
                return;
            }
            spinStarts.TryGetValue(entry.Id, out var spinMemory);
            bool faceUp = CardRushThrowFx.TryThrownFace(s, spinMemory, course, world.Timing, out var color);

            var f = CardRushLanes.Flat(rules.Forward);
            var rightAxis = Vector3.Cross(Vector3.up, f);
            var body = entry.Visual.position;
            var hand = body + rightAxis * HandRight + Vector3.up * HandHeight;
            var door = CardRushCourseLayout.ObstacleAt(rules.Theme, laneOrigin, f, rules.StepSpacing, Mathf.Clamp(step, 0, course.StepCount - 1));
            //  문짝 가운데의 꽂힐 자리(닫힌 문 기준 — 문 로컬 x 0, 문짝 앞면 조금 앞).
            var hit = door + Vector3.up * CardRushLaneView.StuckCardLocal.y + f * CardRushLaneView.StuckCardLocal.z;
            //  얼굴은 자세를 따른다 — 벌러덩하면 얼굴이 하늘을 보고, 붙은 카드가 뒤 위 카메라에 보인다.
            var modelForward = entry.Model.forward;
            var modelUp = entry.Model.up;
            var face = ChibiHeadSpots.FaceFront(entry.Model, entry.Head);
            float startHeight = mine ? CardRushHeadCardView.MineHeight : CardRushHeadCardView.OtherHeight;
            var facing = Quaternion.LookRotation(f, Vector3.up);   //  보는 쪽(-z)이 뒤의 카메라를 본다

            var (at, height, roll) = ThrownPlacement(since, hand, hit, face, startHeight);
            if (height <= 0.01f)
            {
                entry.Thrown.SetVisible(false);
                return;
            }
            entry.Thrown.SetVisible(true);
            entry.Thrown.SetFace(faceUp, color, false);
            var basis = since >= CardRushThrowFx.SlapTick ? Quaternion.LookRotation(modelForward, modelUp) : facing;
            entry.Thrown.Root.SetPositionAndRotation(at, basis * Quaternion.AngleAxis(roll, Vector3.forward));
            entry.Thrown.Root.localScale = Vector3.one * height;
        }

        /// <summary>
        /// 던진 카드의 자리·높이·기울기(도) — 던짐에서 since틱. 0~3 손 → 문(두 바퀴 돌아 꽂힌 기울기로 닿음), 3~7 문 → 얼굴 포물선,
        /// 7~12 얼굴에 붙었다가 떨어지며 작아져 0. 그림만 정하는 순수 식.
        /// </summary>
        public static (Vector3 at, float height, float roll) ThrownPlacement(double since, Vector3 hand, Vector3 hit, Vector3 face, float startHeight)
        {
            float stuckHeight = CardRushLaneView.StuckCardHeight;
            float roll0 = CardRushLaneView.StuckCardRoll;
            if (since < CardRushThrowFx.FlightTicks)
            {
                float u = Mathf.Clamp01((float)(since / CardRushThrowFx.FlightTicks));
                return (Vector3.Lerp(hand, hit, u) + Vector3.up * (4f * ThrowArc * u * (1f - u)),
                        Mathf.Lerp(startHeight, stuckHeight, u), roll0 + 720f * (1f - u));
            }
            if (since < CardRushThrowFx.SlapTick)
            {
                float u = (float)((since - CardRushThrowFx.FlightTicks) / CardRushThrowFx.BounceTicks);
                return (Vector3.Lerp(hit, face, u) + Vector3.up * (4f * BounceArc * u * (1f - u)),
                        Mathf.Lerp(stuckHeight, startHeight * SlapCardOfHead, u), roll0 - 360f * u);
            }
            float w = Mathf.Clamp01((float)((since - CardRushThrowFx.SlapTick) / CardRushThrowFx.DropTicks));
            //  처음 SlapHold01은 얼굴에 붙어 있다가 떨어지며 작아진다.
            float fall = Mathf.Clamp01((w - SlapHold01) / (1f - SlapHold01));
            return (face + Vector3.down * (SlapDrop * fall * fall), startHeight * SlapCardOfHead * (1f - fall), roll0 - 360f - 40f * fall);
        }

        private Entry Dress(string id, Transform visual, Entry old)
        {
            old?.Fx.Destroy();
            old?.Thrown?.Destroy();
            entries.Remove(id);
            var model = visual.Find(ChibiRig.ModelChildName);
            if (model == null)
            {
                return null;
            }
            var animator = ChibiRig.AnimatorOf(visual.gameObject);
            var entry = new Entry
            {
                Id = id,
                Visual = visual,
                Model = model,
                Head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null,
                Rest = new ModelRest(model),
                Fx = new CardRushGagFx(root.transform, "Gag " + id, materials),
            };
            entries[id] = entry;
            return entry;
        }

        /// <summary>
        /// 몸이 그려지는 틱(연속값) — 내 몸 그림은 예측 시각의 한 틱 뒤에 서 있고(KnockbackFlightFxView와 같은 식), 남은 보간 재생 시각.
        /// 몸개그·던지는 카드·문(열림·꽂힌 카드)이 모두 이 하나를 쓴다 — 기준이 다르면 날아가는 카드와 꽂힌 카드가 겹치거나 비었다.
        /// </summary>
        public static double DrawnTick(EntityRenderClock clock, string id, string me)
        {
            double interval = clock.SecondsPerTick;
            if (interval <= 0d)
            {
                return clock.TickFor(id);
            }
            return clock.TimeFor(id) / interval - (id == me ? 1d : 0d);
        }


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
                entry.Thrown?.Destroy();
            }
            entries.Clear();
            spinStarts.Clear();
            CardRushGagFx.DestroyObject(root);
            root = null;
            materials?.Dispose();
            materials = null;
            cardMaterials?.Dispose();
            cardMaterials = null;
            palette?.Dispose();
            palette = null;
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
        //  별: 철썩 맞고 나자빠지는 동안(0.30~0.75) 머리 위를 두 바퀴 돈다.
        private const float StarStart = 0.30f, StarEnd = 0.75f, StarTurns = 2f, StarRadius = 0.42f, StarBelowCrown = 0.40f, StarSize = 0.7f;

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

        /// <summary>그 순간의 효과를 놓는다. 풍덩·철썩 밖이면 다 숨긴다.</summary>
        public void Show(CardRushGagKind kind, float t01, Vector3 bodyRoot, Vector3 forward, float stepSpacing, Vector3 crown,
                         float starStart = StarStart)
        {
            bool splash = kind == CardRushGagKind.Splash && t01 >= SplashStart && t01 < RingEnd;
            bool slap = kind == CardRushGagKind.Slap && t01 >= starStart && t01 < StarEnd;
            if (splash == false && slap == false)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);

            for (int i = 0; i < DropletCount; i++) droplets[i].gameObject.SetActive(splash);
            ring.gameObject.SetActive(splash);
            for (int i = 0; i < StarCount; i++) stars[i].gameObject.SetActive(slap);

            if (splash)
            {
                var f = CardRushLanes.Flat(forward);
                var center = bodyRoot + f * (0.5f * stepSpacing);
                center.y = bodyRoot.y + WaterSurfaceY;
                PlaceSplash(t01, center, f);
            }
            if (slap)
            {
                PlaceStars(t01, crown, starStart);
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

        //  별은 머리 둘레를 돈다 — 옛 치비의 "머리 뼈 + 0.38"을 정수리 기준(정수리 − 0.40)으로 옮겼다.
        private void PlaceStars(float t01, Vector3 crown, float starStart)
        {
            float w = (t01 - starStart) / (StarEnd - starStart);
            //  나타날 때·사라질 때 크기로만 들고 난다(깜빡임 없이).
            float grow = Mathf.Clamp01(w / 0.12f) * Mathf.Clamp01((1f - w) / 0.12f);
            var cam = Camera.main;
            var face = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
            for (int i = 0; i < StarCount; i++)
            {
                float a = w * StarTurns * 2f * Mathf.PI + i * (2f * Mathf.PI / StarCount);
                stars[i].position = crown - Vector3.up * StarBelowCrown + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * StarRadius;
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
