using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 몸 그리기(spec 2026-11-05 §11.3): 몸마다 <see cref="CardRushGag.Phase"/> → <see cref="CardRushGag.PoseAt"/>을 자식 <c>Model</c>에만 주고,
    /// 방패 구·대시 줄·망치·별·말풍선을 몸에 붙여 그린다. 뿌리 Transform은 시뮬·보간 몫이라 건드리지 않는다(<see cref="KnockbackFlightFxView"/>와 같은 규칙).
    /// 그림 틱은 내 몸 = 예측 시각의 한 틱 뒤(그려지는 자리), 남의 몸 = 보간 재생 시각. 모두 (상태, 그림 틱)의 함수라 시뮬 상태는 읽기만 한다.
    /// </summary>
    /// <remarks>틱·보간 뒤(IPostLateTickable)에서 칠한다 — 방패·망치·별이 이번 프레임 몸 자리를 따른다.</remarks>
    public class CardRushRunnerFxView : IStartable, IPostLateTickable, IDisposable
    {
        public readonly struct ModelRest
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public ModelRest(Transform model) { Position = model.localPosition; Rotation = model.localRotation; Scale = model.localScale; }
        }

        /// <summary>방패 — 노랑 반투명 구(반지름 0.9 m), 테두리만 진하게. 시작 0.06초(3틱) 커지며 나타나고 끝 0.1초(5틱) 옅어진다.</summary>
        public const float ShieldRadius = 0.9f;
        public const float ShieldCenterHeight = 0.85f;
        public const int ShieldGrowTicks = 3;
        public const int ShieldFadeTicks = 5;
        public const float ShieldFillAlpha = 0.16f;
        public const float ShieldRimAlpha = 0.85f;
        public static readonly Color ShieldColor = new Color(0.96f, 0.77f, 0.26f);

        /// <summary>
        /// 대시 — 발밑 뒤로 파란 줄 셋. 대시 시작 틱은 대시 끝(14틱째)에 상태에서 지워지므로 그 전에 다 옅어지게 12틱(리뷰 m2 — 15틱이면 끝 20%가 툭 꺼졌다).
        /// </summary>
        public const int StreakTicks = CardRushRules.DashTicks - 2;
        public const float StreakLength = 1.1f;
        public static readonly Color StreakColor = new Color(0.24f, 0.53f, 0.91f);

        /// <summary>말풍선 — 머리 위 0.8초(40틱) 떠오르며 옅어진다. 남의 것은 ±3레인만, 작게.</summary>
        public const int BubbleTicks = 40;
        public const float BubbleRise = 0.4f;
        public const float OtherBubbleSize = 0.7f;
        public static readonly Color GoodBubble = new Color(1f, 0.82f, 0.63f);    //  #ffd0a0 — 와장창·막음·버팀
        public static readonly Color BadBubble = new Color(1f, 0.71f, 0.71f);     //  #ffb4b4 — 꽈당·으악·헛방

        private sealed class Entry
        {
            public string Id;
            public Transform Visual;
            public Transform Model;
            public Transform Head;
            public ModelRest Rest;
            public bool Posed;
            public CardRushGagFx Fx;
            public CardRushRunnerProps Props;
        }

        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actors;
        private readonly IPlayerContext playerContext;
        private readonly EntityRenderClock renderClock;
        private readonly CameraController cameraController;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> gone = new List<string>();
        private CardRushGagFxMaterials materials;
        private GameObject root;

        public CardRushRunnerFxView(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry, ActorRegistry actors,
                                    IPlayerContext playerContext, EntityRenderClock renderClock, CameraController cameraController)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.actors = actors;
            this.playerContext = playerContext;
            this.renderClock = renderClock;
            this.cameraController = cameraController;
        }

        public void Start()
        {
            materials = new CardRushGagFxMaterials();
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
            var myRunner = string.IsNullOrEmpty(me) ? null : entityRegistry.Get(me)?.Get<CardRushRunner>();
            var cam = cameraController != null && cameraController.MainCamera != null ? cameraController.MainCamera : Camera.main;
            var tuning = world.Tuning;
            var track = world.Track;
            foreach (var actor in actors.Values)
            {
                var visual = actor.visualGameObject;
                var runner = entityRegistry.Get(actor.entityId)?.Get<CardRushRunner>();
                if (visual == null || runner == null)
                {
                    continue;
                }

                var state = runner.State;
                double d = DrawnTick(renderClock, actor.entityId, me);
                var (kind, t01) = CardRushGag.Phase(state, d, tuning, track);
                var props = CardRushRunnerProps.Look(state, d);

                if (entries.TryGetValue(actor.entityId, out var entry) == false || entry.Visual != visual.transform)
                {
                    if (kind == CardRushGagKind.None && props.Any == false)
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
                    var pose = CardRushGag.PoseAt(kind, t01);
                    ApplyPose(entry.Visual, entry.Model, entry.Rest, pose, rules.Forward);
                    entry.Posed = kind != CardRushGagKind.None;
                }
                entry.Fx.Show(kind, t01, Crown(entry));

                bool mine = actor.entityId == me;
                bool near = mine || myRunner == null
                            || CardRushHeadCardView.LaneGap(runner.LaneOrigin, myRunner.LaneOrigin, rules.Forward, rules.LaneSpacing) <= CardRushHeadCardView.OtherLaneRange;
                //  소품은 자세를 준 뒤에 놓는다 — 망치·말풍선이 이번 프레임 머리 자리를 따른다.
                entry.Props.Place(props, entry.Visual.position, Crown(entry), rules.Forward, cam, mine, near);
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
                entries[id].Props.Destroy();
                entries.Remove(id);
            }
        }

        private Entry Dress(string id, Transform visual, Entry old)
        {
            old?.Fx.Destroy();
            old?.Props.Destroy();
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
                Props = new CardRushRunnerProps(root.transform, "Props " + id, materials),
            };
            entries[id] = entry;
            return entry;
        }

        /// <summary>
        /// 몸이 그려지는 틱(연속값) — 내 몸 그림은 예측 시각의 한 틱 뒤에 서 있고(KnockbackFlightFxView와 같은 식), 남은 보간 재생 시각.
        /// 몸개그·장애물·구역이 모두 이 하나를 쓴다 — 기준이 다르면 굴러오는 것과 꽈당이 어긋난다.
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

        /// <summary>정수리 — 몸 프리팹의 정수리 자리(<see cref="ChibiHeadSpots.Crown"/>). 별·망치·말풍선이 이 위아래에 선다.</summary>
        private static Vector3 Crown(Entry e) => ChibiHeadSpots.Crown(e.Model, e.Head);

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
                entry.Props.Destroy();
            }
            entries.Clear();
            CardRushGagFx.DestroyObject(root);
            root = null;
            materials?.Dispose();
            materials = null;
        }
    }

    /// <summary>몸 소품의 그림 틱 모습 — 방패·대시 줄·망치·말풍선. 상태와 그림 틱만으로 정한다.</summary>
    public readonly struct CardRushPropsLook
    {
        public readonly float Shield01;     //  방패 진행(0 → 1). 음수 = 없음
        public readonly float ShieldAlpha;  //  나타남·옅어짐을 곱한 몫(0~1)
        public readonly float ShieldScale;
        public readonly float Streak01;     //  대시 줄 옅어짐(0 → 1). 음수 = 없음
        public readonly float Hammer01;     //  망치 내리침(0 → 1). 음수 = 없음
        public readonly string Bubble;      //  말풍선 글. null = 없음
        public readonly bool BubbleGood;
        public readonly float Bubble01;

        public CardRushPropsLook(float shield01, float shieldAlpha, float shieldScale, float streak01, float hammer01, string bubble, bool bubbleGood, float bubble01)
        {
            Shield01 = shield01; ShieldAlpha = shieldAlpha; ShieldScale = shieldScale; Streak01 = streak01; Hammer01 = hammer01;
            Bubble = bubble; BubbleGood = bubbleGood; Bubble01 = bubble01;
        }

        public bool Any => Shield01 >= 0f || Streak01 >= 0f || Hammer01 >= 0f || Bubble != null;
    }

    /// <summary>방패 구·대시 줄·망치·말풍선(spec §11.3). 효과마다 한 색 계열 — 방패 노랑, 대시 파랑, 망치 나무·쇠.</summary>
    public sealed class CardRushRunnerProps
    {
        private readonly Transform root;
        private readonly Renderer shieldFill;
        private readonly LineRenderer shieldRim;
        private readonly Renderer[] streaks = new Renderer[3];
        private readonly Transform hammer;
        private readonly CardRushWorldLabel bubble;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private const int RimSegments = 40;

        public CardRushRunnerProps(Transform parent, string name, CardRushGagFxMaterials materials)
        {
            root = CardRushDraw.Node(name, parent).transform;
            shieldFill = CardRushDraw.Primitive(PrimitiveType.Sphere, "ShieldFill", root, materials.Clear).GetComponent<Renderer>();
            var rimGo = CardRushDraw.Node("ShieldRim", root);
            shieldRim = rimGo.AddComponent<LineRenderer>();
            shieldRim.useWorldSpace = false;
            shieldRim.loop = true;
            shieldRim.sharedMaterial = materials.Clear;
            shieldRim.widthMultiplier = 0.06f;
            shieldRim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shieldRim.receiveShadows = false;
            shieldRim.positionCount = RimSegments;
            for (int i = 0; i < RimSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RimSegments;
                shieldRim.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * CardRushRunnerFxView.ShieldRadius);
            }
            for (int i = 0; i < streaks.Length; i++)
            {
                streaks[i] = CardRushDraw.Box("Streak", root, Vector3.zero, new Vector3(0.06f, 0.01f, CardRushRunnerFxView.StreakLength), materials.Clear).GetComponent<Renderer>();
            }
            hammer = CardRushDraw.Node("Hammer", root).transform;
            var handle = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Handle", hammer, materials.Handle);
            handle.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            handle.transform.localScale = new Vector3(0.07f, 0.32f, 0.07f);
            CardRushDraw.Box("Head", hammer, new Vector3(0f, 0.68f, 0f), new Vector3(0.22f, 0.2f, 0.42f), materials.HammerHead);
            bubble = CardRushWorldLabel.Create("Bubble", root);
            SetAll(false);
        }

        /// <summary>그 그림 틱의 소품 모습.</summary>
        public static CardRushPropsLook Look(in CardRushRunnerState s, double d)
        {
            float shield01 = -1f, shieldAlpha = 0f, shieldScale = 1f;
            if (s.BlockEndTick != 0)
            {
                double start = s.BlockEndTick - CardRushRules.BlockTicks;
                if (d >= start && d < s.BlockEndTick)
                {
                    shield01 = (float)((d - start) / CardRushRules.BlockTicks);
                    float grow = Mathf.Clamp01((float)((d - start) / CardRushRunnerFxView.ShieldGrowTicks));
                    float fade = Mathf.Clamp01((float)((s.BlockEndTick - d) / CardRushRunnerFxView.ShieldFadeTicks));
                    shieldScale = Mathf.Lerp(0.3f, 1f, grow);
                    shieldAlpha = fade;
                }
            }

            float streak = -1f;
            if (s.DashStartTick != 0 && d >= s.DashStartTick && d < s.DashStartTick + CardRushRunnerFxView.StreakTicks)
            {
                streak = (float)((d - s.DashStartTick) / CardRushRunnerFxView.StreakTicks);
            }

            float hammer01 = -1f;
            long swing = CardRushGag.SwingTick(s);
            if (swing != 0 && d >= swing && d < swing + CardRushGag.SmashTicks)
            {
                hammer01 = (float)((d - swing) / CardRushGag.SmashTicks);
            }

            //  말풍선 — 장애물 결과·구역 결과·헛방 중 늦은 것 하나.
            string text = null;
            bool good = false;
            float bubble01 = 0f;
            long at = 0;
            string obstacleText = ObstacleBubble((CardRushObstacleEvent)s.ObstacleEvent, out bool obstacleGood);
            if (obstacleText != null && In(d, s.ObstacleEventTick))
            {
                text = obstacleText; good = obstacleGood; at = s.ObstacleEventTick;
            }
            string zoneText = ZoneBubble((CardRushZoneEvent)s.ZoneEvent, out bool zoneGood);
            if (zoneText != null && In(d, s.ZoneEventTick) && s.ZoneEventTick >= at)
            {
                text = zoneText; good = zoneGood; at = s.ZoneEventTick;
            }
            if (In(d, s.WhiffTick) && s.WhiffTick >= at)
            {
                text = "헛방"; good = false; at = s.WhiffTick;
            }
            if (text != null) bubble01 = (float)((d - at) / CardRushRunnerFxView.BubbleTicks);

            return new CardRushPropsLook(shield01, shieldAlpha, shieldScale, streak, hammer01, text, good, bubble01);
        }

        private static bool In(double d, long at) => at != 0 && d >= at && d < at + CardRushRunnerFxView.BubbleTicks;

        public static string ObstacleBubble(CardRushObstacleEvent ev, out bool good)
        {
            good = ev == CardRushObstacleEvent.Smashed || ev == CardRushObstacleEvent.Blocked;
            return ev switch
            {
                CardRushObstacleEvent.Smashed => "와장창!",
                CardRushObstacleEvent.Blocked => "막음!",
                CardRushObstacleEvent.Tumbled => "꽈당",
                _ => null,
            };
        }

        public static string ZoneBubble(CardRushZoneEvent ev, out bool good)
        {
            good = ev == CardRushZoneEvent.Held;
            return ev switch
            {
                CardRushZoneEvent.Held => "버팀!",
                CardRushZoneEvent.Hit => "으악",
                _ => null,
            };
        }

        public void Place(in CardRushPropsLook look, Vector3 body, Vector3 crown, Vector3 forward, Camera cam, bool mine, bool near)
        {
            var f = CardRushLanes.Flat(forward);
            var right = Vector3.Cross(Vector3.up, f);

            bool shield = look.Shield01 >= 0f;
            CardRushDraw.SetActive(shieldFill.gameObject, shield);
            CardRushDraw.SetActive(shieldRim.gameObject, shield);
            if (shield)
            {
                var center = body + Vector3.up * CardRushRunnerFxView.ShieldCenterHeight;
                float d = CardRushRunnerFxView.ShieldRadius * 2f * look.ShieldScale;
                shieldFill.transform.SetPositionAndRotation(center, Quaternion.identity);
                shieldFill.transform.localScale = new Vector3(d, d, d);
                CardRushDraw.Tint(shieldFill, block, CardRushRunnerFxView.ShieldColor, CardRushRunnerFxView.ShieldFillAlpha * look.ShieldAlpha);
                //  테두리는 카메라를 보는 원 — 구의 윤곽선 자리에 진하게.
                var facing = cam != null ? Quaternion.LookRotation(center - cam.transform.position, Vector3.up) : Quaternion.LookRotation(f, Vector3.up);
                shieldRim.transform.SetPositionAndRotation(center, facing);
                shieldRim.transform.localScale = Vector3.one * look.ShieldScale;
                //  무광 셰이더는 꼭짓점 색을 안 쓴다 — 색·알파는 렌더러 블록으로.
                CardRushDraw.Tint(shieldRim, block, CardRushRunnerFxView.ShieldColor, CardRushRunnerFxView.ShieldRimAlpha * look.ShieldAlpha);
            }

            bool dash = look.Streak01 >= 0f;
            for (int i = 0; i < streaks.Length; i++)
            {
                CardRushDraw.SetActive(streaks[i].gameObject, dash);
                if (dash == false) continue;
                float x = (i - 1) * 0.24f;
                //  발밑 뒤로 — 가운데 줄이 조금 길게 뒤로.
                var at = body + right * x + Vector3.up * 0.04f - f * (CardRushRunnerFxView.StreakLength * 0.5f + 0.2f + (i == 1 ? 0.15f : 0f));
                streaks[i].transform.SetPositionAndRotation(at, Quaternion.LookRotation(f, Vector3.up));
                CardRushDraw.Tint(streaks[i], block, CardRushRunnerFxView.StreakColor, 0.85f * (1f - look.Streak01));
            }

            bool swing = look.Hammer01 >= 0f;
            CardRushDraw.SetActive(hammer.gameObject, swing);
            if (swing)
            {
                //  머리 오른쪽 위에서 들었다가(뒤로 70°) 앞으로 내리친다(앞으로 80°) — 앞 35%에 내리치고 끝 20%에 작아진다.
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(look.Hammer01 / 0.35f));
                float angle = Mathf.Lerp(-70f, 80f, u);
                //  옛 치비의 "머리 뼈 + 0.35"를 정수리 기준(정수리 − 0.43)으로 옮겼다.
                hammer.position = crown - Vector3.up * 0.43f + right * 0.35f;
                hammer.rotation = Quaternion.AngleAxis(angle, right) * Quaternion.LookRotation(f, Vector3.up);
                hammer.localScale = Vector3.one * Mathf.Clamp01((1f - look.Hammer01) / 0.2f);
            }

            if (bubble != null)
            {
                if (look.Bubble != null && near && cam != null)
                {
                    //  옛 치비의 "머리 뼈 + 2.1/1.5"를 정수리 기준(정수리 + 1.32/0.72)으로 옮겼다 — 머리 위 카드 위.
                    float lift = (mine ? 1.32f : 0.72f) + CardRushRunnerFxView.BubbleRise * look.Bubble01;
                    var color = look.BubbleGood ? CardRushRunnerFxView.GoodBubble : CardRushRunnerFxView.BadBubble;
                    bubble.Show(look.Bubble, crown + Vector3.up * lift, color, 1f - look.Bubble01 * look.Bubble01, mine ? 1.1f : CardRushRunnerFxView.OtherBubbleSize, cam);
                }
                else
                {
                    bubble.Hide();
                }
            }
        }

        private void SetAll(bool on)
        {
            CardRushDraw.SetActive(shieldFill.gameObject, on);
            CardRushDraw.SetActive(shieldRim.gameObject, on);
            foreach (var s in streaks) CardRushDraw.SetActive(s.gameObject, on);
            CardRushDraw.SetActive(hammer.gameObject, on);
        }

        /// <summary>지금 켜진 소품 — 시험·진단용.</summary>
        public bool ShieldVisible => shieldFill.gameObject.activeSelf;
        public bool StreaksVisible => streaks[0].gameObject.activeSelf;
        public bool HammerVisible => hammer.gameObject.activeSelf;

        public void Destroy()
        {
            bubble?.Destroy();
            CardRushGagFx.DestroyObject(root != null ? root.gameObject : null);
        }
    }

    /// <summary>별(노랑 한 계열)·방패·대시 줄·망치 재질. 효과 하나에 한 색 계열만 — 번쩍임 없음.</summary>
    public sealed class CardRushGagFxMaterials : IDisposable
    {
        public static readonly Color Star = new Color(1f, 0.84f, 0.29f);

        public Material StarMaterial { get; }
        public Mesh StarMesh { get; }
        /// <summary>반투명 — 색·알파는 렌더러마다 준다(방패·대시 줄).</summary>
        public Material Clear { get; }
        public Material Handle { get; }
        public Material HammerHead { get; }

        public CardRushGagFxMaterials()
        {
            StarMaterial = CardRushDraw.Opaque("CardRush_Star", Star);
            StarMesh = MakeStar(0.5f, 0.22f);
            Clear = CardRushDraw.Transparent("CardRush_FxClear", Color.white);
            Handle = CardRushDraw.Opaque("CardRush_HammerHandle", CardRushStageMaterials.WoodLight);
            HammerHead = CardRushDraw.Opaque("CardRush_HammerHead", CardRushStageMaterials.Iron);
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
            CardRushGagFx.DestroyObject(StarMaterial);
            CardRushGagFx.DestroyObject(StarMesh);
            CardRushGagFx.DestroyObject(Clear);
            CardRushGagFx.DestroyObject(Handle);
            CardRushGagFx.DestroyObject(HammerHead);
        }
    }

    /// <summary>
    /// 몸 하나의 별 셋 — 꽈당 엉덩방아(0.6~0.85)·으악 납작(0.1~0.7) 동안 머리 위를 돈다. 저장하는 시간이 없다 — 매번 (몸개그, t01)에서
    /// 자리를 다시 계산해 기절 끝 틱에 같이 꺼진다.
    /// </summary>
    public sealed class CardRushGagFx
    {
        public const int StarCount = 3;
        private const float StarTurns = 2f, StarRadius = 0.42f, StarBelowCrown = 0.40f, StarSize = 0.7f;

        private readonly GameObject root;
        private readonly Transform[] stars = new Transform[StarCount];
        private bool visible = true;

        public CardRushGagFx(Transform parent, string name, CardRushGagFxMaterials materials)
        {
            root = new GameObject(name);
            root.transform.SetParent(parent, false);
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

        /// <summary>별이 도는 구간(몸개그 안 비율). 없으면 false.</summary>
        public static bool StarSpan(CardRushGagKind kind, out float from, out float to)
        {
            switch (kind)
            {
                case CardRushGagKind.Tumble: from = CardRushGag.TumbleRollEnd; to = CardRushGag.TumbleSitEnd; return true;
                case CardRushGagKind.Squash: from = CardRushGag.SquashPressEnd; to = CardRushGag.SquashFlatEnd; return true;
                default: from = to = 0f; return false;
            }
        }

        //  별은 머리 둘레를 돈다 — 옛 치비의 "머리 뼈 + 0.38"을 정수리 기준(정수리 − 0.40)으로 옮겼다.
        public void Show(CardRushGagKind kind, float t01, Vector3 crown)
        {
            if (StarSpan(kind, out float from, out float to) == false || t01 < from || t01 >= to)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);
            float w = (t01 - from) / (to - from);
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

        private void SetVisible(bool on)
        {
            if (visible == on || root == null)
            {
                return;
            }
            visible = on;
            root.SetActive(on);
        }

        /// <summary>지금 켜진 별 수 — 시험·진단용.</summary>
        public int ActiveStars => visible ? StarCount : 0;

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
