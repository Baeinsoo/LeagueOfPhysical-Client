using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 관중석 — 과녁 너머에 코드 인형 관중을 세우고, 경기에 반응하게 하고, 깃발로 바람을 보여 준다.
    /// 화살이 관중석에 실제로 꽂히면 가장 가까운 관객 머리에 화살을 붙인다. 모두 연출이다(판정 무관).
    /// 맵 씬이 뜬 뒤(<see cref="ArcheryCourse.SharedLane"/>이 생긴 뒤) 처음 도는 프레임에 만든다.
    /// </summary>
    public class ArcheryCrowdView : IStartable, System.IDisposable
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private static readonly string[] SignTexts = { "{k}P 결혼해줘", "{k}P야 과녁은 저기", "10점!!", "{k}P 파이팅", "엄마 나 TV 나와" };

        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;
        private readonly ArcheryArrowLandings landings;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ArcheryShootOffNarrator narrator = new ArcheryShootOffNarrator();

        private ArcheryCrowdDriver driver;
        private System.IDisposable subscription;
        private GameObject root;
        private ArcheryCrowdLayout layout;
        private ArcheryCrowdDirector director;
        private Material material;
        private Mesh clothMesh;
        private Mesh arrowMesh;
        private readonly List<Fan> fans = new List<Fan>();
        private readonly List<Vector3> heads = new List<Vector3>();
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        private bool hasIndex;
        private int lastIndex;
        private float wind;

        private sealed class Fan
        {
            public Transform Root;
            public Transform LeftArm;
            public Transform RightArm;
            public Renderer Body;
            public Transform Cloth;
            public Color Shirt;
            public bool Gray;
        }

        public ArcheryCrowdView(ArcheryCourse course, ArcheryWorld world, GameFramework.Runner.IRunner runner,
                                ISubscriber<WorldEventBatchToC> batchSubscriber, ArcheryArrowLandings landings,
                                IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.course = course;
            this.world = world;
            this.runner = runner;
            this.batchSubscriber = batchSubscriber;
            this.landings = landings;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void Start()
        {
            subscription = batchSubscriber.Subscribe(OnWorldEventBatch);
            landings.Landed += OnLanded;
            driver = new GameObject(nameof(ArcheryCrowdDriver)).AddComponent<ArcheryCrowdDriver>();
            driver.View = this;
        }

        public void Dispose()
        {
            subscription?.Dispose();
            landings.Landed -= OnLanded;
            if (driver != null)
            {
                driver.View = null;
                Object.Destroy(driver.gameObject);
                driver = null;
            }
            foreach (var c in colliders)
            {
                landings.RemoveCrowdCollider(c);
            }
            colliders.Clear();
            if (root != null)
            {
                Object.Destroy(root);
                root = null;
            }
            if (material != null) { Object.Destroy(material); material = null; }
            if (clothMesh != null) { Object.Destroy(clothMesh); clothMesh = null; }
            if (arrowMesh != null) { Object.Destroy(arrowMesh); arrowMesh = null; }
            fans.Clear();
            heads.Clear();
        }

        public void Apply()
        {
            if (course.IsShootOff == false)
            {
                return;
            }
            if (root == null && TryBuild() == false)
            {
                return;   // 맵 씬이 아직 안 떴다 — 다음 프레임에 다시 본다
            }
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return;
            }

            float now = Time.time;
            double interval = runner.tickUpdater.interval;
            long tick = (long)System.Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            long start = world.GameplayStartTick;
            int index = course.IndexAt(tick, start);
            bool inRound = index >= 0 && index < course.StepCount;

            if (inRound)
            {
                wind = WindAcross(course.WindAt(tick, start));   // 라운드 밖에는 마지막 바람을 그대로 둔다
            }
            director.BaseMood = inRound && course.MultiplierAt(index) >= 2 ? ArcheryCrowdMood.Hush : ArcheryCrowdMood.Idle;

            if (hasIndex && index != lastIndex && index >= course.StepCount && lastIndex >= 0 && lastIndex < course.StepCount)
            {
                director.OnMatchEnd(now);
            }
            hasIndex = true;
            lastIndex = index;

            for (int i = 0; i < fans.Count; i++)
            {
                Pose(i, now);
            }
        }

        private void Pose(int i, float now)
        {
            var fan = fans[i];
            var seat = layout.Seats[i];
            var pose = ArcheryCrowdPose.At(director.MoodOf(i, now), now, seat.Phase);
            fan.Root.position = seat.Position + Vector3.up * (pose.Lift * seat.Scale) + layout.Right * pose.Sway;
            fan.LeftArm.localRotation = Quaternion.AngleAxis(-pose.LeftArmDegrees, Vector3.forward);
            fan.RightArm.localRotation = Quaternion.AngleAxis(pose.RightArmDegrees, Vector3.forward);
            if (pose.Gray != fan.Gray)
            {
                fan.Gray = pose.Gray;
                Paint(fan.Body, pose.Gray ? ArcheryCrowdLayout.BooGray : fan.Shirt);
            }
            if (fan.Cloth != null)
            {
                var flag = ArcheryFlagPose.At(wind, now, seat.Phase);
                Vector3 side = flag.Side == 0 ? Vector3.down
                    : Vector3.Slerp(Vector3.down, layout.Right * flag.Side, Mathf.Max(0.35f, flag.Extend)).normalized;
                fan.Cloth.rotation = Quaternion.AngleAxis(flag.FlapDegrees, side) * Quaternion.FromToRotation(Vector3.right, side);
            }
        }

        //  깃발 값 = HUD 바람 화살표와 같은 식(양수 = 사수 기준 오른쪽).
        private float WindAcross(Vector3 worldWind)
        {
            var lane = course.SharedLane;
            return lane.HasValue ? Vector3.Dot(worldWind, ArcheryTargetMotion.ShooterRightAxis(-lane.Value.Forward)) : 0f;
        }

        private bool TryBuild()
        {
            var lane = course.SharedLane;
            if (lane.HasValue == false)
            {
                return false;
            }
            layout = ArcheryCrowdLayout.Build(lane.Value.ShooterPosition, lane.Value.Forward);
            director = new ArcheryCrowdDirector(layout.Seats.Count, () => Random.value);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            clothMesh = BuildCloth();
            root = new GameObject("ArcheryCrowd");

            foreach (var step in layout.Steps)
            {
                var box = Part(PrimitiveType.Cube, root.transform, step.Size, new Color(0.55f, 0.47f, 0.4f), keepCollider: true);
                box.SetPositionAndRotation(step.Center, layout.Rotation);
                var collider = box.GetComponent<Collider>();
                colliders.Add(collider);
                landings.AddCrowdCollider(collider);
            }
            //  관중 키 높이의 안 보이는 상자 — 단 위로 날아온 화살도 관중석에 꽂힌 것으로 본다.
            foreach (var volume in layout.Volumes)
            {
                var go = new GameObject("CrowdVolume");
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(volume.Center, layout.Rotation);
                var collider = go.AddComponent<BoxCollider>();
                collider.size = volume.Size;
                colliders.Add(collider);
                landings.AddCrowdCollider(collider);
            }

            var roster = Roster();
            for (int i = 0; i < layout.Seats.Count; i++)
            {
                fans.Add(BuildFan(layout.Seats[i], roster));
                heads.Add(layout.Seats[i].Position + Vector3.up * (1.3f * layout.Seats[i].Scale));
            }
            return true;
        }

        private Fan BuildFan(ArcheryCrowdSeat seat, List<string> roster)
        {
            var fanRoot = new GameObject("Fan").transform;
            fanRoot.SetParent(root.transform, false);
            fanRoot.SetPositionAndRotation(seat.Position, layout.Rotation);
            fanRoot.localScale = Vector3.one * seat.Scale;

            Color shirt = ArcheryCrowdLayout.Shirts[seat.Shirt];
            Color skin = ArcheryCrowdLayout.Skins[seat.Skin];
            var body = Part(PrimitiveType.Capsule, fanRoot, new Vector3(0.5f, 0.55f, 0.35f), shirt);
            body.localPosition = new Vector3(0f, 0.55f, 0f);
            var head = Part(PrimitiveType.Sphere, fanRoot, Vector3.one * 0.36f, skin);
            head.localPosition = new Vector3(0f, 1.3f, 0f);

            var fan = new Fan
            {
                Root = fanRoot,
                LeftArm = Arm(fanRoot, -0.28f, skin),
                RightArm = Arm(fanRoot, 0.28f, skin),
                Body = body.GetComponent<Renderer>(),
                Shirt = shirt,
            };

            if (seat.Flag)
            {
                var pole = Part(PrimitiveType.Cube, fanRoot, new Vector3(0.04f, 1.4f, 0.04f), new Color(0.36f, 0.27f, 0.21f));
                pole.localPosition = new Vector3(0.4f, 1.6f, 0f);
                var cloth = new GameObject("Cloth").transform;
                cloth.SetParent(fanRoot, false);
                cloth.localPosition = new Vector3(0.4f, 2.3f, 0f);
                cloth.gameObject.AddComponent<MeshFilter>().sharedMesh = clothMesh;
                var renderer = cloth.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Paint(renderer, shirt);
                fan.Cloth = cloth;
            }

            if (seat.Sign >= 0)
            {
                BuildSign(fanRoot, SignText(seat.Sign, roster));
            }
            return fan;
        }

        //  팔은 어깨를 축으로 돈다(0도 = 아래). 막대는 축에서 아래로 매달린다.
        private Transform Arm(Transform fanRoot, float x, Color skin)
        {
            var pivot = new GameObject("Arm").transform;
            pivot.SetParent(fanRoot, false);
            pivot.localPosition = new Vector3(x, 1.0f, 0f);
            var stick = Part(PrimitiveType.Cube, pivot, new Vector3(0.09f, 0.5f, 0.09f), skin);
            stick.localPosition = new Vector3(0f, -0.25f, 0f);
            return pivot;
        }

        //  위치는 부르는 쪽이 정한다.
        private Transform Part(PrimitiveType type, Transform parent, Vector3 scale, Color color,
                               bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (keepCollider == false)
            {
                Object.Destroy(go.GetComponent<Collider>());   // 관중 몸에 화살이 걸리면 땅으로 읽힌다 — 상자만 받는다
            }
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Paint(renderer, color);
            return go.transform;
        }

        private void Paint(Renderer renderer, Color color)
        {
            block.Clear();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        //  삼각 천 — 막대 꼭대기에서 +X로 뻗는다. 양면이 보이게 앞뒤 삼각형을 다 넣는다.
        private static Mesh BuildCloth()
        {
            var mesh = new Mesh { name = "CrowdFlagCloth" };
            mesh.vertices = new[] { Vector3.zero, new Vector3(0.7f, -0.2f, 0f), new Vector3(0f, -0.45f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private void BuildSign(Transform fanRoot, string text)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            if (panelSettings == null)
            {
                Debug.LogError($"[ArcheryCrowdView] 리소스 로드 실패: {PanelSettingsResource}");
                return;
            }
            //  패널 값이 세팅된 뒤 OnEnable이 패널을 빌드하도록 꺼 둔 채로 만들고 켠다(이름표와 같은 순서).
            var go = new GameObject("Sign");
            go.SetActive(false);
            go.transform.SetParent(fanRoot, false);
            go.transform.localPosition = new Vector3(0f, 2.0f, 0.1f);
            //  월드 패널은 +Z 쪽에서 보인다 — 사수가 보는 방향(관중석의 뒤쪽)을 향하게.
            go.transform.rotation = layout.Rotation * Quaternion.AngleAxis(180f, Vector3.up);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.worldSpaceSize = new Vector2(140f, 40f);   // 100px = 1m → 1.4m × 0.4m
            go.SetActive(true);

            var label = new Label(text);
            label.style.flexGrow = 1f;
            label.style.backgroundColor = new Color(1f, 0.99f, 0.96f);
            label.style.color = new Color(0.17f, 0.12f, 0.23f);
            label.style.fontSize = 22f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            document.rootVisualElement.Add(label);
        }

        private static string SignText(int sign, List<string> roster)
        {
            int k = roster.Count == 0 ? 1 : sign % roster.Count + 1;
            return SignTexts[sign % SignTexts.Length].Replace("{k}", k.ToString());
        }

        //  HUD 이름("{n}P")과 같은 순서 — 엔티티 id 서수.
        private List<string> Roster()
        {
            var roster = new List<string>();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>())
                {
                    roster.Add(entity.Id);
                }
            }
            roster.Sort(string.CompareOrdinal);
            return roster;
        }

        private void OnWorldEventBatch(WorldEventBatchToC msg)
        {
            if (director == null)
            {
                return;
            }
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);
                    director.OnBandHit(hit.points, Time.time);
                }
                else if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryRoundResult)
                {
                    var result = (ArcheryRoundResultEvent)WorldEventWire.FromWire(rec);
                    //  해설과 같은 분류 — 해설은 "접전"인데 관중은 "대역전"으로 반응하는 어긋남이 없게.
                    var line = narrator.LineFor(result, playerContext.entityId, out _, out _);
                    director.OnResult(line, result.multiplier >= 2, Time.time);
                }
            }
        }

        private void OnLanded(ArcheryArrowLanding landing)
        {
            if (director == null)
            {
                return;
            }
            switch (landing.Kind)
            {
                case ArcheryLandingKind.Ground:
                    director.OnMiss(Time.time);
                    break;
                case ArcheryLandingKind.Crowd:
                    int victim = ArcheryCrowdLayout.NearestFree(heads, director.HasArrow, landing.Point);
                    if (victim >= 0)
                    {
                        director.OnCrowdHit(victim, Time.time);
                        StickArrowOnHead(fans[victim].Root);
                    }
                    break;
                case ArcheryLandingKind.Target:
                    if (landing.SplitArrow)
                    {
                        director.OnRobinHood(Time.time);
                    }
                    break;
            }
        }

        //  머리에 비스듬히 박힌 화살 — 촉은 머리 속, 대는 사수 쪽으로 튀어나온다.
        private void StickArrowOnHead(Transform fanRoot)
        {
            if (arrowMesh == null)
            {
                arrowMesh = ArcheryArrowMesh.Build();
            }
            var go = new GameObject("HeadArrow");
            go.transform.SetParent(fanRoot, false);
            go.transform.localPosition = new Vector3(0.05f, 1.4f, 0f);
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(0.3f, -0.4f, -1f));
            go.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material, material, material };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var colors = new[] { new Color(0.92f, 0.78f, 0.52f), new Color(0.3f, 0.3f, 0.34f), new Color(1f, 0.25f, 0.3f) };
            for (int part = 0; part < colors.Length; part++)
            {
                block.Clear();
                block.SetColor("_BaseColor", colors[part]);
                renderer.SetPropertyBlock(block, part);
            }
        }
    }
}
