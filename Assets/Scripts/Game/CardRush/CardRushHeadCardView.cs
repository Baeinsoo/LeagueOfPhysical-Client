using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 머리 위 카드(spec 2026-11-03 §5.1·§5.5) — 몸마다 카드 판 하나를 머리 위에 띄워 카메라 쪽으로 돌린다(y축만).
    /// 그림은 <see cref="CardRushHeadCard.Compute"/>의 결과를 그리기만 한다: 내 카드는 <see cref="PlayerInputManager.NextInputTick"/>
    /// (지금 누르면 판정될 틱 — 본 색 = 판정 색), 남의 카드는 그림 틱(<see cref="CardRushRunnerFxView"/>와 같은 식).
    /// 남의 카드는 내 레인 ±<see cref="OtherLaneRange"/>만, 작게. 남은 시간 링은 내 카드에만. 시뮬 상태는 읽기만 한다.
    /// </summary>
    public class CardRushHeadCardView : IStartable, ITickable, IDisposable
    {
        public const float HeadCardLift = 0.55f;   //  머리 위로 이만큼 띄운 자리가 카드 아래 끝
        public const float MineHeight = 0.7f;
        public const float OtherHeight = 0.4f;
        public const int OtherLaneRange = 3;
        /// <summary>머리 뼈(목 쪽)에서 정수리까지 — 치비 머리가 커서 뼈 + 띄움만으로는 카드 아래가 머리에 가렸다(실제 판).</summary>
        public const float HeadTopAboveBone = 0.45f;
        /// <summary>카메라 쪽으로 당기는 거리 — 뒤 위에서 보면 카드가 머리 뒤에 서 있어 아래가 가린다. 화면 자리는 거의 그대로다.</summary>
        public const float TowardCamera = 0.6f;

        public const float FlipSeconds = 0.06f;    //  뒷면 → 앞면 넘김(가로 찌그러짐)
        public const float PopScale = 1.06f;       //  색이 바뀌는 프레임에 6% 커졌다 돌아온다
        public const float PopSeconds = 0.12f;
        public const int BrightFrames = 2;         //  색이 바뀌면 그 색 밝은 판으로 2프레임
        public const float RingWidthOfHeight = 0.04f;
        public const int RingSegments = 48;
        /// <summary>
        /// 링 반지름(카드 높이 1 기준) — 테두리 포함 모서리(√(0.43² + 0.58²) ≈ 0.72) 바로 바깥. 0.66이면 링이 모서리를 가로질렀다(리뷰 L4).
        /// </summary>
        public const float RingRadius = 0.76f;
        /// <summary>링 밑에 까는 어두운 외곽선 굵기 배수 — 노랑 문 앞에서도 금 링이 묻히지 않게(리뷰 M1).</summary>
        public const float RingOutlineScale = 1.9f;

        private sealed class Entry
        {
            public Transform Visual;
            public Transform Model;
            public Transform Head;
            public CardRushCardPlate Plate;
            public LineRenderer Ring;
            public LineRenderer RingOutline;
            public bool LastFaceUp;
            public CardColor LastColor;
            public float FlipStart = -1f;
            public float PopStart = -1f;
            public int BrightLeft;
        }

        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actors;
        private readonly IPlayerContext playerContext;
        private readonly EntityRenderClock renderClock;
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> gone = new List<string>();
        private readonly Vector3[] ringPoints = new Vector3[RingSegments + 1];
        private CardRushCoursePalette palette;
        private CardRushCardMaterials materials;
        private GameObject root;

        public CardRushHeadCardView(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry, ActorRegistry actors,
                                    IPlayerContext playerContext, EntityRenderClock renderClock, PlayerInputManager input,
                                    CameraController cameraController)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.actors = actors;
            this.playerContext = playerContext;
            this.renderClock = renderClock;
            this.input = input;
            this.cameraController = cameraController;
        }

        public void Start()
        {
            palette = new CardRushCoursePalette();
            materials = new CardRushCardMaterials(palette);
            root = new GameObject("CardRushHeadCards");
        }

        /// <summary>
        /// 내 카드의 지금 모습. 틱은 반드시 <see cref="PlayerInputManager.NextInputTick"/> — 그림 틱으로 칠하면 색 경계에서
        /// 본 색과 판정 색이 한 틱 갈린다(spec §5 틱 기준).
        /// </summary>
        public bool TryComputeMine(out CardRushHeadCardFace face)
        {
            face = default;
            string me = playerContext?.entityId;
            var runner = string.IsNullOrEmpty(me) ? null : entityRegistry.Get(me)?.Get<CardRushRunner>();
            var course = world.Course;
            if (runner == null || course == null || input == null)
            {
                return false;
            }
            face = CardRushHeadCard.Compute(runner.State, input.NextInputTick, world.GameplayStartTick, course, world.Timing);
            return true;
        }

        /// <summary>두 레인 출발점이 몇 레인 떨어졌나(진행 방향에 수직인 거리 ÷ 레인 간격).</summary>
        public static int LaneGap(Vector3 a, Vector3 b, Vector3 forward, float laneSpacing)
        {
            if (laneSpacing <= 0f)
            {
                return 0;
            }
            var right = Vector3.Cross(Vector3.up, CardRushLanes.Flat(forward));
            return Mathf.RoundToInt(Mathf.Abs(Vector3.Dot(a - b, right)) / laneSpacing);
        }

        public void Tick()
        {
            var rules = world.Rules;
            var course = world.Course;
            if (root == null || rules == null || rules.HasRules == false || course == null)
            {
                return;
            }
            double interval = renderClock != null ? renderClock.SecondsPerTick : 0d;
            string me = playerContext?.entityId;
            var myRunner = string.IsNullOrEmpty(me) ? null : entityRegistry.Get(me)?.Get<CardRushRunner>();
            var cam = cameraController != null && cameraController.MainCamera != null ? cameraController.MainCamera : Camera.main;
            float now = Time.unscaledTime;

            foreach (var actor in actors.Values)
            {
                var visual = actor.visualGameObject;
                var runner = entityRegistry.Get(actor.entityId)?.Get<CardRushRunner>();
                if (visual == null || runner == null)
                {
                    continue;
                }
                bool mine = actor.entityId == me;

                CardRushHeadCardFace face;
                if (mine)
                {
                    TryComputeMine(out face);
                }
                else if (myRunner != null && LaneGap(runner.LaneOrigin, myRunner.LaneOrigin, rules.Forward, rules.LaneSpacing) > OtherLaneRange)
                {
                    face = default;   //  먼 레인 — 카드 여덟 장이 앞 코스를 덮지 않게
                }
                else
                {
                    //  남의 몸 그림 틱 = 보간 재생 시각(FxView와 같은 식).
                    long drawnTick = interval > 0d ? (long)(renderClock.TimeFor(actor.entityId) / interval) : renderClock?.TickFor(actor.entityId) ?? 0;
                    face = CardRushHeadCard.Compute(runner.State, drawnTick, world.GameplayStartTick, course, world.Timing);
                }

                if (entries.TryGetValue(actor.entityId, out var entry) == false || entry.Visual != visual.transform)
                {
                    if (face.Visible == false)
                    {
                        continue;
                    }
                    entry = Dress(actor.entityId, visual.transform, entry);
                }

                Draw(entry, face, mine, cam, now);
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
                entries[id].Plate.Destroy();
                entries.Remove(id);
            }
        }

        private Entry Dress(string id, Transform visual, Entry old)
        {
            old?.Plate.Destroy();
            entries.Remove(id);
            var model = visual.Find(ChibiRig.ModelChildName);
            var animator = ChibiRig.AnimatorOf(visual.gameObject);
            var entry = new Entry
            {
                Visual = visual,
                Model = model != null ? model : visual,
                Head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null,
                Plate = new CardRushCardPlate(root.transform, "HeadCard " + id, materials),
            };
            entry.RingOutline = MakeRing(entry.Plate.Root, materials.RingOutline, RingOutlineScale, "RingOutline");
            entry.Ring = MakeRing(entry.Plate.Root, materials.Ring, 1f, "Ring");
            entries[id] = entry;
            return entry;
        }

        private LineRenderer MakeRing(Transform parent, Material material, float widthScale, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = false;
            ring.sharedMaterial = material;
            ring.widthMultiplier = MineHeight * RingWidthOfHeight * widthScale;
            //  외곽선은 카메라에서 조금 먼 쪽(+z)에 깔아 금 링이 위에 그려진다.
            if (widthScale > 1f) go.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            ring.numCapVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.enabled = false;
            return ring;
        }

        private void Draw(Entry entry, in CardRushHeadCardFace face, bool mine, Camera cam, float now)
        {
            if (face.Visible == false)
            {
                entry.Plate.SetVisible(false);
                entry.Ring.enabled = false;
                entry.RingOutline.enabled = false;
                entry.LastFaceUp = false;
                return;
            }

            float height = mine ? MineHeight : OtherHeight;
            var head = CardRushRunnerFxView.HeadPosition(entry.Model, entry.Head);
            var center = head + Vector3.up * (HeadTopAboveBone + HeadCardLift + height * 0.5f);
            if (cam != null)
            {
                center += (cam.transform.position - center).normalized * TowardCamera;
            }
            var look = cam != null ? center - cam.transform.position : entry.Visual.forward;
            look.y = 0f;
            var rotation = look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity;
            entry.Plate.Root.SetPositionAndRotation(center, rotation);
            entry.Plate.Root.localScale = Vector3.one * height;

            //  넘김은 뒷면 → 앞면에서만. 색은 먼저 바꾸고 넘김을 덧붙인다(원 "구현 중 바뀐 것").
            if (face.FaceUp && entry.LastFaceUp == false)
            {
                entry.FlipStart = now;
            }
            else if (face.FaceUp && face.Color != entry.LastColor)
            {
                entry.BrightLeft = BrightFrames;
                entry.PopStart = now;
            }
            entry.Plate.SetVisible(true);
            entry.Plate.SetFace(face.FaceUp, face.Color, entry.BrightLeft > 0);
            if (entry.BrightLeft > 0)
            {
                entry.BrightLeft--;
            }
            entry.LastFaceUp = face.FaceUp;
            entry.LastColor = face.Color;

            entry.Plate.Body.localScale = new Vector3(FlipWidth(now - entry.FlipStart), 1f, 1f) * PopSize(now - entry.PopStart);

            bool ring = mine && face.FaceUp;
            entry.Ring.enabled = ring;
            entry.RingOutline.enabled = ring;
            if (ring)
            {
                int n = RingArc(face.Remaining01, ringPoints);
                entry.Ring.positionCount = n;
                entry.Ring.SetPositions(ringPoints);
                entry.RingOutline.positionCount = n;
                entry.RingOutline.SetPositions(ringPoints);
            }
        }

        /// <summary>넘김 중 가로 비율 — 0.03초에 0.15로 접혔다가 0.03초에 편다. 넘김 밖이면 1.</summary>
        public static float FlipWidth(float since)
        {
            if (since < 0f || since >= FlipSeconds)
            {
                return 1f;
            }
            float u = since / FlipSeconds;
            return u < 0.5f ? Mathf.Lerp(1f, 0.15f, u * 2f) : Mathf.Lerp(0.15f, 1f, (u - 0.5f) * 2f);
        }

        public static float PopSize(float since)
            => since < 0f || since >= PopSeconds ? 1f : Mathf.Lerp(PopScale, 1f, since / PopSeconds);

        /// <summary>
        /// 남은 몫만큼의 링 점(카드 높이 1 기준, 카드 평면). 12시에서 시작해 먹힌 몫이 시계 방향으로 자란다 —
        /// 남은 호는 (12시 − 먹힌 각)에서 시계 방향으로 12시까지. 점 수를 돌려준다(최소 2).
        /// </summary>
        public static int RingArc(float remaining01, Vector3[] into)
        {
            float r = Mathf.Clamp01(remaining01);
            int n = Mathf.Clamp(Mathf.CeilToInt(RingSegments * r) + 1, 2, into.Length);
            float start = 90f - (1f - r) * 360f;
            float span = r * 360f;
            for (int i = 0; i < n; i++)
            {
                //  보는 쪽(-z)에서 x 오른쪽·y 위 — 시계 방향 = 각이 줄어드는 쪽.
                float a = (start - span * i / (n - 1)) * Mathf.Deg2Rad;
                into[i] = new Vector3(Mathf.Cos(a) * RingRadius, Mathf.Sin(a) * RingRadius, -0.02f);
            }
            return n;
        }

        public void Dispose()
        {
            foreach (var entry in entries.Values)
            {
                entry.Plate.Destroy();
            }
            entries.Clear();
            CardRushGagFx.DestroyObject(root);
            root = null;
            materials?.Dispose();
            materials = null;
            palette?.Dispose();
            palette = null;
        }
    }

    /// <summary>
    /// 카드 판이 함께 쓰는 재질 — 앞면 색은 <see cref="CardRushCoursePalette"/>의 것을 그대로(문·돌과 같은 색), 테두리는 모든 색 공통
    /// 진한 금 하나, 뒷면은 중립 회색. 밝은 판은 색이 바뀌는 2프레임용(같은 색 계열). 팔레트는 빌려 쓴다(버리지 않는다).
    /// </summary>
    public sealed class CardRushCardMaterials : IDisposable
    {
        /// <summary>테두리 금 — 노랑 카드(#F5C542)보다 진해 노랑 판 위에서도 테두리가 보인다.</summary>
        public static readonly Color Gold = new Color(0.72f, 0.52f, 0.10f);
        public static readonly Color Back = new Color(0.42f, 0.40f, 0.38f);
        public static readonly Color BackMark = new Color(0.58f, 0.56f, 0.53f);
        public const float BrightLift = 0.45f;

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Material[] bright = new Material[CardColors.Count];

        public CardRushCoursePalette Palette { get; }
        public Material Rim { get; }
        public Material BackPlate { get; }
        public Material BackMarkMaterial { get; }
        public Material Ring { get; }
        public Material RingOutline { get; }
        /// <summary>링 외곽선 — 진한 밤색(금과 같은 계열의 어두운 쪽).</summary>
        public static readonly Color RingDark = new Color(0.24f, 0.16f, 0.05f);
        public Mesh Diamond { get; }

        public CardRushCardMaterials(CardRushCoursePalette palette)
        {
            Palette = palette;
            Rim = Make("CardRush_CardRim", Gold);
            BackPlate = Make("CardRush_CardBack", Back);
            BackMarkMaterial = Make("CardRush_CardBackMark", BackMark);
            for (int i = 0; i < CardColors.Count; i++)
            {
                var c = CardRushCoursePalette.ColorOf((CardColor)i);
                bright[i] = Make("CardRush_CardBright_" + (CardColor)i, Color.Lerp(c, Color.white, BrightLift));
            }
            //  링은 테두리와 같은 진한 금, 불투명(α 1) — 반투명 밝은 금은 노랑 문 앞에서 묻혔다(리뷰 M1).
            Ring = MakeRingMaterial(new Color(Gold.r, Gold.g, Gold.b, 1f));
            RingOutline = MakeRingMaterial(new Color(RingDark.r, RingDark.g, RingDark.b, 0.85f));
            Diamond = MakeDiamond();
        }

        public Material Face(CardColor color, bool brightNow) => brightNow ? bright[(int)color] : Palette.Of(color, false);

        private Material Make(string name, Color color)
        {
            var shader = Shader.Find("LOP/Toon");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0f);
            owned.Add(m);
            return m;
        }

        private Material MakeRingMaterial(Color color)
        {
            var urp = Shader.Find("Universal Render Pipeline/Unlit");
            Material m;
            if (urp == null)
            {
                m = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            }
            else
            {
                m = new Material(urp) { hideFlags = HideFlags.DontSave };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            m.name = "CardRush_CardRing";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            owned.Add(m);
            return m;
        }

        //  뒷면 무늬 — 마름모 하나(앞뒤 면). 파랑 ■과 헷갈리지 않게 중립 회색, 45° 세운 모양.
        private Mesh MakeDiamond()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            var outline = new[] { new Vector3(0f, 0.5f), new Vector3(0.5f, 0f), new Vector3(0f, -0.5f), new Vector3(-0.5f, 0f) };
            for (int side = 0; side < 2; side++)
            {
                int start = verts.Count;
                var normal = side == 0 ? Vector3.back : Vector3.forward;
                foreach (var v in outline)
                {
                    verts.Add(v);
                    normals.Add(normal);
                }
                if (side == 0) { tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 }); }
                else { tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 }); }
            }
            var mesh = new Mesh { name = "CardRush_CardDiamond", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }

        public void Dispose()
        {
            foreach (var o in owned)
            {
                CardRushGagFx.DestroyObject(o);
            }
            owned.Clear();
        }
    }

    /// <summary>
    /// 카드 판 한 장 — 금 테두리 + 앞면(색 판 + 흰 모양) 또는 뒷면(회색 판 + 마름모). 높이 1·폭 <see cref="Aspect"/> 기준으로 짓고
    /// <see cref="Root"/>의 크기로 키운다. 보는 쪽은 -z. 콜라이더 없음 — 그림 전용(머리 위 카드·날아가는 카드·문에 꽂힌 카드가 같은 판).
    /// </summary>
    public sealed class CardRushCardPlate
    {
        public const float Aspect = 0.7f;
        public const float RimOfHeight = 0.08f;

        private readonly CardRushCardMaterials materials;
        private readonly Renderer plate;
        private readonly MeshFilter shapeFilter;
        private readonly Renderer shape;
        private readonly Renderer backMark;
        private bool visible = true;
        private int drawn = -1;   //  지금 그린 (앞/뒤, 색, 밝음) — 같으면 재질을 다시 안 넣는다

        public Transform Root { get; }
        /// <summary>넘김·통통이 크기를 주는 판 몸(테두리·판·모양). 링은 여기 밖이라 같이 찌그러지지 않는다.</summary>
        public Transform Body { get; }

        public CardRushCardPlate(Transform parent, string name, CardRushCardMaterials materials, HideFlags hideFlags = HideFlags.None)
        {
            this.materials = materials;
            var rootGo = new GameObject(name) { hideFlags = hideFlags };
            Root = rootGo.transform;
            Root.SetParent(parent, false);
            var bodyGo = new GameObject("Body") { hideFlags = hideFlags };
            Body = bodyGo.transform;
            Body.SetParent(Root, false);

            float rim = RimOfHeight;
            //  테두리: 판보다 rim만큼 크고 얇다. 판이 1 cm(높이 1 기준) 앞에 서서 같은 평면 깜빡임이 없다.
            Cube("Rim", Body, new Vector3(0f, 0f, 0f), new Vector3(Aspect + 2f * rim, 1f + 2f * rim, 0.03f), materials.Rim, hideFlags);
            plate = Cube("Plate", Body, new Vector3(0f, 0f, -0.01f), new Vector3(Aspect, 1f, 0.04f), materials.BackPlate, hideFlags);

            var shapeGo = new GameObject("Shape") { hideFlags = hideFlags };
            shapeGo.transform.SetParent(Body, false);
            shapeGo.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            shapeGo.transform.localScale = Vector3.one * (Aspect * 0.72f);
            shapeFilter = shapeGo.AddComponent<MeshFilter>();
            shape = Renderer(shapeGo, materials.Palette.Decal);

            var markGo = new GameObject("BackMark") { hideFlags = hideFlags };
            markGo.transform.SetParent(Body, false);
            markGo.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            markGo.transform.localScale = new Vector3(Aspect * 0.6f, Aspect * 0.85f, 1f);
            markGo.AddComponent<MeshFilter>().sharedMesh = materials.Diamond;
            backMark = Renderer(markGo, materials.BackMarkMaterial);

            SetFace(false, default, false);
        }

        private static Renderer Renderer(GameObject go, Material material)
        {
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private static Renderer Cube(string name, Transform parent, Vector3 at, Vector3 scale, Material material, HideFlags hideFlags)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.hideFlags = hideFlags;
            CardRushGagFx.DestroyObject(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>앞면이면 그 색 판 + 흰 모양(빨 ●, 노 ▲, 파 ■), 뒷면이면 회색 판 + 마름모.</summary>
        public void SetFace(bool faceUp, CardColor color, bool bright)
        {
            int key = faceUp ? 1 + (int)color * 2 + (bright ? 1 : 0) : 0;
            if (key == drawn)
            {
                return;
            }
            drawn = key;
            plate.sharedMaterial = faceUp ? materials.Face(color, bright) : materials.BackPlate;
            shape.enabled = faceUp;
            backMark.enabled = faceUp == false;
            if (faceUp)
            {
                shapeFilter.sharedMesh = materials.Palette.ShapeOf(color);
            }
        }

        public void SetVisible(bool on)
        {
            if (visible == on || Root == null)
            {
                return;
            }
            visible = on;
            Root.gameObject.SetActive(on);
        }

        public bool Visible => visible;

        public void Destroy()
        {
            if (Root != null)
            {
                CardRushGagFx.DestroyObject(Root.gameObject);
            }
        }
    }
}
