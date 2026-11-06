using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 트랙 그림의 짓기(spec 2026-11-05 §11.1) — 레인 경계선·출발선·10 m 눈금, 결승선 + 결승 문 + 술래 얼굴 판. 실행 중 뷰와 에디터 미리보기가
    /// 같은 식으로 짓는다. 원시 도형이고 콜라이더가 없다(판정은 시뮬 식이 한다).
    /// </summary>
    public static class CardRushTrackDecor
    {
        public const float LineWidth = 0.06f;
        public const float LineAlpha = 0.35f;
        public const float TickEveryMeters = 10f;
        public const float TickLengthOfLane = 0.5f;
        /// <summary>바닥 그림을 맵 바닥에서 띄우는 높이 — 같은 평면이면 깜빡인다(광산 부품 교훈 5~10 mm).</summary>
        public const float FloorLift = 0.008f;

        public const float FinishBandDepth = 1.0f;
        public const float GatePostHeight = 4.2f;
        public const float BannerHeight = 3.6f;
        public const float SullaeHeight = 6.1f;
        public const float SullaeRadius = 1.3f;

        /// <summary>한 레인의 바닥 그림 — 몸이 나타날 때 그 레인에 한 번 짓는다.</summary>
        public static GameObject BuildLane(Transform parent, string name, Vector3 laneOrigin, Vector3 forward, float laneSpacing, int trackMm,
                                           CardRushStageMaterials materials, HideFlags hideFlags = HideFlags.None)
        {
            var f = CardRushLanes.Flat(forward);
            var root = CardRushDraw.Node(name, parent, hideFlags);
            root.transform.SetPositionAndRotation(laneOrigin, Quaternion.LookRotation(f, Vector3.up));
            float length = trackMm / 1000f + 2f;
            float half = laneSpacing * 0.5f;
            var block = new MaterialPropertyBlock();

            //  경계선(옅은 흰 줄) — 출발 1 m 뒤부터 결승 2 m 너머까지.
            Line(root.transform, "EdgeL", new Vector3(-half, FloorLift, length * 0.5f - 1f), new Vector3(LineWidth, 0.004f, length + 1f), materials, block, LineAlpha, hideFlags);
            Line(root.transform, "EdgeR", new Vector3(half, FloorLift, length * 0.5f - 1f), new Vector3(LineWidth, 0.004f, length + 1f), materials, block, LineAlpha, hideFlags);
            //  출발선 — 진하게.
            Line(root.transform, "Start", new Vector3(0f, FloorLift, 0f), new Vector3(laneSpacing, 0.004f, 0.12f), materials, block, 0.85f, hideFlags);
            //  10 m 눈금(진행감) — 레인 가운데 반 폭.
            for (float m = TickEveryMeters; m < trackMm / 1000f - 0.5f; m += TickEveryMeters)
            {
                Line(root.transform, "Tick " + m, new Vector3(0f, FloorLift, m), new Vector3(laneSpacing * TickLengthOfLane, 0.004f, 0.08f), materials, block, LineAlpha, hideFlags);
            }
            return root;
        }

        private static void Line(Transform parent, string name, Vector3 at, Vector3 size, CardRushStageMaterials materials, MaterialPropertyBlock block, float alpha, HideFlags hideFlags)
        {
            var go = CardRushDraw.Box(name, parent, at, size, materials.Clear, hideFlags);
            CardRushDraw.Tint(go.GetComponent<Renderer>(), block, CardRushStageMaterials.Line, alpha);
        }

        /// <summary>
        /// 결승선(체크 띠) + 결승 문(나무 기둥 둘·현수막) — 8레인 전체 폭에 한 번. 현수막 글은 <paramref name="label"/>이 있을 때만(실행 중).
        /// 술래 얼굴 판은 문 위 가운데에 세워 돌려준다.
        /// </summary>
        public static GameObject BuildFinish(Transform parent, Vector3 markerOrigin, Vector3 forward, float laneSpacing, int trackMm,
                                             CardRushStageMaterials materials, HideFlags hideFlags, out CardRushSullaeFace sullae)
        {
            var f = CardRushLanes.Flat(forward);
            var root = CardRushDraw.Node("Finish", parent, hideFlags);
            root.transform.SetPositionAndRotation(markerOrigin + f * (trackMm / 1000f), Quaternion.LookRotation(f, Vector3.up));
            float span = laneSpacing * CardRushLanes.MaxLanes;
            float half = span * 0.5f;

            //  결승선 — 결승 자리(TrackMm)가 띠의 앞 끝. 몸이 띠를 밟는 순간이 완주다.
            CardRushDraw.Box("CheckerBand", root.transform, new Vector3(0f, FloorLift, -FinishBandDepth * 0.5f), new Vector3(span, 0.004f, FinishBandDepth), materials.Checker, hideFlags);

            for (int side = -1; side <= 1; side += 2)
            {
                var post = CardRushDraw.Primitive(PrimitiveType.Cylinder, side < 0 ? "PostL" : "PostR", root.transform, materials.WoodMat, hideFlags);
                post.transform.localPosition = new Vector3(side * (half + 0.4f), GatePostHeight * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.38f, GatePostHeight * 0.5f, 0.38f);
            }
            CardRushDraw.Box("Banner", root.transform, new Vector3(0f, BannerHeight, 0f), new Vector3(span + 1.2f, 0.9f, 0.12f), materials.BannerMat, hideFlags);
            CardRushDraw.Box("BannerTrim", root.transform, new Vector3(0f, BannerHeight - 0.5f, 0f), new Vector3(span + 1.2f, 0.1f, 0.14f), materials.WoodDarkMat, hideFlags);

            sullae = new CardRushSullaeFace(root.transform, materials, hideFlags);
            sullae.Root.localPosition = new Vector3(0f, SullaeHeight, 0f);
            return root;
        }
    }

    /// <summary>
    /// 술래 얼굴 판(spec §11.5) — 결승 문 위 큰 얼굴(원 두 개 눈). 내 구역이 켜져 있으면 눈동자가 빨강 — 시안 "술래의 신호". 카메라를 본다(y축만).
    /// </summary>
    public sealed class CardRushSullaeFace
    {
        private readonly Renderer[] pupils = new Renderer[2];
        private readonly CardRushStageMaterials materials;
        private bool angry;

        public Transform Root { get; }
        public bool Angry => angry;

        public CardRushSullaeFace(Transform parent, CardRushStageMaterials materials, HideFlags hideFlags)
        {
            this.materials = materials;
            Root = CardRushDraw.Node("Sullae", parent, hideFlags).transform;
            float r = CardRushTrackDecor.SullaeRadius;
            //  판은 원기둥을 눕혀 -z(보는 쪽)를 향한다. 머리카락은 뒤에 조금 위로 비켜 깔아 위 반쪽만 보인다.
            Disc("Hair", new Vector3(0f, r * 0.3f, 0.06f), r * 2.05f, materials.HairMat, hideFlags);
            Disc("Face", Vector3.zero, r * 2f, materials.SkinMat, hideFlags);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * r * 0.36f;
                Disc("Eye", new Vector3(x, -r * 0.02f, -0.05f), r * 0.42f, materials.EyeMat, hideFlags);
                pupils[i] = Disc("Pupil", new Vector3(x, -r * 0.05f, -0.09f), r * 0.2f, materials.PupilMat, hideFlags).GetComponent<Renderer>();
            }
        }

        private GameObject Disc(string name, Vector3 at, float diameter, Material material, HideFlags hideFlags)
        {
            var go = CardRushDraw.Primitive(PrimitiveType.Cylinder, name, Root, material, hideFlags);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, 0.02f, diameter);
            return go;
        }

        public void Update(Camera cam, bool angryNow)
        {
            if (cam != null)
            {
                var look = Root.position - cam.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-6f) Root.rotation = Quaternion.LookRotation(look, Vector3.up);
            }
            if (angryNow == angry) return;
            angry = angryNow;
            foreach (var p in pupils) p.sharedMaterial = angry ? materials.PupilAngryMat : materials.PupilMat;
        }
    }

    /// <summary>
    /// 트랙 그림(spec §11.1, 옛 <c>CardRushCourseView</c> 자리) — 몸이 나타나면 그 레인에 바닥 줄을 깔고, 결승 문·술래 얼굴은 한 번.
    /// 술래 눈동자는 내 구역이 그림 틱에 켜져 있을 때 빨갛다. 그림만 다루고 상태는 읽기만 한다.
    /// </summary>
    public class CardRushTrackView : IStartable, IPostLateTickable, IDisposable
    {
        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly EntityRenderClock renderClock;
        private readonly IPlayerContext playerContext;
        private readonly CameraController cameraController;

        private readonly Dictionary<string, GameObject> lanes = new Dictionary<string, GameObject>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly List<string> gone = new List<string>();
        private CardRushStageMaterials materials;
        private GameObject root;
        private GameObject finish;
        private CardRushSullaeFace sullae;
        private CardRushWorldLabel banner;
        private CardRushTrack builtFor;

        /// <summary>현수막 글 크기 — 패널 1 px ≈ 1 cm라 2배면 글판이 약 5 m(이름표와 같은 패널 설정).</summary>
        public const float BannerLabelScale = 2f;

        public CardRushTrackView(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry, EntityRenderClock renderClock,
                                 IPlayerContext playerContext, CameraController cameraController)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.renderClock = renderClock;
            this.playerContext = playerContext;
            this.cameraController = cameraController;
        }

        /// <summary>지금 깔린 레인 수(몸 수)와 결승 문 — 시험·진단용.</summary>
        public int LaneCount => lanes.Count;
        public GameObject Finish => finish;
        public CardRushSullaeFace Sullae => sullae;

        public void Start()
        {
            materials = new CardRushStageMaterials();
            root = new GameObject("CardRushTrack");
        }

        public void PostLateTick()
        {
            var track = world.Track;
            var rules = world.Rules;
            if (track == null || rules == null || rules.HasRules == false || root == null)
            {
                return;
            }
            if (ReferenceEquals(track, builtFor) == false)
            {
                Clear();
                builtFor = track;
                finish = CardRushTrackDecor.BuildFinish(root.transform, rules.Origin, rules.Forward, rules.LaneSpacing, track.TrackMm, materials,
                                                        HideFlags.None, out sullae);
                banner = CardRushWorldLabel.Create("FinishBanner", finish.transform);
                //  현수막 글은 달려오는 쪽(-앞)을 본다 — 패널의 앞면이 transform의 -z라 진행 방향을 그대로 준다.
                banner?.Pin("결승", finish.transform.position + Vector3.up * CardRushTrackDecor.BannerHeight - CardRushLanes.Flat(rules.Forward) * 0.08f,
                            Quaternion.LookRotation(CardRushLanes.Flat(rules.Forward), Vector3.up), BannerLabelScale, new Color(0.35f, 0.2f, 0.08f));
            }

            seen.Clear();
            string me = playerContext?.entityId;
            bool angry = false;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var runner = entity.Get<CardRushRunner>();
                if (runner == null)
                {
                    continue;
                }
                seen.Add(entity.Id);
                if (lanes.ContainsKey(entity.Id) == false)
                {
                    lanes.Add(entity.Id, CardRushTrackDecor.BuildLane(root.transform, "Lane " + entity.Id, runner.LaneOrigin, rules.Forward,
                                                                      rules.LaneSpacing, track.TrackMm, materials));
                }
                if (entity.Id == me)
                {
                    double d = CardRushRunnerFxView.DrawnTick(renderClock, me, me);
                    angry = CardRushHazardLayout.ZoneLook(runner.State, runner.State.ZoneIndex, d, track).Phase == CardRushZonePhase.Warning;
                }
            }

            gone.Clear();
            foreach (var id in lanes.Keys)
            {
                if (seen.Contains(id) == false) gone.Add(id);
            }
            foreach (var id in gone)
            {
                CardRushGagFx.DestroyObject(lanes[id]);
                lanes.Remove(id);
            }

            var cam = cameraController != null && cameraController.MainCamera != null ? cameraController.MainCamera : Camera.main;
            sullae?.Update(cam, angry);
        }

        private void Clear()
        {
            foreach (var lane in lanes.Values) CardRushGagFx.DestroyObject(lane);
            lanes.Clear();
            banner?.Destroy();
            banner = null;
            CardRushGagFx.DestroyObject(finish);
            finish = null;
            sullae = null;
        }

        public void Dispose()
        {
            Clear();
            CardRushGagFx.DestroyObject(root);
            root = null;
            materials?.Dispose();
            materials = null;
        }
    }
}
