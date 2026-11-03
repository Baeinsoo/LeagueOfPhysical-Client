using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 칸 그림의 자리와 움직임을 정하는 순수 식(spec §4·§7.4). 실행 중 그림(<see cref="CardRushCourseView"/>)과
    /// 에디터 미리보기가 같은 식을 써서, 미리보기에서 본 배치가 판에서 그대로 나온다.
    /// </summary>
    public static class CardRushCourseLayout
    {
        /// <summary>문짝이 확 열리는 시간 — 이동 0.3초 중 앞 0.1초.</summary>
        public const float DoorOpenSeconds = 0.1f;
        public const float DoorOpenDegrees = 100f;

        /// <summary>지나간 칸은 이만큼 바랜다.</summary>
        public const float PassedFade = 0.3f;

        /// <summary>
        /// 문이 따라가는 몸보다 이만큼(칸) 뒤로 가면 가라앉기 시작해 <see cref="DoorHiddenBehindSteps"/>에서 사라진다.
        /// 카메라가 몸 뒤 약 1칸 위에 서서, 막 지나온 문(몸 뒤 0.5칸)이 화면 아래를 덮는다 — 몸이 다음 자리에 서면 다 사라지게 0.5.
        /// </summary>
        public const float DoorSinkBehindSteps = 0.1f;
        public const float DoorHiddenBehindSteps = 0.5f;

        /// <summary>뒤집힌 돌 아랫면 — 같은 색을 이만큼 어둡게(젖은 바닥).</summary>
        public const float UndersideDarken = 0.45f;

        /// <summary>
        /// 칸 k의 장애물(돌·문) 자리. 돌다리는 칸을 맞히면 그 돌 위에 서므로 자리 k+1, 문은 두 자리 사이에 서서
        /// 지나가므로 자리 k+0.5다. 출발점은 자리 0.
        /// </summary>
        public static Vector3 ObstacleAt(CardRushTheme theme, Vector3 laneOrigin, Vector3 forward, float stepSpacing, int step)
            => CardRushLanes.PositionAt(laneOrigin, forward, stepSpacing, step + ObstacleOffset(theme));

        public static float ObstacleOffset(CardRushTheme theme) => theme == CardRushTheme.DoorDash ? 0.5f : 1f;

        /// <summary>
        /// 그림 틱에 칸 k를 이미 건넜나. 맞힌 칸이라도 그 칸으로 가는 이동이 그림 틱에 아직 안 끝났으면 아니다 —
        /// 남의 상태는 최신 스냅이라 그려지는 몸보다 앞서 있다(그림 틱이 이동 시작 전이어도 아직 안 건넌 것으로 본다).
        /// </summary>
        /// <summary>
        /// 그림 틱의 칸 단위 진행. 남의 상태는 최신 스냅이라 그려지는 몸보다 앞선다 — 그림 틱이 이동 시작 전이면 아직
        /// 앞 칸에 서 있다(<see cref="CardRushLanes.Progress"/>는 그때 새 칸을 돌려줘 한 칸 앞섰다가 되돌아간다, 최종 리뷰 M1).
        /// </summary>
        public static float DrawnProgress(in CardRushRunnerState s, long drawnTick, int moveTicks)
        {
            if (s.MoveStartTick != 0 && drawnTick < s.MoveStartTick)
            {
                return System.Math.Max(0, s.Step - 1);
            }
            return CardRushLanes.Progress(s, drawnTick, moveTicks);
        }

        public static bool IsPassed(in CardRushRunnerState s, long drawnTick, int moveTicks, int step)
        {
            if (step >= s.Step)
            {
                return false;
            }
            if (step < s.Step - 1)
            {
                return true;
            }
            //  마지막으로 맞힌 칸 — 그 이동이 끝났어야 건넌 것이다.
            return s.MoveStartTick == 0 || drawnTick >= s.MoveStartTick + moveTicks;
        }

        /// <summary>
        /// 문 칸 k의 문짝 열림 각(도). 던진 카드가 문에 꽂히는 틱(맞힌 틱 + <see cref="CardRushThrowFx.FlightTicks"/>)부터
        /// 0.1초에 0 → 100°, 건넌 문은 열린 채, 아직 안 맞힌 문은 닫힘. 그림만이다 — 몸은 맞힌 틱부터 시뮬대로 간다.
        /// </summary>
        public static float DoorOpenAngle(in CardRushRunnerState s, long drawnTick, int moveTicks, int step)
        {
            if (step >= s.Step)
            {
                return 0f;
            }
            if (step < s.Step - 1 || s.MoveStartTick == 0)
            {
                return DoorOpenDegrees;
            }
            int openTicks = CardRushTiming.Ticks(DoorOpenSeconds);
            float t = Mathf.Clamp01((float)(drawnTick - (s.MoveStartTick + CardRushThrowFx.FlightTicks)) / openTicks);
            return DoorOpenDegrees * t;
        }

        /// <summary>
        /// 문 칸 k가 얼마나 서 있나(1 = 그대로, 0 = 숨김). 카메라가 따라가는 몸의 그림 진행(<paramref name="followedProgress"/>,
        /// 칸 단위)에서 문 자리(k+0.5)가 얼마나 뒤인지로 정한다 — 모든 레인이 같은 줄에 있어 같은 기준을 쓴다.
        /// 따라갈 몸이 없으면(NaN) 다 보인다. 그림만이다: 판정은 문과 무관하다.
        /// </summary>
        public static float DoorVisibility(int step, float followedProgress)
        {
            if (float.IsNaN(followedProgress))
            {
                return 1f;
            }
            float behind = followedProgress - (step + ObstacleOffset(CardRushTheme.DoorDash));
            return 1f - Mathf.Clamp01((behind - DoorSinkBehindSteps) / (DoorHiddenBehindSteps - DoorSinkBehindSteps));
        }

        /// <summary>그림 틱이 틀림 멈춤 안이면 그 진행(0~1).</summary>
        public static bool TryStun(in CardRushRunnerState s, long drawnTick, int stunTicks, out float t01)
        {
            t01 = 0f;
            if (s.StunEndTick == 0)
            {
                return false;
            }
            long start = s.StunEndTick - stunTicks;
            if (drawnTick < start || drawnTick >= s.StunEndTick)
            {
                return false;
            }
            t01 = (float)(drawnTick - start) / stunTicks;
            return true;
        }

        /// <summary>
        /// 틀림 멈춤 중 다음 돌의 뒤집힘 각(도). 몸이 반쯤 뛰어오르는 동안 뒤집혀(0.15~0.30) 몸이 물에 빠지고,
        /// 몸이 제 돌로 기어오르는 끝 무렵(0.75~0.95) 제자리로 돌아온다. 0과 1에서 0이라 멈춤이 끝나면 원래 돌이다.
        /// </summary>
        public static float StoneFlipAngle(float t01)
        {
            if (t01 <= 0f || t01 >= 1f)
            {
                return 0f;
            }
            return 180f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.30f, t01))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 0.95f, t01)));
        }

        /// <summary>
        /// 틀림 멈춤 중 문짝 출렁임(도). 얼굴을 박는 0.15부터 앞으로 밀렸다 잦아드는 흔들림. 0과 1에서 0.
        /// </summary>
        public static float DoorWobbleAngle(float t01)
        {
            const float slam = 0.15f;
            if (t01 <= slam || t01 >= 1f)
            {
                return 0f;
            }
            float u = (t01 - slam) / (1f - slam);
            return 14f * Mathf.Exp(-4f * u) * Mathf.Sin(u * Mathf.PI * 6f) * (1f - u);
        }
    }

    /// <summary>칸 그림이 함께 쓰는 재질·모양 그물. 색마다 재질 하나(바랜 것 하나 더)를 모든 칸이 공유한다.</summary>
    public sealed class CardRushCoursePalette : IDisposable
    {
        public static readonly Color Red = Hex("#E5484D");
        public static readonly Color Yellow = Hex("#F5C542");
        public static readonly Color Blue = Hex("#3B82F6");

        private readonly Material[] colors = new Material[CardColors.Count];
        private readonly Material[] faded = new Material[CardColors.Count];
        private readonly Material[] underside = new Material[CardColors.Count];
        private readonly Mesh[] shapes = new Mesh[CardColors.Count];
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        public Material Decal { get; }
        public Material Frame { get; }
        public Material StartPad { get; }
        public Material FinishPad { get; }

        public CardRushCoursePalette()
        {
            for (int i = 0; i < CardColors.Count; i++)
            {
                var c = ColorOf((CardColor)i);
                colors[i] = Make("CardRush_" + (CardColor)i, c);
                //  바램 = 흰빛 쪽으로 30% — 색은 아직 알아보되 "끝난 칸"으로 읽힌다.
                faded[i] = Make("CardRush_" + (CardColor)i + "_Faded", Color.Lerp(c, new Color(0.85f, 0.85f, 0.85f), CardRushCourseLayout.PassedFade));
                underside[i] = Make("CardRush_" + (CardColor)i + "_Under", Color.Lerp(c, Color.black, CardRushCourseLayout.UndersideDarken));
                shapes[i] = MakeShape((CardColor)i);
            }
            Decal = Make("CardRush_Decal", Color.white);
            Frame = Make("CardRush_Frame", Hex("#6B5A4A"));
            StartPad = Make("CardRush_Start", Hex("#C9C2B8"));
            FinishPad = Make("CardRush_Finish", Hex("#F2E3B3"));
        }

        public static Color ColorOf(CardColor color) => color switch
        {
            CardColor.Red => Red,
            CardColor.Yellow => Yellow,
            _ => Blue,
        };

        public Material Of(CardColor color, bool passed) => passed ? faded[(int)color] : colors[(int)color];

        /// <summary>돌 아랫면 — 뒤집히면 이 어두운 면이 위로 와서 "뒤집힘"으로 읽힌다(같은 색 계열).</summary>
        public Material UndersideOf(CardColor color) => underside[(int)color];

        /// <summary>지름 1 원판 — 빨강 모양(●)이 곧 원이라 같은 그물을 쓴다.</summary>
        public Mesh Disc => shapes[(int)CardColor.Red];

        /// <summary>색마다 모양(빨강 ●, 노랑 ▲, 파랑 ■) — 색을 못 가리는 사람도 모양으로 읽는다(HUD 카드와 같은 짝).</summary>
        public Mesh ShapeOf(CardColor color) => shapes[(int)color];

        private Material Make(string name, Color color)
        {
            var shader = Shader.Find("LOP/Toon");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            //  가장자리 빛은 캐릭터용 — 비스듬히 보이는 돌·문이 회색으로 뜨지 않게 끈다(LobbyTunnel과 같은 이유).
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0f);
            owned.Add(m);
            return m;
        }

        //  -z를 보는 평면 모양(지름 1). 앞뒤 면을 다 넣어 어느 쪽에서 봐도 보인다.
        private Mesh MakeShape(CardColor color)
        {
            var verts = new List<Vector3>();
            switch (color)
            {
                case CardColor.Red:
                    for (int i = 0; i < 32; i++)
                    {
                        float a = i * Mathf.PI * 2f / 32f;
                        verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f);
                    }
                    break;
                case CardColor.Yellow:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                        verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a) - 0.1f, 0f) * 0.55f);
                    }
                    break;
                default:
                    verts.Add(new Vector3(-0.4f, -0.4f, 0f));
                    verts.Add(new Vector3(0.4f, -0.4f, 0f));
                    verts.Add(new Vector3(0.4f, 0.4f, 0f));
                    verts.Add(new Vector3(-0.4f, 0.4f, 0f));
                    break;
            }

            //  가운데 점에서 부채꼴로 잇는다. 앞뒤 면은 점을 따로 둔다 — 점을 같이 쓰면 법선이 서로 지워져 검게 그려진다.
            int n = verts.Count;
            var all = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = all.Count;
                var normal = side == 0 ? Vector3.back : Vector3.forward;
                all.Add(Vector3.zero);
                normals.Add(normal);
                foreach (var v in verts)
                {
                    all.Add(v);
                    normals.Add(normal);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = start + 1 + i;
                    int b = start + 1 + (i + 1) % n;
                    //  유니티 앞면 = 보는 쪽에서 시계 방향. -z 쪽 면은 각을 줄이는 순서, +z 쪽은 반대.
                    if (side == 0) { tris.Add(start); tris.Add(b); tris.Add(a); }
                    else { tris.Add(start); tris.Add(a); tris.Add(b); }
                }
            }
            var mesh = new Mesh { name = "CardRush_Shape_" + color, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(all);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public void Dispose()
        {
            foreach (var o in owned)
            {
                if (o == null) continue;
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(o);
                else UnityEngine.Object.DestroyImmediate(o);
            }
            owned.Clear();
        }
    }

    /// <summary>
    /// 레인 하나의 칸 그림 — 출발 받침, 칸 StepCount개, 결승 받침. 원시 도형으로 만들고 콜라이더는 지운다
    /// (판정은 시뮬 식이 하고, 그림이 PhysX에 끼면 안 된다). <see cref="Apply"/>는 그림만 바꾼다.
    /// </summary>
    public sealed class CardRushLaneView
    {
        //  돌: 지름 1.4 m 납작 원기둥, 윗면이 레인 바닥(y 0)에 맞는다.
        public const float StoneDiameter = 1.4f;
        public const float StoneThickness = 0.35f;
        //  문: 폭 2.2 m 문틀 + 문짝.
        public const float DoorWidth = 2.2f;
        public const float DoorHeight = 2.2f;
        public const float FrameThickness = 0.16f;
        public const float LeafHeight = DoorHeight - FrameThickness * 0.5f - 0.04f;

        /// <summary>문에 꽂힌 카드(spec 2026-11-03 §5.3) — 문 로컬 자리(문짝 가운데, 다가오는 쪽 면 조금 앞)·높이·기울기.</summary>
        public static readonly Vector3 StuckCardLocal = new Vector3(0f, LeafHeight * 0.5f + 0.02f, -0.075f);
        public const float StuckCardHeight = 0.45f;
        public const float StuckCardRoll = 15f;

        private readonly CardRushTheme theme;
        private readonly CardRushCoursePalette palette;
        private readonly CardRushCourse course;
        private readonly CardRushCardMaterials cards;
        private readonly HideFlags hideFlags;
        private readonly CardRushCardPlate[] stuck;   //  문마다 꽂힌 카드 — 처음 꽂힐 때 만든다(문짝 경첩의 자식이라 같이 열리고 가라앉는다)
        private readonly Renderer[] bodies;
        private readonly Transform[] movers;   // 돌다리 = 돌, 문 = 문짝 경첩
        private readonly Transform[] doors;    // 문 테마만 — 문틀과 문짝을 함께 가라앉힌다
        private readonly float[] doorVisibleNow;
        private readonly bool[] passedNow;
        private readonly float[] angleNow;

        public GameObject Root { get; }

        public CardRushLaneView(Transform parent, string name, CardRushTheme theme, Vector3 laneOrigin, Vector3 forward,
                                float stepSpacing, CardRushCourse course, CardRushCoursePalette palette, HideFlags hideFlags = HideFlags.None,
                                CardRushCardMaterials cards = null)
        {
            this.theme = theme;
            this.palette = palette;
            this.course = course;
            this.cards = cards;
            this.hideFlags = hideFlags;
            int count = course.StepCount;
            stuck = new CardRushCardPlate[count];
            bodies = new Renderer[count];
            movers = new Transform[count];
            doors = new Transform[count];
            doorVisibleNow = new float[count];
            for (int i = 0; i < count; i++) doorVisibleNow[i] = 1f;
            passedNow = new bool[count];
            angleNow = new float[count];

            var flat = CardRushLanes.Flat(forward);
            var rotation = Quaternion.LookRotation(flat, Vector3.up);

            Root = new GameObject(name) { hideFlags = hideFlags };
            Root.transform.SetParent(parent, false);
            Root.transform.SetPositionAndRotation(laneOrigin, rotation);

            //  출발 받침은 자리 0, 결승 받침은 마지막 칸 다음 자리(돌다리: 마지막 돌이 자리 StepCount라 그 한 칸 뒤 둑,
            //  문: 마지막 문 뒤 자리 StepCount가 몸이 서는 곳).
            float finishAt = theme == CardRushTheme.DoorDash ? count : count + 1;
            MakePad("Start", CardRushLanes.PositionAt(laneOrigin, flat, stepSpacing, 0f), rotation, stepSpacing, palette.StartPad, hideFlags);
            MakePad("Finish", CardRushLanes.PositionAt(laneOrigin, flat, stepSpacing, finishAt), rotation, stepSpacing, palette.FinishPad, hideFlags);

            for (int k = 0; k < count; k++)
            {
                var at = CardRushCourseLayout.ObstacleAt(theme, laneOrigin, flat, stepSpacing, k);
                var color = course.RequiredColor(k);
                if (theme == CardRushTheme.DoorDash)
                {
                    BuildDoor(k, at, rotation, color, hideFlags);
                }
                else
                {
                    BuildStone(k, at, rotation, color, hideFlags);
                }
            }
        }

        private void MakePad(string name, Vector3 at, Quaternion rotation, float stepSpacing, Material material, HideFlags hideFlags)
        {
            var pad = Primitive(PrimitiveType.Cube, name, Root.transform, hideFlags);
            float height = theme == CardRushTheme.DoorDash ? 0.06f : 0.6f;
            float length = theme == CardRushTheme.DoorDash ? Mathf.Min(1.6f, stepSpacing * 0.5f) : Mathf.Min(2.0f, stepSpacing * 0.9f);
            pad.transform.SetPositionAndRotation(at + Vector3.down * (height * 0.5f - 0.001f), rotation);
            pad.transform.localScale = new Vector3(DoorWidth * 0.95f, height, length);
            pad.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void BuildStone(int k, Vector3 at, Quaternion rotation, CardColor color, HideFlags hideFlags)
        {
            //  뒤집힘 축(레인 진행 방향)이 돌 가운데를 지나도록, 바깥 축 오브젝트를 돌 가운데에 둔다.
            var pivot = new GameObject($"Stone {k}") { hideFlags = hideFlags };
            pivot.transform.SetParent(Root.transform, false);
            pivot.transform.SetPositionAndRotation(at + Vector3.down * (StoneThickness * 0.5f), rotation);

            var stone = Primitive(PrimitiveType.Cylinder, "Body", pivot.transform, hideFlags);
            stone.transform.localPosition = Vector3.zero;
            stone.transform.localScale = new Vector3(StoneDiameter, StoneThickness * 0.5f, StoneDiameter);
            var renderer = stone.GetComponent<Renderer>();
            renderer.sharedMaterial = palette.Of(color, false);

            var decal = Decal(color, pivot.transform, hideFlags);
            //  -z를 보는 모양을 위로 눕힌다. 윗면에서 1 cm 띄워 같은 평면 깜빡임을 피한다.
            decal.transform.localPosition = new Vector3(0f, StoneThickness * 0.5f + 0.01f, 0f);
            decal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            decal.transform.localScale = Vector3.one * (StoneDiameter * 0.6f);

            //  아랫면: 어두운 같은 색 원판(아래를 본다). 뒤집히면 이 면이 위로 온다.
            var under = new GameObject("Under") { hideFlags = hideFlags };
            under.transform.SetParent(pivot.transform, false);
            under.AddComponent<MeshFilter>().sharedMesh = palette.Disc;
            var underRenderer = under.AddComponent<MeshRenderer>();
            underRenderer.sharedMaterial = palette.UndersideOf(color);
            underRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            under.transform.localPosition = new Vector3(0f, -(StoneThickness * 0.5f + 0.01f), 0f);
            under.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            under.transform.localScale = Vector3.one * StoneDiameter;

            bodies[k] = renderer;
            movers[k] = pivot.transform;
        }

        private void BuildDoor(int k, Vector3 at, Quaternion rotation, CardColor color, HideFlags hideFlags)
        {
            var door = new GameObject($"Door {k}") { hideFlags = hideFlags };
            door.transform.SetParent(Root.transform, false);
            door.transform.SetPositionAndRotation(at, rotation);

            float half = DoorWidth * 0.5f;
            Bar("PostL", door.transform, new Vector3(-half, DoorHeight * 0.5f, 0f), new Vector3(FrameThickness, DoorHeight, FrameThickness), hideFlags);
            Bar("PostR", door.transform, new Vector3(half, DoorHeight * 0.5f, 0f), new Vector3(FrameThickness, DoorHeight, FrameThickness), hideFlags);
            Bar("Lintel", door.transform, new Vector3(0f, DoorHeight, 0f), new Vector3(DoorWidth + FrameThickness, FrameThickness, FrameThickness), hideFlags);

            //  경첩은 왼쪽 기둥. 문짝은 경첩에서 오른쪽으로 뻗고, 앞(진행 방향)으로 열린다.
            var hinge = new GameObject("Hinge") { hideFlags = hideFlags };
            hinge.transform.SetParent(door.transform, false);
            hinge.transform.localPosition = new Vector3(-half + FrameThickness * 0.5f, 0f, 0f);

            float leafWidth = DoorWidth - FrameThickness;
            float leafHeight = LeafHeight;
            var leaf = Primitive(PrimitiveType.Cube, "Leaf", hinge.transform, hideFlags);
            leaf.transform.localPosition = new Vector3(leafWidth * 0.5f, leafHeight * 0.5f + 0.02f, 0f);
            leaf.transform.localScale = new Vector3(leafWidth, leafHeight, 0.08f);
            var renderer = leaf.GetComponent<Renderer>();
            renderer.sharedMaterial = palette.Of(color, false);

            //  모양은 다가오는 쪽(-z) 면에. 문짝과 같이 돈다.
            var decal = Decal(color, hinge.transform, hideFlags);
            decal.transform.localPosition = new Vector3(leafWidth * 0.5f, leafHeight * 0.55f, -0.05f);
            decal.transform.localRotation = Quaternion.identity;
            decal.transform.localScale = Vector3.one * 1.0f;

            bodies[k] = renderer;
            movers[k] = hinge.transform;
            doors[k] = door.transform;
        }

        private void Bar(string name, Transform parent, Vector3 localPosition, Vector3 scale, HideFlags hideFlags)
        {
            var bar = Primitive(PrimitiveType.Cube, name, parent, hideFlags);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = scale;
            bar.GetComponent<Renderer>().sharedMaterial = palette.Frame;
        }

        private GameObject Decal(CardColor color, Transform parent, HideFlags hideFlags)
        {
            var decal = new GameObject("Shape") { hideFlags = hideFlags };
            decal.transform.SetParent(parent, false);
            decal.AddComponent<MeshFilter>().sharedMesh = palette.ShapeOf(color);
            var r = decal.AddComponent<MeshRenderer>();
            r.sharedMaterial = palette.Decal;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return decal;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, HideFlags hideFlags)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = hideFlags;
            //  그림 전용 — 콜라이더가 남으면 PhysX 대리 몸과 부딪치거나 레이에 걸린다.
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
            }
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// 그 몸의 상태를 그림 틱에서 읽어 칸 그림만 바꾼다(바램·뒤집힘·문짝). 시뮬 값은 건드리지 않는다.
        /// <paramref name="followedProgress"/>는 카메라가 따라가는 몸의 그림 진행 — 문 테마에서 그보다 뒤의 문을 가라앉힌다.
        /// </summary>
        public void Apply(in CardRushRunnerState s, long drawnTick, CardRushTiming timing, float followedProgress = float.NaN)
        {
            bool stunned = CardRushCourseLayout.TryStun(s, drawnTick, timing.StunTicks, out float stunT);
            for (int k = 0; k < bodies.Length; k++)
            {
                bool passed = CardRushCourseLayout.IsPassed(s, drawnTick, timing.MoveTicks, k);
                if (passed != passedNow[k])
                {
                    passedNow[k] = passed;
                    bodies[k].sharedMaterial = palette.Of(course.RequiredColor(k), passed);
                }

                float angle;
                if (theme == CardRushTheme.DoorDash)
                {
                    angle = CardRushCourseLayout.DoorOpenAngle(s, drawnTick, timing.MoveTicks, k);
                    if (stunned && k == s.Step)
                    {
                        angle += CardRushCourseLayout.DoorWobbleAngle(stunT);
                    }
                }
                else
                {
                    angle = stunned && k == s.Step ? CardRushCourseLayout.StoneFlipAngle(stunT) : 0f;
                }

                if (doors[k] != null)
                {
                    float visible = CardRushCourseLayout.DoorVisibility(k, followedProgress);
                    if (Mathf.Approximately(visible, doorVisibleNow[k]) == false)
                    {
                        doorVisibleNow[k] = visible;
                        //  바닥으로 가라앉힌다(투명 재질 없이 — 효과 하나, 색 그대로).
                        doors[k].localScale = new Vector3(1f, visible, 1f);
                        doors[k].gameObject.SetActive(visible > 0f);
                    }
                }

                if (theme == CardRushTheme.DoorDash && cards != null)
                {
                    ShowStuck(k, CardRushThrowFx.IsStuckIn(s, drawnTick, k));
                }

                if (Mathf.Approximately(angle, angleNow[k]) == false)
                {
                    angleNow[k] = angle;
                    //  돌은 진행 축으로 옆으로 구르고, 문짝은 경첩(위 축)으로 앞쪽(-각)으로 열린다.
                    movers[k].localRotation = theme == CardRushTheme.DoorDash
                        ? Quaternion.AngleAxis(-angle, Vector3.up)
                        : Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
        }

        /// <summary>문 칸 k에 꽂힌 카드를 켜고 끈다. 맞힌 칸이라 색은 그 칸 요구 색이다.</summary>
        private void ShowStuck(int k, bool on)
        {
            if (on == false)
            {
                stuck[k]?.SetVisible(false);
                return;
            }
            if (stuck[k] == null)
            {
                var hinge = movers[k];
                var plate = new CardRushCardPlate(hinge, "StuckCard", cards, hideFlags);
                plate.Root.localPosition = StuckCardLocal - hinge.localPosition;
                plate.Root.localRotation = Quaternion.AngleAxis(StuckCardRoll, Vector3.forward);
                plate.Root.localScale = Vector3.one * StuckCardHeight;
                plate.SetFace(true, course.RequiredColor(k), false);
                stuck[k] = plate;
            }
            stuck[k].SetVisible(true);
        }

        /// <summary>문 칸 k에 꽂힌 카드가 보이나 — 시험·진단용.</summary>
        public bool IsStuckVisible(int k) => stuck[k] != null && stuck[k].Visible;

        public void Destroy()
        {
            if (Root == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
        }
    }

    /// <summary>
    /// 몸마다 그 레인에 칸을 깔고 그림 틱에 맞춰 바꾼다(spec §7.4). 돌·문은 씬에 없고 (칸 수, 코스 색)에서 실행 중에 만든다 —
    /// 두 테마가 같은 식을 쓴다. 몸이 사라지면 그 레인을 지운다. 그림만 다루고 시뮬 상태는 읽기만 한다.
    ///
    /// <para>몸마다 그 몸이 그려지는 틱(<see cref="EntityRenderClock.TickFor"/>)으로 읽는다 — 남의 상태는 최신 스냅이라
    /// 보간된 몸보다 앞서 있어서, 지금 틱으로 읽으면 몸이 닿기 전에 문이 열리고 돌이 뒤집힌다.</para>
    /// </summary>
    public class CardRushCourseView : IStartable, ITickable, IDisposable
    {
        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly EntityRenderClock renderClock;
        private readonly ISpectateTargets spectate;

        private readonly Dictionary<string, CardRushLaneView> lanes = new Dictionary<string, CardRushLaneView>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly List<string> gone = new List<string>();

        private CardRushCoursePalette palette;
        private CardRushCardMaterials cards;
        private GameObject root;
        private CardRushCourse builtFor;

        public CardRushCourseView(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry,
                                  EntityRenderClock renderClock, ISpectateTargets spectate)
        {
            this.spectate = spectate;
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.renderClock = renderClock;
        }

        /// <summary>지금 깔린 레인 수(몸 수). 시험·진단용.</summary>
        public int LaneCount => lanes.Count;

        public void Start()
        {
            palette = new CardRushCoursePalette();
            cards = new CardRushCardMaterials(palette);
            root = new GameObject("CardRushCourse");
        }

        public void Tick()
        {
            var course = world.Course;
            var rules = world.Rules;
            if (course == null || rules == null || rules.HasRules == false || root == null)
            {
                return;
            }

            //  클라 씨앗은 늦게 와서 출발 전에 코스가 한 번 바뀔 수 있다 — 바뀐 색으로 다시 깐다.
            if (ReferenceEquals(course, builtFor) == false)
            {
                ClearLanes();
                builtFor = course;
            }

            float followed = FollowedProgress();
            seen.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var cardRunner = entity.Get<CardRushRunner>();
                if (cardRunner == null)
                {
                    continue;
                }
                seen.Add(entity.Id);

                if (lanes.TryGetValue(entity.Id, out var lane) == false)
                {
                    lane = new CardRushLaneView(root.transform, "Lane " + entity.Id, rules.Theme, cardRunner.LaneOrigin,
                                                rules.Forward, rules.StepSpacing, course, palette, HideFlags.None, cards);
                    lanes.Add(entity.Id, lane);
                }

                lane.Apply(cardRunner.State, renderClock.TickFor(entity.Id), world.Timing, followed);
            }

            gone.Clear();
            foreach (var id in lanes.Keys)
            {
                if (seen.Contains(id) == false)
                {
                    gone.Add(id);
                }
            }
            foreach (var id in gone)
            {
                lanes[id].Destroy();
                lanes.Remove(id);
            }
        }

        //  카메라가 따라가는 몸(관전 대상)의 그림 진행. 없으면 NaN — 문을 숨기지 않는다.
        private float FollowedProgress()
        {
            string id = spectate?.Current;
            var runner = string.IsNullOrEmpty(id) ? null : entityRegistry.Get(id)?.Get<CardRushRunner>();
            return runner == null
                ? float.NaN
                : CardRushCourseLayout.DrawnProgress(runner.State, renderClock.TickFor(id), world.Timing.MoveTicks);
        }

        private void ClearLanes()
        {
            foreach (var lane in lanes.Values)
            {
                lane.Destroy();
            }
            lanes.Clear();
        }

        public void Dispose()
        {
            ClearLanes();
            if (root != null)
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }
            cards?.Dispose();
            cards = null;
            palette?.Dispose();
            palette = null;
        }
    }
}
