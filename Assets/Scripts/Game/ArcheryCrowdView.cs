using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 관중석 — 사대 양옆에 코드 인형 관중을 세우고, 경기에 반응하게 한다.
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
        private readonly ArcheryShootOffNarrator narrator = new ArcheryShootOffNarrator();

        private ArcheryCrowdDriver driver;
        private System.IDisposable subscription;
        private GameObject root;
        private ArcheryCrowdLayout layout;
        private ArcheryCrowdDirector director;
        private Mesh arrowMesh;
        private readonly List<Fan> fans = new List<Fan>();
        private readonly List<Vector3> heads = new List<Vector3>();
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        private bool hasIndex;
        private int lastIndex;

        private sealed class Fan
        {
            public Transform Root;
            public Transform LeftArm;
            public Transform RightArm;
            public Renderer Body;
            public Color Shirt;
            public bool Gray;
        }

        public ArcheryCrowdView(ArcheryCourse course, ArcheryWorld world, GameFramework.Runner.IRunner runner,
                                ISubscriber<WorldEventBatchToC> batchSubscriber, ArcheryArrowLandings landings,
                                IPlayerContext playerContext)
        {
            this.course = course;
            this.world = world;
            this.runner = runner;
            this.batchSubscriber = batchSubscriber;
            this.landings = landings;
            this.playerContext = playerContext;
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
            foreach (var mat in materials.Values)
            {
                Object.Destroy(mat);
            }
            materials.Clear();
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

            //  마지막 라운드는 결과가 나올 때까지만 조용하다.
            bool hush = inRound && course.MultiplierAt(index) >= 2 && tick < course.RoundCloseTick(index, start);
            director.BaseMood = hush ? ArcheryCrowdMood.Hush : ArcheryCrowdMood.Idle;

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
            fan.Root.position = seat.Position + Vector3.up * (pose.Lift * seat.Scale) + (seat.Facing * Vector3.right) * pose.Sway;
            fan.LeftArm.localRotation = Quaternion.AngleAxis(-pose.LeftArmDegrees, Vector3.forward);
            fan.RightArm.localRotation = Quaternion.AngleAxis(pose.RightArmDegrees, Vector3.forward);
            if (pose.Gray != fan.Gray)
            {
                fan.Gray = pose.Gray;
                fan.Body.sharedMaterial = MaterialFor(pose.Gray ? ArcheryCrowdLayout.BooGray : fan.Shirt);
            }
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
            root = new GameObject("ArcheryCrowd");

            foreach (var step in layout.Steps)
            {
                var box = Part(PrimitiveType.Cube, root.transform, step.Size, new Color(0.55f, 0.47f, 0.4f), keepCollider: true);
                box.SetPositionAndRotation(step.Center, step.Rotation);
                var collider = box.GetComponent<Collider>();
                colliders.Add(collider);
                landings.AddCrowdCollider(collider);
            }
            //  관중 키 높이의 안 보이는 상자 — 단 위로 날아온 화살도 관중석에 꽂힌 것으로 본다.
            foreach (var volume in layout.Volumes)
            {
                var go = new GameObject("CrowdVolume");
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(volume.Center, volume.Rotation);
                var collider = go.AddComponent<BoxCollider>();
                collider.size = volume.Size;
                colliders.Add(collider);
                landings.AddCrowdCollider(collider);
            }

            for (int i = 0; i < layout.Seats.Count; i++)
            {
                fans.Add(BuildFan(layout.Seats[i], lane.Value.ShooterPosition));
                heads.Add(layout.Seats[i].Position + Vector3.up * (1.3f * layout.Seats[i].Scale));
            }
            return true;
        }

        private Fan BuildFan(ArcheryCrowdSeat seat, Vector3 shooterPosition)
        {
            var fanRoot = new GameObject("Fan").transform;
            fanRoot.SetParent(root.transform, false);
            fanRoot.SetPositionAndRotation(seat.Position, seat.Facing);
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

            if (seat.Sign >= 0)
            {
                Vector3 signWorldPosition = fanRoot.TransformPoint(0f, 2.0f, 0.1f);
                BuildSign(fanRoot, signWorldPosition, shooterPosition, SignText(seat.Sign, course.RosterCount));
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
                //  관중 몸에 화살이 걸리면 땅으로 읽힌다 — 상자만 받는다. Destroy는 프레임 끝이라 먼저 끈다.
                var bodyCollider = go.GetComponent<Collider>();
                bodyCollider.enabled = false;
                Object.Destroy(bodyCollider);
            }
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        //  색깔당 재질 하나를 재사용한다 — SRP 배처가 묶어 그리도록 MaterialPropertyBlock 대신 sharedMaterial을 쓴다.
        private Material MaterialFor(Color color)
        {
            if (materials.TryGetValue(color, out var mat) == false)
            {
                mat = LOPToonMaterials.Create(color);
                materials[color] = mat;
            }
            return mat;
        }

        private void BuildSign(Transform fanRoot, Vector3 signWorldPosition, Vector3 shooterPosition, string text)
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
            //  월드 패널은 +Z가 보는 사람 반대쪽일 때 읽힌다 — +Z를 사수 자리 → 팻말 수평 방향으로.
            Vector3 away = signWorldPosition - shooterPosition;
            away.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(away.normalized);
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

        //  팻말 번호는 판 시작 때 정한 참가자 수만큼 돌려 쓴다(HUD 이름 "{n}P"와 같은 번호).
        private static string SignText(int sign, int rosterCount)
        {
            int k = rosterCount <= 0 ? 1 : sign % rosterCount + 1;
            return SignTexts[sign % SignTexts.Length].Replace("{k}", k.ToString());
        }

        private void OnWorldEventBatch(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    if (director == null)
                    {
                        continue;
                    }
                    var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);
                    director.OnBandHit(hit.points, Time.time);
                }
                else if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryRoundResult)
                {
                    var result = (ArcheryRoundResultEvent)WorldEventWire.FromWire(rec);
                    //  해설과 같은 분류 — 해설은 "접전"인데 관중은 "대역전"으로 반응하는 어긋남이 없게.
                    //  narrator는 라운드를 건너 연속 기록을 세므로, 관중석이 아직 없어도 항상 불러야 한다.
                    var line = narrator.LineFor(result, playerContext.entityId, out _, out _);
                    if (director != null)
                    {
                        director.OnResult(line, result.multiplier >= 2, Time.time);
                    }
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
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var colors = new[] { new Color(0.92f, 0.78f, 0.52f), new Color(0.3f, 0.3f, 0.34f), new Color(1f, 0.25f, 0.3f) };
            renderer.sharedMaterials = new[] { MaterialFor(colors[0]), MaterialFor(colors[1]), MaterialFor(colors[2]) };
        }
    }
}
