using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 칸 그림(spec §4·§7.4) — 돌·문 자리, 그림 틱으로 읽는 바램·문짝·뒤집힘, 카메라 식.
    /// 그림은 시뮬 상태를 읽기만 하므로 식을 직접 부르고, 레인 그림은 실제 GameObject로 한 번 깔아 본다.
    /// </summary>
    public class CardRushCourseLayoutTests
    {
        private const float Eps = 1e-4f;
        private const int MoveTicks = 15;
        private const int StunTicks = 30;

        private static readonly CardRushTiming Timing = new CardRushTiming(MoveTicks, StunTicks);

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CardRushCoursePalette palette;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { if (go != null) Object.DestroyImmediate(go); }
            spawned.Clear();
            palette?.Dispose();
            palette = null;
        }

        private static void Near(Vector3 expected, Vector3 actual, string message = null)
            => Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Eps), $"{message} 기대 {expected} 실제 {actual}");

        // ── 자리 ──────────────────────────────────────────────

        [Test]
        public void 돌다리_칸_0은_출발점에서_한_칸_앞()
        {
            var origin = new Vector3(3f, 0f, -2f);
            Near(origin + Vector3.forward * 2.2f,
                 CardRushCourseLayout.ObstacleAt(CardRushTheme.StoneBridge, origin, Vector3.forward, 2.2f, 0));
        }

        [Test]
        public void 문_칸_0은_출발점에서_반_칸_앞()
        {
            var origin = new Vector3(3f, 0f, -2f);
            Near(origin + Vector3.forward * 1.5f,
                 CardRushCourseLayout.ObstacleAt(CardRushTheme.DoorDash, origin, Vector3.forward, 3.0f, 0));
        }

        [Test]
        public void 마지막_칸_자리는_비스듬한_레인에서도_진행_방향_위()
        {
            var origin = new Vector3(-5f, 0f, 7f);
            //  수평으로 눕히기 전의 방향(아래로 기울었다) — 높이는 무시하고 수평 방향만 쓴다.
            var forward = new Vector3(1f, -0.3f, 1f);
            var flat = new Vector3(1f, 0f, 1f).normalized;

            Near(origin + flat * (2.2f * 40f),
                 CardRushCourseLayout.ObstacleAt(CardRushTheme.StoneBridge, origin, forward, 2.2f, 39), "돌다리 칸 39 = 자리 40");
            Near(origin + flat * (3.0f * 39.5f),
                 CardRushCourseLayout.ObstacleAt(CardRushTheme.DoorDash, origin, forward, 3.0f, 39), "문 칸 39 = 자리 39.5");
        }

        // ── 그림 틱으로 읽는 상태 ─────────────────────────────

        [Test]
        public void 맞힌_칸은_그리로_가는_이동이_끝나야_지나간_칸()
        {
            //  칸 4를 틱 1000에 맞혔다(Step 5).
            var s = new CardRushRunnerState { Step = 5, MoveStartTick = 1000 };

            Assert.IsTrue(CardRushCourseLayout.IsPassed(s, 1000, MoveTicks, 3), "그 전 칸은 이미 지나갔다");
            Assert.IsFalse(CardRushCourseLayout.IsPassed(s, 1000 + MoveTicks - 1, MoveTicks, 4), "이동 중엔 아직");
            Assert.IsTrue(CardRushCourseLayout.IsPassed(s, 1000 + MoveTicks, MoveTicks, 4), "이동이 끝난 틱부터");
            Assert.IsFalse(CardRushCourseLayout.IsPassed(s, 2000, MoveTicks, 5), "지금 잡으려는 칸은 아니다");
        }

        [Test]
        public void 남의_상태가_그림보다_앞서면_아직_안_건넌_칸()
        {
            //  최신 스냅은 이미 칸 4를 맞혔지만, 그려지는 몸(그림 틱 990)은 아직 맞히기 전이다.
            var s = new CardRushRunnerState { Step = 5, MoveStartTick = 1000 };
            Assert.IsFalse(CardRushCourseLayout.IsPassed(s, 990, MoveTicks, 4));
            Assert.AreEqual(0f, CardRushCourseLayout.DoorOpenAngle(s, 990, MoveTicks, 4), Eps, "문도 아직 닫혀 있다");
        }

        [Test]
        public void 문짝은_카드가_꽂힌_뒤_0_1초에_100도까지_열리고_건넌_문은_열린_채()
        {
            var s = new CardRushRunnerState { Step = 5, MoveStartTick = 1000 };
            int openTicks = CardRushTiming.Ticks(CardRushCourseLayout.DoorOpenSeconds);
            Assert.AreEqual(5, openTicks);
            long stuck = 1000 + CardRushThrowFx.FlightTicks;   // 던진 카드가 문에 닿는 틱
            Assert.AreEqual(1003, stuck);

            Assert.AreEqual(0f, CardRushCourseLayout.DoorOpenAngle(s, 1000, MoveTicks, 4), Eps);
            Assert.AreEqual(0f, CardRushCourseLayout.DoorOpenAngle(s, 1002, MoveTicks, 4), Eps, "카드가 날아가는 동안은 닫혀 있다");
            Assert.AreEqual(0f, CardRushCourseLayout.DoorOpenAngle(s, stuck, MoveTicks, 4), Eps, "꽂히는 틱에 열리기 시작");
            Assert.AreEqual(40f, CardRushCourseLayout.DoorOpenAngle(s, stuck + 2, MoveTicks, 4), Eps, "5틱 중 2틱");
            Assert.AreEqual(100f, CardRushCourseLayout.DoorOpenAngle(s, stuck + openTicks, MoveTicks, 4), Eps, "던짐 + 8틱에 활짝");
            Assert.AreEqual(100f, CardRushCourseLayout.DoorOpenAngle(s, 1000 + MoveTicks + 50, MoveTicks, 4), Eps, "건넌 문은 열린 채");
            Assert.AreEqual(100f, CardRushCourseLayout.DoorOpenAngle(s, 1000, MoveTicks, 2), Eps, "그 전 문도 열린 채");
            Assert.AreEqual(0f, CardRushCourseLayout.DoorOpenAngle(s, 1000 + MoveTicks, MoveTicks, 5), Eps, "앞 문은 닫힘");
        }

        [Test]
        public void 멈춤은_끝_틱에서_정확히_끝난다()
        {
            var s = new CardRushRunnerState { Step = 3, StunEndTick = 2000 };
            Assert.IsFalse(CardRushCourseLayout.TryStun(s, 2000 - StunTicks - 1, StunTicks, out _));
            Assert.IsTrue(CardRushCourseLayout.TryStun(s, 2000 - StunTicks, StunTicks, out float t0));
            Assert.AreEqual(0f, t0, Eps);
            Assert.IsTrue(CardRushCourseLayout.TryStun(s, 2000 - 15, StunTicks, out float tHalf));
            Assert.AreEqual(0.5f, tHalf, Eps);
            Assert.IsFalse(CardRushCourseLayout.TryStun(s, 2000, StunTicks, out _), "끝 틱엔 이미 원래대로");
            Assert.IsFalse(CardRushCourseLayout.TryStun(new CardRushRunnerState { Step = 3 }, 5, StunTicks, out _), "틀린 적 없음");
        }

        [Test]
        public void 뒤집힘과_출렁임은_멈춤_처음과_끝에서_원래_자세()
        {
            Assert.AreEqual(0f, CardRushCourseLayout.StoneFlipAngle(0f), Eps);
            Assert.AreEqual(0f, CardRushCourseLayout.StoneFlipAngle(1f), Eps);
            Assert.AreEqual(180f, CardRushCourseLayout.StoneFlipAngle(0.5f), Eps, "빠진 동안엔 뒤집힌 채");
            Assert.AreEqual(0f, CardRushCourseLayout.DoorWobbleAngle(0f), Eps);
            Assert.AreEqual(0f, CardRushCourseLayout.DoorWobbleAngle(1f), Eps);
            Assert.AreEqual(0f, CardRushCourseLayout.DoorWobbleAngle(0.1f), Eps, "박기 전엔 안 흔들린다");
            Assert.That(Mathf.Abs(CardRushCourseLayout.DoorWobbleAngle(0.2f)), Is.GreaterThan(3f), "박은 직후 흔들린다");
        }

        [Test]
        public void 문은_따라가는_몸_뒤로_가면_가라앉아_사라진다()
        {
            Assert.AreEqual(1f, CardRushCourseLayout.DoorVisibility(5, 3f), Eps, "앞 문은 그대로");
            Assert.AreEqual(1f, CardRushCourseLayout.DoorVisibility(2, 2.55f), Eps, "막 지나간 문(0.05칸 뒤)은 아직 그대로");
            Assert.AreEqual(0.5f, CardRushCourseLayout.DoorVisibility(2, 2.8f), Eps, "0.3칸 뒤 — 반쯤 가라앉음");
            Assert.AreEqual(0f, CardRushCourseLayout.DoorVisibility(2, 3f), Eps, "몸이 다음 자리에 서면 바로 뒤 문(0.5칸 뒤)도 사라진다 — 카메라 앞을 안 가린다");
            Assert.AreEqual(0f, CardRushCourseLayout.DoorVisibility(0, 30f), Eps);
            Assert.AreEqual(1f, CardRushCourseLayout.DoorVisibility(0, float.NaN), Eps, "따라갈 몸이 없으면 다 보인다");
        }

        [Test]
        public void ShouldEnterFinish는_판이_안_끝났고_조작판이_있고_내가_완주했을_때만()
        {
            Assert.IsTrue(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: false, padOpen: true, mineFinished: true));
            Assert.IsFalse(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: true, padOpen: true, mineFinished: true), "결과 화면 위에 겹치지 않는다");
            Assert.IsFalse(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: false, padOpen: false, mineFinished: true), "이미 넘어갔다");
            Assert.IsFalse(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: false, padOpen: true, mineFinished: false));
        }

        // ── 레인 그림 ─────────────────────────────────────────

        private static CardRushCourse Course(int steps = 12) => new CardRushCourse(77UL, steps);

        private CardRushLaneView Lane(CardRushTheme theme, CardRushCourse course, Vector3 origin, float spacing)
        {
            palette ??= new CardRushCoursePalette();
            var parent = new GameObject("lane-test");
            spawned.Add(parent);
            return new CardRushLaneView(parent.transform, "Lane", theme, origin, Vector3.forward, spacing, course, palette);
        }

        [TestCase(CardRushTheme.StoneBridge)]
        [TestCase(CardRushTheme.DoorDash)]
        public void 레인은_칸마다_요구_색이고_콜라이더가_없다(CardRushTheme theme)
        {
            var course = Course();
            var lane = Lane(theme, course, new Vector3(1f, 0f, 2f), theme == CardRushTheme.DoorDash ? 3.0f : 2.2f);

            Assert.IsEmpty(lane.Root.GetComponentsInChildren<Collider>(true), "그림 전용 — 콜라이더가 남으면 PhysX 대리 몸과 부딪친다");
            string prefix = theme == CardRushTheme.DoorDash ? "Door " : "Stone ";
            for (int k = 0; k < course.StepCount; k++)
            {
                var step = lane.Root.transform.Find(prefix + k);
                Assert.IsNotNull(step, $"칸 {k}");
                var expected = CardRushCoursePalette.ColorOf(course.RequiredColor(k));
                bool colored = false;
                foreach (var r in step.GetComponentsInChildren<MeshRenderer>(true))
                {
                    colored |= r.sharedMaterial == palette.Of(course.RequiredColor(k), false);
                }
                Assert.IsTrue(colored, $"칸 {k}는 {course.RequiredColor(k)} 재질({expected})");
            }
            Assert.IsNotNull(lane.Root.transform.Find("Start"));
            Assert.IsNotNull(lane.Root.transform.Find("Finish"));
        }

        [Test]
        public void 돌다리_돌은_칸_자리에_윗면이_바닥에_맞게_놓인다()
        {
            var course = Course();
            var origin = new Vector3(1f, 0f, 2f);
            var lane = Lane(CardRushTheme.StoneBridge, course, origin, 2.2f);
            var stone = lane.Root.transform.Find("Stone 3");
            var at = CardRushCourseLayout.ObstacleAt(CardRushTheme.StoneBridge, origin, Vector3.forward, 2.2f, 3);
            var bounds = stone.GetComponentInChildren<MeshRenderer>().bounds;
            Assert.AreEqual(at.x, bounds.center.x, 1e-3f);
            Assert.AreEqual(at.z, bounds.center.z, 1e-3f);
            Assert.AreEqual(0f, bounds.max.y, 1e-3f, "윗면 = 레인 바닥(y 0) — 몸이 돌 위에 선다");
        }

        [Test]
        public void 지나간_칸은_바래고_틀림_중엔_다음_돌이_뒤집힌다()
        {
            var course = Course();
            var lane = Lane(CardRushTheme.StoneBridge, course, Vector3.zero, 2.2f);
            //  칸 0·1을 건넜고 칸 2에서 틀려 멈춤 한가운데.
            var s = new CardRushRunnerState { Step = 2, MoveStartTick = 900, StunEndTick = 1015 };
            lane.Apply(s, 1000, Timing);

            Renderer Body(int k) => lane.Root.transform.Find("Stone " + k).Find("Body").GetComponent<Renderer>();
            Assert.AreSame(palette.Of(course.RequiredColor(0), true), Body(0).sharedMaterial, "칸 0은 바램");
            Assert.AreSame(palette.Of(course.RequiredColor(1), true), Body(1).sharedMaterial, "칸 1은 바램");
            Assert.AreSame(palette.Of(course.RequiredColor(2), false), Body(2).sharedMaterial, "칸 2는 원래 색");

            float flipped = Quaternion.Angle(Quaternion.identity, lane.Root.transform.Find("Stone 2").localRotation);
            Assert.AreEqual(180f, flipped, 0.5f, "틀린 칸의 돌이 뒤집혔다");
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, lane.Root.transform.Find("Stone 3").localRotation), 0.5f);

            //  멈춤이 끝나는 틱엔 제자리.
            lane.Apply(s, 1015, Timing);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, lane.Root.transform.Find("Stone 2").localRotation), 0.5f);
        }

        [Test]
        public void 뒤집힌_돌은_어두운_아랫면이_위로_온다()
        {
            var course = Course();
            var lane = Lane(CardRushTheme.StoneBridge, course, Vector3.zero, 2.2f);
            var stone = lane.Root.transform.Find("Stone 2");
            var under = stone.Find("Under").GetComponent<Renderer>();
            Assert.AreSame(palette.UndersideOf(course.RequiredColor(2)), under.sharedMaterial);
            Color.RGBToHSV(CardRushCoursePalette.ColorOf(course.RequiredColor(2)), out float h0, out _, out float v0);
            Color.RGBToHSV(palette.UndersideOf(course.RequiredColor(2)).GetColor("_BaseColor"), out float h1, out _, out float v1);
            Assert.AreEqual(h0, h1, 0.02f, "같은 색 계열");
            Assert.Less(v1, v0, "더 어둡다");
            Assert.Less(under.bounds.center.y, -0.3f, "평소엔 아래를 본다");

            lane.Apply(new CardRushRunnerState { Step = 2, StunEndTick = 1015 }, 1000, Timing);
            Assert.Greater(under.bounds.center.y, 0f, "뒤집히면 위로 온다");
        }

        [Test]
        public void 문_테마는_따라가는_몸_뒤의_문을_숨긴다()
        {
            var course = Course();
            var lane = Lane(CardRushTheme.DoorDash, course, Vector3.zero, 3.0f);
            lane.Apply(new CardRushRunnerState { Step = 3 }, 1000, Timing, followedProgress: 3f);
            Assert.IsFalse(lane.Root.transform.Find("Door 1").gameObject.activeSelf);
            Assert.IsFalse(lane.Root.transform.Find("Door 2").gameObject.activeSelf, "바로 뒤 문");
            Assert.IsTrue(lane.Root.transform.Find("Door 3").gameObject.activeSelf, "앞 문");

            //  관전 대상이 뒤로 바뀌면 다시 선다.
            lane.Apply(new CardRushRunnerState { Step = 3 }, 1000, Timing, followedProgress: 0f);
            Assert.IsTrue(lane.Root.transform.Find("Door 1").gameObject.activeSelf);
            Assert.AreEqual(1f, lane.Root.transform.Find("Door 1").localScale.y, Eps);
        }

        //  spec 2026-11-03 §5.3: 맞힌 카드는 날아가(3틱) 문짝에 꽂혀 남는다 — 문짝의 자식이라 같이 열리고 가라앉는다.
        [Test]
        public void 꽂힌_카드는_날아가_닿은_뒤부터_문짝에_남는다()
        {
            var course = Course();
            palette ??= new CardRushCoursePalette();
            var cards = new CardRushCardMaterials(palette);
            try
            {
                var parent = new GameObject("lane-test");
                spawned.Add(parent);
                var lane = new CardRushLaneView(parent.transform, "Lane", CardRushTheme.DoorDash, Vector3.zero, Vector3.forward, 3f,
                                                course, palette, HideFlags.None, cards);
                var s = new CardRushRunnerState { Step = 2, MoveStartTick = 1000 };

                lane.Apply(s, 1000 + CardRushThrowFx.FlightTicks - 1, Timing);
                Assert.IsTrue(lane.IsStuckVisible(0), "건넌 문엔 그대로 꽂혀 있다");
                Assert.IsFalse(lane.IsStuckVisible(1), "방금 던진 카드는 아직 날아가는 중");
                Assert.IsFalse(lane.IsStuckVisible(2), "안 맞힌 문");

                lane.Apply(s, 1000 + CardRushThrowFx.FlightTicks, Timing);
                Assert.IsTrue(lane.IsStuckVisible(1));
                var card = lane.Root.transform.Find("Door 1/Hinge/StuckCard");
                Assert.IsNotNull(card, "문짝 경첩의 자식 — 문이 열리고 가라앉을 때 같이 간다");
                Assert.AreEqual(course.RequiredColor(1), ColorOfPlate(card, palette), "맞힌 칸 = 그 칸 요구 색");

                //  같은 칸에서 틀리면(멈춤) 꽂히지 않는다.
                lane.Apply(new CardRushRunnerState { Step = 2, MoveStartTick = 1000, StunEndTick = 2030, Attempt = 1 }, 2010, Timing);
                Assert.IsFalse(lane.IsStuckVisible(2));
            }
            finally
            {
                cards.Dispose();
            }
        }

        private static CardColor ColorOfPlate(Transform card, CardRushCoursePalette palette)
        {
            var material = card.Find("Body/Plate").GetComponent<Renderer>().sharedMaterial;
            for (int i = 0; i < CardColors.Count; i++)
            {
                if (palette.Of((CardColor)i, false) == material) return (CardColor)i;
            }
            Assert.Fail("앞면 색 재질이 아니다: " + material.name);
            return default;
        }

        [Test]
        public void 문짝은_맞힌_이동에서_앞으로_열린다()
        {
            var course = Course();
            var lane = Lane(CardRushTheme.DoorDash, course, Vector3.zero, 3.0f);
            var s = new CardRushRunnerState { Step = 1, MoveStartTick = 1000 };
            lane.Apply(s, 1000 + MoveTicks, Timing);

            var hinge = lane.Root.transform.Find("Door 0").Find("Hinge");
            var leaf = hinge.Find("Leaf");
            Assert.AreEqual(100f, Quaternion.Angle(Quaternion.identity, hinge.localRotation), 0.5f);
            Assert.Greater(leaf.position.z, 1.5f + 0.5f, "문짝이 진행 방향(앞)으로 열렸다 — 몸 쪽으로 열리면 몸을 친다");
        }

        // ── 카메라 식 ─────────────────────────────────────────

        [Test]
        public void 카메라_피벗은_몸_앞_0_75칸이고_10초당_비율로_따라간다()
        {
            Near(new Vector3(1f, 0f, 2f + 2.2f * 0.75f), CardRushCamera.PivotTarget(new Vector3(1f, 0f, 2f), Vector3.forward, 2.2f));

            var next = CardRushCamera.Follow(Vector3.zero, new Vector3(10f, 0f, 0f), 0.02f);
            Assert.AreEqual(10f * (1f - Mathf.Exp(-10f * 0.02f)), next.x, Eps);
        }

        [Test]
        public void 카메라_각과_거리()
        {
            Assert.AreEqual(0f, CardRushCamera.YawDegrees(Vector3.forward), Eps);
            Assert.AreEqual(90f, CardRushCamera.YawDegrees(Vector3.right), Eps);
            Assert.AreEqual(9f, CardRushCamera.DistanceFor(1920, 1080), Eps);
            Assert.AreEqual(9f * 1.35f, CardRushCamera.DistanceFor(1080, 1920), Eps, "세로 화면은 멀리");
        }

        //  CardRush.unity 메인 카메라의 field of view(세로). 씬 값이 바뀌면 여기도 같이 바꾼다.
        private const float SceneFov = 50f;
        private const float StepSpacing = 2.2f;   //  두 맵의 칸 간격(CardRushMapRules 기본값)

        //  화면 높이(아래 0) — 내 몸 자리 = 피벗에서 진행 방향 −앞보기만큼 뒤.
        private static float BodyY(float forward, float up, int screenW, int screenH)
            => CardRushCamera.ViewportY(-CardRushCamera.LookAheadSteps * StepSpacing + forward, up,
                                        CardRushCamera.PitchDegrees, CardRushCamera.DistanceFor(screenW, screenH), SceneFov);

        //  아래 카드 띠는 2차에서 지웠다 — 그 띠 위 끝(가로 0.235)을 비워 둘 기준으로 남긴다(세로 띠는 더 낮았다).
        private static float BandTop(int screenW, int screenH) => CardRushCamera.KeepClearBottom01;

        //  실제 판 스크린샷(맵 17·18)에서 카드가 내 몸을 다 덮었다 — 발이 0.21, 카드 띠 위 끝이 0.39였다.
        //  가로·세로 모두 내 몸(발·머리)과 몸개그 중 가장 낮은 자세(풍덩: 반 칸 앞, 1.1 m 아래)가 띠 위에 와야 한다.
        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(1080, 1920)]
        [TestCase(1080, 2400)]
        public void 내_몸과_몸개그는_카드_띠_위에_보인다(int w, int h)
        {
            float band = BandTop(w, h);
            const float Clear = 0.05f;   //  발 밑 그림자·물보라 자리

            Assert.Greater(BodyY(0f, 0f, w, h), band + Clear, "서 있는 발");
            Assert.Greater(BodyY(0f, BodySizes.CharacterHeight, w, h), band + Clear, "머리");
            Assert.Less(BodyY(0f, BodySizes.CharacterHeight, w, h), 0.9f, "머리가 위 HUD(진행 막대) 밑에 남는다");

            float splashForward = 0.5f * StepSpacing;
            Assert.Greater(BodyY(splashForward, -CardRushGag.SplashDepth, w, h), band + Clear, "풍덩 — 물에 잠긴 발");
        }

        //  2차: 카드가 머리 위로 올라갔다(spec 2026-11-03 §5.1 위험 "화면 위로 잘림") — 각·거리를 그대로 둬도 내 카드 위 끝
        //  (머리 + 띄움 + 카드 높이, 링은 그 조금 바깥)이 위 HUD(진행 막대) 밑에 남아야 한다.
        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(1080, 1920)]
        [TestCase(1080, 2400)]
        public void 내_머리_위_카드는_위_HUD_밑에_보인다(int w, int h)
        {
            //  머리 높이를 몸 키로 넉넉히 잡는다(실제 머리 뼈는 그보다 낮다).
            float cardTop = BodySizes.CharacterHeight + CardRushHeadCardView.HeadTopAboveBone + CardRushHeadCardView.HeadCardLift + CardRushHeadCardView.MineHeight;
            float ringTop = cardTop - CardRushHeadCardView.MineHeight * 0.5f + CardRushHeadCardView.MineHeight * CardRushHeadCardView.RingRadius;
            Assert.Less(BodyY(0f, Mathf.Max(cardTop, ringTop), w, h), 0.88f, "카드·링 위 끝");
        }

        [Test]
        public void 화면_높이_식은_피벗이_가운데다()
        {
            Assert.AreEqual(0.5f, CardRushCamera.ViewportY(0f, 0f, 35f, 9f, 50f), Eps);
            //  피벗 바로 앞(진행 방향)은 화면 위로, 아래(땅 밑)는 화면 아래로.
            Assert.Greater(CardRushCamera.ViewportY(2f, 0f, 35f, 9f, 50f), 0.5f);
            Assert.Less(CardRushCamera.ViewportY(0f, -1f, 35f, 9f, 50f), 0.5f);
            //  시야각 끝 — 카메라 위쪽 축으로 거리×tan(25°)만큼 올린 점은 화면 맨 위(1).
            float up = 9f * Mathf.Tan(25f * Mathf.Deg2Rad);
            float p = 35f * Mathf.Deg2Rad;
            Assert.AreEqual(1f, CardRushCamera.ViewportY(up * Mathf.Sin(p), up * Mathf.Cos(p), 35f, 9f, 50f), 1e-3f);
        }

        // ── 그림 진행(최종 리뷰 M1) ─────────────────────────

        [Test]
        public void 그림_진행은_이동_시작_전이면_앞_칸이고_그_뒤로는_고르게_간다()
        {
            var s = new CardRushRunnerState { Step = 5, MoveStartTick = 1000 };
            Assert.AreEqual(4f, CardRushCourseLayout.DrawnProgress(s, 995, 20), Eps, "스냅이 앞서 와도 그림 틱엔 아직 칸 4");
            Assert.AreEqual(4f, CardRushCourseLayout.DrawnProgress(s, 1000, 20), Eps);
            Assert.AreEqual(4.5f, CardRushCourseLayout.DrawnProgress(s, 1010, 20), Eps);
            Assert.AreEqual(5f, CardRushCourseLayout.DrawnProgress(s, 1020, 20), Eps);
            Assert.AreEqual(0f, CardRushCourseLayout.DrawnProgress(new CardRushRunnerState(), 995, 20), Eps, "아직 한 칸도 안 맞혔다");
        }

        // ── 남의 몸 상태 적용(Task 8 리뷰에서 넘어온 빈칸) ────

        [Test]
        public void 남의_몸_스냅은_카드_러시_상태_다섯_칸을_넣는다()
        {
            var entity = new GameFramework.World.Entity("other");
            entity.Add(new CardRushRunner { State = new CardRushRunnerState { BestStreak = 9, Misses = 4 } });
            var snap = new EntitySnap
            {
                entityId = "other", cardStep = 7, cardAttempt = 2, cardMoveStartTick = 1234, cardStunEndTick = 1300, cardStreak = 3,
            };

            //  핸들러는 의존이 스무 개 넘는다 — 이 경로는 그중 아무것도 안 쓰므로 생성자 없이 만든다.
            var handler = (GameEntityMessageHandler)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(GameEntityMessageHandler));
            typeof(GameEntityMessageHandler).GetMethod("ApplyRemoteSnapState", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(handler, new object[] { entity, new global::EntitySnap(), snap });

            var s = entity.Get<CardRushRunner>().State;
            Assert.AreEqual(7, s.Step);
            Assert.AreEqual(2, s.Attempt);
            Assert.AreEqual(1234, s.MoveStartTick);
            Assert.AreEqual(1300, s.StunEndTick);
            Assert.AreEqual(3, s.Streak);
            Assert.AreEqual(9, s.BestStreak, "최고 연속은 지킨다");
            Assert.AreEqual(4, s.Misses, "틀린 수는 서버가 안 보낸다 — 그대로");
        }
    }
}
