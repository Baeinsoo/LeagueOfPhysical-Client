using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 머리 위 카드(spec 2026-11-05 §11.4) — 몸마다 카드 판 하나를 머리 위에 띄워 카메라 쪽으로 돌린다(y축만).
    /// 그림은 <see cref="CardRushHeadCard.Compute"/>의 결과를 그리기만 한다: 내 카드는 <see cref="PlayerInputManager.NextInputTick"/>
    /// (지금 누르면 쓰는 틱 — 본 카드 = 쓰는 카드), 남의 카드는 그림 틱(<see cref="CardRushRunnerFxView"/>와 같은 식).
    /// 남의 카드는 내 레인 ±<see cref="OtherLaneRange"/>만, 작게. 기절·쿨다운 중엔 흐리게(알파 0.35). 시뮬 상태는 읽기만 한다.
    /// <para>틱·보간 뒤(<see cref="IPostLateTickable"/>, PreLateUpdate 끝)에서 칠한다 — 그 프레임 틱이 이미 돌았고 보간기 LateUpdate가 몸을
    /// 옮긴 뒤다. 그래서 내 카드의 <c>NextInputTick</c> = 다음 프레임 틱 앞 탭(CardRushTapGrab)이 실릴 틱이고(본 색 = 판정 색), 카드·손 자리가
    /// 몸에 붙는다. ITickable(Update 맨 앞)이면 그 프레임 틱이 돈 뒤 화면이 한 틱 늦은 색이었다(최종 리뷰 I1).</para>
    /// </summary>
    public class CardRushHeadCardView : IStartable, IPostLateTickable, IDisposable
    {
        /// <summary>정수리(<see cref="ChibiHeadSpots.Crown"/>) 위로 이만큼 띄운 자리가 카드 아래 끝.</summary>
        public const float HeadCardLift = 0.22f;
        public const float MineHeight = 0.7f;
        public const float OtherHeight = 0.4f;
        public const int OtherLaneRange = 3;
        /// <summary>카메라 쪽으로 당기는 거리 — 뒤 위에서 보면 카드가 머리 뒤에 서 있어 아래가 가린다. 화면 자리는 거의 그대로다.</summary>
        public const float TowardCamera = 0.6f;

        /// <summary>카드 가운데(카메라 쪽으로 당기기 전). 옛 치비에서 맞춘 "머리 뼈 + 1.0 m" = 정수리 + 0.22 m를 정수리 기준으로 옮겼다.</summary>
        public static Vector3 CardCenter(Vector3 crown, float height) => crown + Vector3.up * (HeadCardLift + height * 0.5f);

        /// <summary>
        /// 카드 기준점 — 몸개그 자세를 벗긴 정수리. 자세를 받은 정수리를 따르면 꽈당 구르기 동안 카드가 몸 둘레를 돌고
        /// 거꾸로 선 순간 바닥까지 내려간다(최종 리뷰 m5). 기절 중 흐린 카드는 제자리에 떠 있어야 한다.
        /// 쉴 때 자세는 자세를 주는 뷰와 같은 값(<see cref="CardRushRunnerFxView.ModelRest.Of"/>)이라 두 뷰의 순서에 기대지 않는다.
        /// </summary>
        public static Vector3 Anchor(Transform visual, Transform model, Transform head)
        {
            if (visual == null || model == null || model == visual)
            {
                return ChibiHeadSpots.Crown(model, head);
            }
            var rest = CardRushRunnerFxView.ModelRest.Of(model);
            return ChibiHeadSpots.CrownAtRest(visual, model, head, rest.Position, rest.Rotation, rest.Scale);
        }

        public const float FlipSeconds = 0.06f;    //  뒷면 → 앞면 넘김(가로 찌그러짐)
        /// <summary>흐린 카드의 알파(시안) — 기절 중(돌지만 못 쓴다)·쿨다운 중.</summary>
        public const float DimAlpha = 0.35f;
        /// <summary>잡은 카드 — 1.25배로 커지고 위로 0.15 m 뜬다.</summary>
        public const float HeldScale = 1.25f;
        public const float HeldLift = 0.15f;
        /// <summary>평소 뒷면 숨쉬기 — 커진 반에서 1.08배.</summary>
        public const float ReadyScale = 1.08f;

        /// <summary>그 모습의 크기 배율(잡음 1.25, 사라지는 중 1 → 0).</summary>
        public static float ScaleOf(in CardRushHeadCardFace face)
            => face.Vanish01 > 0f ? Mathf.Clamp01(1f - face.Vanish01) : face.Held ? HeldScale : face.Ready && face.ReadyBig ? ReadyScale : 1f;

        /// <summary>위로 더 뜨는 높이(m) — 잡았을 때만.</summary>
        public static float LiftOf(in CardRushHeadCardFace face) => face.Held ? HeldLift : 0f;

        /// <summary>링이 보이나 — 잡았거나(5초) 돌고 있고(6초, 가늘게) 깜빡 꺼짐 구간이 아닐 때.</summary>
        public static bool RingShown(in CardRushHeadCardFace face) => (face.Held || face.Rolling) && face.RingBlink == false;

        private sealed class Entry
        {
            public Transform Visual;
            public Transform Model;
            public Transform Head;
            public CardRushCardPlate Plate;
            public bool LastFaceUp;
            public float FlipStart = -1f;
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
        private CardRushCardPalette palette;
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
            palette = new CardRushCardPalette();
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
            var track = world.Track;
            if (runner == null || track == null || input == null)
            {
                return false;
            }
            face = CardRushHeadCard.Compute(runner.State, input.NextInputTick, world.GameplayStartTick, runner.Lane, track);
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

        public void PostLateTick()
        {
            var rules = world.Rules;
            var track = world.Track;
            if (root == null || rules == null || rules.HasRules == false || track == null)
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
                    face = CardRushHeadCard.ComputeOther(runner.State, drawnTick, world.GameplayStartTick, runner.Lane, track);
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
            entries[id] = entry;
            return entry;
        }

        private void Draw(Entry entry, in CardRushHeadCardFace face, bool mine, Camera cam, float now)
        {
            if (face.Visible == false)
            {
                entry.Plate.SetVisible(false);
                entry.LastFaceUp = false;
                return;
            }

            float height = mine ? MineHeight : OtherHeight;
            var center = CardCenter(Anchor(entry.Visual, entry.Model, entry.Head), height) + Vector3.up * LiftOf(face);
            if (cam != null)
            {
                center += (cam.transform.position - center).normalized * TowardCamera;
            }
            var look = cam != null ? center - cam.transform.position : entry.Visual.forward;
            look.y = 0f;
            var rotation = look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity;
            entry.Plate.Root.SetPositionAndRotation(center, rotation);
            entry.Plate.Root.localScale = Vector3.one * (height * ScaleOf(face));

            //  뒷면 → 앞면은 시간으로 넘기고(0.06초), 색이 바뀐 장은 그림 틱으로 가로 0.6 → 1 펴진다(시안 sq).
            if (face.FaceUp && entry.LastFaceUp == false)
            {
                entry.FlipStart = now;
            }
            entry.Plate.SetVisible(true);
            entry.Plate.SetFace(face.FaceUp, face.Color, face.Dim);
            entry.Plate.SetRing(RingShown(face) ? face.RingT01 : 0f, thin: face.Rolling);
            entry.Plate.SetHeld(face.Held);
            entry.LastFaceUp = face.FaceUp;

            entry.Plate.Body.localScale = new Vector3(FlipWidth(now - entry.FlipStart) * ChangeWidth(face), 1f, 1f);
        }

        /// <summary>색이 바뀐 장의 가로 비율 — 뜬 틱 0.6에서 <see cref="CardRushHeadCard.FlipTicks"/>틱에 1. 뒷면이면 1.</summary>
        public static float ChangeWidth(in CardRushHeadCardFace face)
            => face.FaceUp ? Mathf.Lerp(CardRushHeadCard.FlipStartWidth, 1f, face.FlipT01) : 1f;

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
    /// 카드 판이 함께 쓰는 재질 — 앞면 색은 <see cref="CardRushCardPalette"/>의 것, 테두리는 모든 색 공통 진한 금 하나, 뒷면은 중립 회색.
    /// 흐린 판(기절·쿨다운)은 같은 색의 반투명 무광 짝(알파 <see cref="CardRushHeadCardView.DimAlpha"/>)으로 갈아 끼운다. 팔레트는 빌려 쓴다.
    /// </summary>
    public sealed class CardRushCardMaterials : IDisposable
    {
        /// <summary>테두리 금 — 노랑 카드(#F5C542)보다 진해 노랑 판 위에서도 테두리가 보인다.</summary>
        public static readonly Color Gold = new Color(0.72f, 0.52f, 0.10f);
        public static readonly Color Back = new Color(0.42f, 0.40f, 0.38f);
        public static readonly Color BackMark = new Color(0.58f, 0.56f, 0.53f);

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Material[] dimFaces = new Material[CardColors.Count];

        public CardRushCardPalette Palette { get; }
        public Material Rim { get; }
        public Material RingMaterial { get; }
        public Material HeldRim { get; }
        public Material BackPlate { get; }
        public Material BackMarkMaterial { get; }
        public Material DimRim { get; }
        public Material DimBackPlate { get; }
        public Material DimBackMark { get; }
        public Material DimDecal { get; }
        public Mesh Diamond { get; }

        public CardRushCardMaterials(CardRushCardPalette palette)
        {
            Palette = palette;
            Rim = Make("CardRush_CardRim", Gold);
            RingMaterial = Make("CardRush_CardRing", Color.white);
            HeldRim = Make("CardRush_CardHeldRim", new Color(1f, 0.97f, 0.85f));
            BackPlate = Make("CardRush_CardBack", Back);
            BackMarkMaterial = Make("CardRush_CardBackMark", BackMark);
            DimRim = MakeDim("CardRush_CardRimDim", Gold);
            DimBackPlate = MakeDim("CardRush_CardBackDim", Back);
            DimBackMark = MakeDim("CardRush_CardBackMarkDim", BackMark);
            DimDecal = MakeDim("CardRush_DecalDim", Color.white);
            for (int i = 0; i < CardColors.Count; i++)
            {
                dimFaces[i] = MakeDim("CardRush_CardDim_" + (CardColor)i, CardRushCardPalette.ColorOf((CardColor)i));
            }
            Diamond = MakeDiamond();
        }

        public Material Face(CardColor color, bool dim) => dim ? dimFaces[(int)color] : Palette.Of(color);

        private Material Make(string name, Color color)
        {
            var m = CardRushDraw.Opaque(name, color);
            owned.Add(m);
            return m;
        }

        private Material MakeDim(string name, Color color)
        {
            color.a = CardRushHeadCardView.DimAlpha;
            var m = CardRushDraw.Transparent(name, color);
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
    /// <see cref="Root"/>의 크기로 키운다. 보는 쪽은 -z. 콜라이더 없음 — 그림 전용.
    /// </summary>
    public sealed class CardRushCardPlate
    {
        public const float Aspect = 0.7f;
        public const float RimOfHeight = 0.08f;
        public const int RingSegments = 24;
        public const float RingRadius = 0.78f;
        public const float RingSegment = 0.09f;
        public const float ThinRingSegment = 0.05f;

        private readonly CardRushCardMaterials materials;
        private readonly Renderer rim;
        private readonly Renderer plate;
        private readonly MeshFilter shapeFilter;
        private readonly Renderer shape;
        private readonly Renderer backMark;
        private bool visible = true;
        private bool held;
        private bool dimDrawn;
        private readonly Transform[] ringSegments = new Transform[RingSegments];
        private bool ringThin;
        private int ringShown = -1;
        private int drawn = -1;   //  지금 그린 (앞/뒤, 색, 흐림) — 같으면 재질을 다시 안 넣는다

        public Transform Root { get; }
        /// <summary>넘김이 크기를 주는 판 몸(테두리·판·모양).</summary>
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

            float r = RimOfHeight;
            //  테두리: 판보다 r만큼 크고 얇다. 판이 1 cm(높이 1 기준) 앞에 서서 같은 평면 깜빡임이 없다.
            rim = CardRushDraw.Box("Rim", Body, Vector3.zero, new Vector3(Aspect + 2f * r, 1f + 2f * r, 0.03f), materials.Rim, hideFlags).GetComponent<Renderer>();
            plate = CardRushDraw.Box("Plate", Body, new Vector3(0f, 0f, -0.01f), new Vector3(Aspect, 1f, 0.04f), materials.BackPlate, hideFlags).GetComponent<Renderer>();

            var shapeGo = CardRushDraw.Node("Shape", Body, hideFlags);
            shapeGo.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            shapeGo.transform.localScale = Vector3.one * (Aspect * 0.72f);
            shapeFilter = shapeGo.AddComponent<MeshFilter>();
            shape = Renderer(shapeGo, materials.Palette.Decal);

            var markGo = CardRushDraw.Node("BackMark", Body, hideFlags);
            markGo.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            markGo.transform.localScale = new Vector3(Aspect * 0.6f, Aspect * 0.85f, 1f);
            markGo.AddComponent<MeshFilter>().sharedMesh = materials.Diamond;
            backMark = Renderer(markGo, materials.BackMarkMaterial);

            for (int i = 0; i < RingSegments; i++)
            {
                //  카드 둘레를 도는 작은 조각 — 위(12시)에서 시계 방향으로 놓는다. 남은 몫만큼 앞 조각부터 켠다.
                float a = (float)i / RingSegments * Mathf.PI * 2f;
                var seg = CardRushDraw.Box("Ring" + i, Root, new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * RingRadius,
                                           new Vector3(RingSegment, RingSegment, 0.03f), materials.RingMaterial, hideFlags);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                seg.SetActive(false);
                ringSegments[i] = seg.transform;
            }

            SetFace(false, default, false);
        }

        /// <summary>링이 몇 조각 켜지나 — 남은 몫(1 → 0)을 올림. 몫이 0이면 0.</summary>
        public static int RingCount(float t01) => t01 <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(t01 * RingSegments), 0, RingSegments);

        /// <summary>링 — 남은 몫만큼 조각을 켠다(0이면 모두 끔). 도는 6초 링은 가늘게(잡은 5초 링과 구별).</summary>
        public void SetRing(float t01, bool thin = false)
        {
            int n = RingCount(t01);
            if (thin != ringThin)
            {
                ringThin = thin;
                float size = thin ? ThinRingSegment : RingSegment;
                for (int i = 0; i < RingSegments; i++)
                {
                    ringSegments[i].localScale = new Vector3(size, size, 0.03f);
                }
            }
            if (n == ringShown)
            {
                return;
            }
            ringShown = n;
            for (int i = 0; i < RingSegments; i++)
            {
                ringSegments[i].gameObject.SetActive(i < n);
            }
        }

        /// <summary>잡은 카드는 테두리가 흰 빛(흐린 판이면 흐린 테두리 그대로).</summary>
        public void SetHeld(bool on)
        {
            if (held == on)
            {
                return;
            }
            held = on;
            ApplyRim();
        }

        public int RingOn => ringShown < 0 ? 0 : ringShown;
        public bool RingThin => ringThin;
        public bool Held => held;

        private void ApplyRim() => rim.sharedMaterial = dimDrawn ? materials.DimRim : held ? materials.HeldRim : materials.Rim;

        private static Renderer Renderer(GameObject go, Material material)
        {
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>앞면이면 그 색 판 + 흰 모양(빨 ●, 노 ▲, 파 ■), 뒷면이면 회색 판 + 마름모. 흐리면 모든 겹이 반투명 짝으로.</summary>
        public void SetFace(bool faceUp, CardColor color, bool dim)
        {
            int key = (faceUp ? 1 + (int)color : 0) * 2 + (dim ? 1 : 0);
            if (key == drawn)
            {
                return;
            }
            drawn = key;
            dimDrawn = dim;
            ApplyRim();
            plate.sharedMaterial = faceUp ? materials.Face(color, dim) : dim ? materials.DimBackPlate : materials.BackPlate;
            shape.enabled = faceUp;
            backMark.enabled = faceUp == false;
            if (faceUp)
            {
                shapeFilter.sharedMesh = materials.Palette.ShapeOf(color);
                shape.sharedMaterial = dim ? materials.DimDecal : materials.Palette.Decal;
            }
            else
            {
                backMark.sharedMaterial = dim ? materials.DimBackMark : materials.BackMarkMaterial;
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
        /// <summary>판 재질 — 시험·진단용.</summary>
        public Material PlateMaterial => plate.sharedMaterial;

        public void Destroy()
        {
            if (Root != null)
            {
                CardRushGagFx.DestroyObject(Root.gameObject);
            }
        }
    }
}
