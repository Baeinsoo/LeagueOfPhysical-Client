using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 몸개그는 그림만이다(spec §7.4): 시작·끝 자세가 원래 자세라 멈춤·이동이 끝나는 틱에 그림이 시뮬 자리로 돌아온다.
    /// 소리는 내 몸 상태가 바뀐 것만 듣는다(spec §7.5).
    /// </summary>
    public class CardRushGagTests
    {
        private const float Spacing = 2.2f;
        private static readonly CardRushTiming Timing = new CardRushTiming(15, 30);

        private static void AssertRest(CardRushPose p, string label)
        {
            Assert.AreEqual(0f, p.Offset.magnitude, 1e-5f, label + " 오프셋");
            Assert.AreEqual(0f, p.EulerDeg.magnitude, 1e-5f, label + " 기울기");
            Assert.AreEqual(1f, p.Squash, 1e-5f, label + " 찌그러짐");
        }

        [Test]
        public void 몸개그는_처음과_끝에서_원래_자세([Values] CardRushGagKind kind)
        {
            AssertRest(CardRushGag.PoseAt(kind, 0f, Spacing), kind + " t=0");
            AssertRest(CardRushGag.PoseAt(kind, 1f, Spacing), kind + " t=1");
        }

        [Test]
        public void 멈춤_끝_직전은_원래_자세에_가깝다_튀지_않는다(
            [Values(CardRushGagKind.Splash, CardRushGagKind.Slap, CardRushGagKind.DoorRun, CardRushGagKind.Finish)] CardRushGagKind kind)
        {
            var p = CardRushGag.PoseAt(kind, 0.999f, Spacing);
            Assert.Less(p.Offset.magnitude, 0.05f, kind + " 오프셋");
            var q = Quaternion.Euler(p.EulerDeg);
            Assert.Less(Quaternion.Angle(q, Quaternion.identity), 3f, kind + " 기울기");
            Assert.AreEqual(1f, p.Squash, 1e-3f);
        }

        [Test]
        public void 풍덩은_가운데에서_물속_두_돌_사이()
        {
            var p = CardRushGag.PoseAt(CardRushGagKind.Splash, 0.6f, Spacing);
            Assert.AreEqual(-CardRushGag.SplashDepth, p.Offset.y, CardRushGag.SplashBob + 1e-3f, "y −1.1 근처(허우적 까딱임만큼)");
            //  제 돌 아래로 빠지면 돌이 몸을 가른다 — 반 칸 앞(두 돌 사이)에 빠진다.
            Assert.AreEqual(0.5f * Spacing, p.Offset.z, 1e-4f);
            Assert.Greater(CardRushGag.PoseAt(CardRushGagKind.Splash, 0.125f, Spacing).Offset.y, 0.3f, "처음엔 앞 돌 쪽으로 뛰어오른다");
        }

        [Test]
        public void 철썩은_카드가_닿을_때_젖혀지고_가운데에서_뒤로_누움()
        {
            var p = CardRushGag.PoseAt(CardRushGagKind.Slap, 0.55f, 3.0f);
            Assert.LessOrEqual(p.EulerDeg.x, -70f);
            Assert.AreEqual(0f, p.Offset.magnitude, 1e-5f, "발은 제자리 — 앞뒤로 안 간다");
            Assert.Greater(CardRushGag.PoseAt(CardRushGagKind.Slap, 0.05f, 3.0f).EulerDeg.x, 5f, "던지며 앞으로 숙인다");
            //  철썩 0.23 = 던짐 + SlapTick(7) ÷ 멈춤 30틱 — 카드가 얼굴에 닿는 틱에 머리가 젖혀지고 찌그러진다.
            Assert.AreEqual(0.23f, (float)CardRushThrowFx.SlapTick / Timing.StunTicks, 0.01f);
            var hit = CardRushGag.PoseAt(CardRushGagKind.Slap, 0.27f, 3.0f);
            Assert.Less(hit.EulerDeg.x, -15f);
            Assert.AreEqual(0.9f, hit.Squash, 1e-5f);
            Assert.AreEqual(1f, CardRushGag.PoseAt(CardRushGagKind.Slap, 0.2f, 3.0f).Squash, 1e-5f, "닿기 전엔 멀쩡");
        }

        [Test]
        public void 문_틀림은_철썩()
        {
            var s = new CardRushRunnerState { StunEndTick = 3030, Attempt = 1 };
            Assert.AreEqual(CardRushGagKind.Slap, CardRushGag.Phase(CardRushTheme.DoorDash, s, 3010, new CardRushTiming(15, 30), 40).kind);
            Assert.AreEqual(CardRushGagKind.Splash, CardRushGag.Phase(CardRushTheme.StoneBridge, s, 3010, new CardRushTiming(15, 30), 40).kind, "돌다리 그림은 남겨 둔다");
        }

        [Test]
        public void 틀림_몸개그는_몸_뒤로_안_간다_카메라_아래로_안_사라진다(
            [Values(CardRushGagKind.Splash, CardRushGagKind.Slap)] CardRushGagKind kind)
        {
            //  카메라가 몸 바로 뒤 위에 있어 몸 뒤쪽 땅은 화면 밖이다 — 뒤로 가면 내 몸개그가 안 보인다.
            for (int i = 0; i <= 100; i++)
            {
                Assert.GreaterOrEqual(CardRushGag.PoseAt(kind, i / 100f, 3.0f).Offset.z, 0f, kind + " t=" + i / 100f);
            }
        }

        [Test]
        public void 멈춤은_시작_틱에_0_끝_틱_직전에_1_근처_끝_틱에_없음()
        {
            var s = new CardRushRunnerState { Step = 3, MoveStartTick = 900, StunEndTick = 1030 };
            var (k0, t0) = CardRushGag.Phase(CardRushTheme.StoneBridge, s, 1000, Timing, 40);
            Assert.AreEqual(CardRushGagKind.Splash, k0);
            Assert.AreEqual(0f, t0, 1e-6f);

            var (k1, t1) = CardRushGag.Phase(CardRushTheme.StoneBridge, s, 1029.5, Timing, 40);
            Assert.AreEqual(CardRushGagKind.Splash, k1);
            Assert.Greater(t1, 0.98f);

            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(CardRushTheme.StoneBridge, s, 1030, Timing, 40).kind);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(CardRushTheme.StoneBridge, s, 999.9, Timing, 40).kind);
            Assert.AreEqual(CardRushGagKind.Slap, CardRushGag.Phase(CardRushTheme.DoorDash, s, 1010, Timing, 40).kind);
        }

        [Test]
        public void 이동은_폴짝_문은_달려_나감()
        {
            var s = new CardRushRunnerState { Step = 4, MoveStartTick = 1000 };
            var (k, t) = CardRushGag.Phase(CardRushTheme.StoneBridge, s, 1007.5, Timing, 40);
            Assert.AreEqual(CardRushGagKind.Hop, k);
            Assert.AreEqual(0.5f, t, 1e-6f);
            Assert.AreEqual(CardRushGag.HopHeight, CardRushGag.PoseAt(k, t, Spacing).Offset.y, 1e-4f, "가운데에서 0.8 m");
            Assert.AreEqual(CardRushGagKind.DoorRun, CardRushGag.Phase(CardRushTheme.DoorDash, s, 1007.5, Timing, 40).kind);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(CardRushTheme.DoorDash, s, 1015, Timing, 40).kind, "이동 끝 틱엔 없음");
        }

        [Test]
        public void 완주하면_결승_이동이_끝난_뒤_1초_몸짓()
        {
            var s = new CardRushRunnerState { Step = 40, MoveStartTick = 1000 };
            Assert.AreEqual(CardRushGagKind.Hop, CardRushGag.Phase(CardRushTheme.StoneBridge, s, 1010, Timing, 40).kind, "결승 칸으로 가는 폴짝 먼저");
            var (k, t) = CardRushGag.Phase(CardRushTheme.StoneBridge, s, 1015 + 25, Timing, 40);
            Assert.AreEqual(CardRushGagKind.Finish, k);
            Assert.AreEqual(0.5f, t, 1e-6f);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(CardRushTheme.StoneBridge, s, 1015 + CardRushGag.FinishTicks, Timing, 40).kind);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(CardRushTheme.StoneBridge, new CardRushRunnerState { Step = 39, MoveStartTick = 1000 }, 1040, Timing, 40).kind,
                "완주 전엔 결승 몸짓이 없다");
            Assert.Greater(CardRushGag.PoseAt(CardRushGagKind.Finish, 0.25f, Spacing).Offset.y, 0.7f, "첫 번째 뜀");
        }

        // ── 자세를 모델에 주기 ──

        [Test]
        public void 자세는_진행_방향_기준이고_원래_자세로_정확히_돌아온다()
        {
            var visual = new GameObject("visual");
            try
            {
                var model = new GameObject("Model").transform;
                model.SetParent(visual.transform, false);
                model.localPosition = new Vector3(0f, 0.1f, 0f);
                model.localRotation = Quaternion.Euler(0f, 180f, 0f);
                visual.transform.SetPositionAndRotation(new Vector3(5f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));
                var rest = new CardRushRunnerFxView.ModelRest(model);
                var forward = Vector3.right;   // 레인이 +x로 간다
                var before = model.position;

                CardRushRunnerFxView.ApplyPose(visual.transform, model, rest, new CardRushPose(new Vector3(0f, -1f, 2f), new Vector3(-80f, 0f, 0f), 0.85f), forward);
                var moved = model.position - before;
                Assert.AreEqual(2f, moved.x, 1e-4f, "앞(z)은 레인 방향 +x");
                Assert.AreEqual(-1f, moved.y, 1e-4f);
                Assert.AreEqual(0f, moved.z, 1e-4f);
                Assert.Less(Vector3.Dot(model.up, forward), -0.9f, "뒤로 80° 누우면 머리가 레인 뒤쪽");
                Assert.AreEqual(0.85f, model.localScale.y, 1e-4f);
                Assert.AreEqual(1f / Mathf.Sqrt(0.85f), model.localScale.x, 1e-4f);

                CardRushRunnerFxView.ApplyPose(visual.transform, model, rest, CardRushPose.Rest, forward);
                Assert.AreEqual(rest.Position, model.localPosition);
                Assert.AreEqual(0f, Quaternion.Angle(rest.Rotation, model.localRotation), 1e-3f);
                Assert.AreEqual(rest.Scale, model.localScale);
            }
            finally
            {
                Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void 물보라는_풍덩_구간에만_별은_나자빠질_때만()
        {
            var parent = new GameObject("fx-parent");
            var materials = new CardRushGagFxMaterials();
            try
            {
                var fx = new CardRushGagFx(parent.transform, "fx", materials);
                fx.Show(CardRushGagKind.Splash, 0.3f, Vector3.zero, Vector3.forward, Spacing, Vector3.up);
                Assert.AreEqual(CardRushGagFx.DropletCount, fx.ActiveDroplets, "빠지는 순간 튄다");
                Assert.AreEqual(0, fx.ActiveStars);

                fx.Show(CardRushGagKind.Splash, 0.1f, Vector3.zero, Vector3.forward, Spacing, Vector3.up);
                Assert.AreEqual(0, fx.ActiveDroplets, "뛰어오르는 중엔 아직 없다");

                fx.Show(CardRushGagKind.Slap, 0.25f, Vector3.zero, Vector3.forward, 3f, Vector3.up);
                Assert.AreEqual(0, fx.ActiveStars, "철썩 맞기 전엔 별이 없다");

                fx.Show(CardRushGagKind.Slap, 0.5f, Vector3.zero, Vector3.forward, 3f, Vector3.up);
                Assert.AreEqual(CardRushGagFx.StarCount, fx.ActiveStars);
                Assert.AreEqual(0, fx.ActiveDroplets);

                fx.Show(CardRushGagKind.Slap, 0.9f, Vector3.zero, Vector3.forward, 3f, Vector3.up);
                Assert.AreEqual(0, fx.ActiveStars, "벌떡 일어설 땐 별이 없다");
                fx.Show(CardRushGagKind.None, 0f, Vector3.zero, Vector3.forward, 3f, Vector3.up);
                Assert.AreEqual(0, fx.ActiveStars + fx.ActiveDroplets);
            }
            finally
            {
                materials.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        //  최종 리뷰 m4: 철썩(머리 젖힘·찌그러짐)은 카드가 얼굴에 닿는 틱(던짐 + SlapTick) — 멈춤 틱 수는 설정에서 온다.
        [Test]
        public void 철썩_순간은_설정의_멈춤_틱으로_정한다()
        {
            Assert.AreEqual(7f / 40f, CardRushGag.SlapHit01(40), 1e-6f);
            Assert.AreEqual(7f / 30f, CardRushGag.SlapHit01(30), 1e-6f);
            float justAfter40 = 7f / 40f + 0.01f;
            Assert.AreEqual(0.9f, CardRushGag.PoseAt(CardRushGagKind.Slap, justAfter40, 3f, 40).Squash, 1e-5f, "멈춤 40틱이면 0.185에 찌그러짐");
            Assert.AreEqual(1f, CardRushGag.PoseAt(CardRushGagKind.Slap, justAfter40, 3f, 30).Squash, 1e-5f, "멈춤 30틱이면 아직 안 맞았다");
            Assert.AreEqual(0.9f, CardRushGag.PoseAt(CardRushGagKind.Slap, 7f / 30f + 0.01f, 3f, 30).Squash, 1e-5f);
        }

        //  최종 리뷰 m2: 보정이 틀림을 맞음으로 바꾸거나 던짐 틱을 옮기면 같은 던짐이다 — 휙·탁이 두 번 나지 않는다.
        [Test]
        public void 보정으로_바뀐_같은_던짐은_다시_울리지_않는다()
        {
            var mem = new CardRushAudioMemory();
            var cues = new List<CardRushSfx>();
            var waiting = new CardRushAudioFrame { Has = true, Step = 3, StunTicks = 30, MoveTicks = 15, DrawnTick = 2999 };
            var predictedMiss = waiting; predictedMiss.StunEndTick = 3030; predictedMiss.DrawnTick = 3000;
            CardRushAudioCues.Detect(waiting, predictedMiss, CardRushTheme.DoorDash, cues, ref mem);
            CollectionAssert.AreEqual(new[] { CardRushSfx.Whoosh }, cues);

            cues.Clear();
            var correctedHit = waiting; correctedHit.Step = 4; correctedHit.MoveStartTick = 3001; correctedHit.DrawnTick = 3002;
            CardRushAudioCues.Detect(predictedMiss, correctedHit, CardRushTheme.DoorDash, cues, ref mem);
            CollectionAssert.DoesNotContain(cues, CardRushSfx.Whoosh, "같은 던짐 — 휙은 한 번");

            cues.Clear();
            var landed = correctedHit; landed.DrawnTick = 3004;
            CardRushAudioCues.Detect(correctedHit, landed, CardRushTheme.DoorDash, cues, ref mem);
            CollectionAssert.AreEqual(new[] { CardRushSfx.Thunk }, cues);

            //  보정이 맞음 틱을 3001 → 3003으로 옮겨 착지를 다시 지나도 탁은 한 번.
            cues.Clear();
            var moved = landed; moved.MoveStartTick = 3003; moved.DrawnTick = 3006;
            CardRushAudioCues.Detect(landed, moved, CardRushTheme.DoorDash, cues, ref mem);
            CollectionAssert.IsEmpty(cues);

            //  다음 진짜 던짐(이동 15틱 + 시작 1틱 뒤)은 운다.
            cues.Clear();
            var next = moved; next.Step = 5; next.MoveStartTick = 3020; next.DrawnTick = 3020;
            CardRushAudioCues.Detect(moved, next, CardRushTheme.DoorDash, cues, ref mem);
            CollectionAssert.Contains(cues, CardRushSfx.Whoosh);
        }

        // ── 소리 ──

        private static CardRushAudioFrame Frame(int step = 3, long stunEnd = 0, bool faceUp = true, CardColor color = CardColor.Red, bool finished = false)
            => new CardRushAudioFrame { Has = true, Step = step, StunEndTick = stunEnd, FaceUp = faceUp, Color = color, Finished = finished };

        private static List<CardRushSfx> Cues(CardRushAudioFrame prev, CardRushAudioFrame now, CardRushTheme theme = CardRushTheme.StoneBridge)
        {
            var list = new List<CardRushSfx>();
            CardRushAudioCues.Detect(prev, now, theme, list);
            return list;
        }

        [Test]
        public void 카드_색이_바뀌면_째깍_같으면_조용()
        {
            CollectionAssert.AreEqual(new[] { CardRushSfx.CardTick }, Cues(Frame(color: CardColor.Red), Frame(color: CardColor.Yellow)));
            CollectionAssert.IsEmpty(Cues(Frame(color: CardColor.Red), Frame(color: CardColor.Red)));
            CollectionAssert.IsEmpty(Cues(Frame(color: CardColor.Red), Frame(faceUp: false)), "뒷면이 되는 건 소리 없음");
            CollectionAssert.AreEqual(new[] { CardRushSfx.CardTick }, Cues(Frame(faceUp: false), Frame(color: CardColor.Red)), "새 카드가 앞면이 되면 째깍");
        }

        [Test]
        public void 던지면_휙_돌다리_틀림은_풍덩_완주는_팡파르()
        {
            CollectionAssert.AreEqual(new[] { CardRushSfx.Whoosh }, Cues(Frame(step: 3), Frame(step: 4, faceUp: false)));
            CollectionAssert.AreEqual(new[] { CardRushSfx.Splash }, Cues(Frame(stunEnd: 0), Frame(stunEnd: 1030, faceUp: false)));
            CollectionAssert.AreEqual(new[] { CardRushSfx.Whoosh }, Cues(Frame(stunEnd: 0), Frame(stunEnd: 1030, faceUp: false), CardRushTheme.DoorDash));
            CollectionAssert.AreEqual(new[] { CardRushSfx.Fanfare }, Cues(Frame(step: 39), Frame(step: 40, faceUp: false, finished: true)));
            CollectionAssert.IsEmpty(Cues(default, Frame(step: 10, stunEnd: 500)), "첫 장면은 기준만 — 지난 일로 소리 내지 않음");
        }

        [Test]
        public void 던지면_휙_꽂히면_탁_한_번()
        {
            var cues = new System.Collections.Generic.List<CardRushSfx>();
            var a = new CardRushAudioFrame { Has = true, Step = 3, MoveStartTick = 1000, DrawnTick = 1999 };
            var b = new CardRushAudioFrame { Has = true, Step = 4, MoveStartTick = 2000, DrawnTick = 2000 };
            CardRushAudioCues.Detect(a, b, CardRushTheme.DoorDash, cues);
            CollectionAssert.Contains(cues, CardRushSfx.Whoosh); CollectionAssert.DoesNotContain(cues, CardRushSfx.Thunk);
            cues.Clear();
            var c = b; c.DrawnTick = 2003;
            CardRushAudioCues.Detect(b, c, CardRushTheme.DoorDash, cues);
            CollectionAssert.AreEqual(new[] { CardRushSfx.Thunk }, cues);
            cues.Clear();
            var d = c; d.DrawnTick = 2010;
            CardRushAudioCues.Detect(c, d, CardRushTheme.DoorDash, cues);
            CollectionAssert.IsEmpty(cues, "이미 지난 꽂힘은 다시 안 낸다");
        }

        [Test]
        public void 틀리면_휙_뒤_7틱에_철썩()
        {
            var cues = new System.Collections.Generic.List<CardRushSfx>();
            var a = new CardRushAudioFrame { Has = true, Step = 3, StunEndTick = 0, StunTicks = 30, DrawnTick = 2999 };
            var b = new CardRushAudioFrame { Has = true, Step = 3, StunEndTick = 3030, StunTicks = 30, DrawnTick = 3000 };
            CardRushAudioCues.Detect(a, b, CardRushTheme.DoorDash, cues);
            CollectionAssert.AreEqual(new[] { CardRushSfx.Whoosh }, cues);
            cues.Clear();
            var c = b; c.DrawnTick = 3007.2;
            CardRushAudioCues.Detect(b, c, CardRushTheme.DoorDash, cues);
            CollectionAssert.AreEqual(new[] { CardRushSfx.Slap }, cues);
            cues.Clear();
            var d = c; d.DrawnTick = 3020;
            CardRushAudioCues.Detect(c, d, CardRushTheme.StoneBridge, cues);
            CollectionAssert.IsEmpty(cues);
            CardRushAudioCues.Detect(b, c, CardRushTheme.StoneBridge, cues);
            CollectionAssert.IsEmpty(cues, "돌다리는 풍덩 하나 — 철썩 없음");
        }

        [Test]
        public void 소리는_정한_크기로_나고_연속이_길어도_같은_휙()
        {
            var audio = new CardRushAudio(null, null, null, null, null);
            var played = new List<(AudioClip clip, float volume)>();
            audio.playOneShot = (c, v) => played.Add((c, v));
            try
            {
                audio.Observe(Frame(step: 3, faceUp: false));
                audio.Observe(Frame(step: 3, color: CardColor.Blue));
                Assert.AreEqual(1, played.Count);
                Assert.AreSame(audio.ClipOf(CardRushSfx.CardTick), played[0].clip);
                Assert.AreEqual(Mathf.Pow(10f, -18f / 20f), played[0].volume, 1e-4f, "째깍 −18 dB");

                audio.Observe(Frame(step: 4, faceUp: false));
                audio.Observe(Frame(step: 30, faceUp: false));
                Assert.AreEqual(3, played.Count);
                Assert.AreSame(audio.ClipOf(CardRushSfx.Whoosh), played[1].clip);
                Assert.AreSame(played[1].clip, played[2].clip, "연속이 길어져도 음을 올리지 않는다");
                Assert.AreEqual(played[1].volume, played[2].volume);
                Assert.AreEqual(Mathf.Pow(10f, -10f / 20f), played[1].volume, 1e-4f, "휙 −10 dB");

                audio.Play(CardRushSfx.Thunk);
                audio.Play(CardRushSfx.Slap);
                Assert.AreEqual(Mathf.Pow(10f, -8f / 20f), played[3].volume, 1e-4f, "탁 −8 dB");
                Assert.AreEqual(Mathf.Pow(10f, -6f / 20f), played[4].volume, 1e-4f, "철썩 −6 dB");
            }
            finally
            {
                audio.Dispose();
            }
        }

        [Test]
        public void 공용_경기_화면_소리도_나고_완주_팡파르는_한_번만()
        {
            var audio = new CardRushAudio(null, null, null, null, null);
            var played = new List<AudioClip>();
            audio.playOneShot = (c, v) => played.Add(c);
            try
            {
                audio.Play(FlappySfx.RankUp);
                audio.Play(FlappySfx.Stamp);
                audio.Play(FlappySfx.Finish);
                Assert.AreEqual(2, played.Count, "결승 팡파르는 상태에서 이미 났다 — 경기 화면의 Finish는 삼킨다");
                Assert.AreSame(audio.ClipOf(FlappySfx.RankUp), played[0]);
                Assert.AreEqual(FlappySynth.Render(FlappySfx.RankUp).Length, played[0].samples, "플래피와 같은 클립");
                Assert.IsNotNull(played[1]);
            }
            finally
            {
                audio.Dispose();
            }
        }

        [Test]
        public void 소리는_다_지어진다_철썩_뒤_뾰잉은_벌떡_서는_순간()
        {
            var audio = new CardRushAudio(null, null, null, null, null);
            try
            {
                foreach (CardRushSfx sfx in System.Enum.GetValues(typeof(CardRushSfx)))
                {
                    var clip = audio.ClipOf(sfx);
                    Assert.IsNotNull(clip, sfx.ToString());
                    Assert.Greater(clip.samples, 0, sfx.ToString());
                }
                //  뾰잉은 몸개그가 벌떡 서는 순간(멈춤의 0.75 = 0.45초)에. 클립은 철썩(던짐 + 7틱 = 0.14초)에 시작하니 클립 안에선 0.31초.
                var slap = CardRushAudio.Recipe(CardRushSfx.Slap, 0.6f);
                Assert.AreEqual(0.31f, slap[slap.Length - 1].When, 1e-4f);

                //  휙은 대역 가운데를 1.2 → 3 kHz로 0.12초에 훑는다(겹마다 가운데가 오른다).
                var whoosh = CardRushAudio.Recipe(CardRushSfx.Whoosh, 0.6f);
                Assert.AreEqual(1200f, whoosh[0].Freq, 1e-2f);
                Assert.AreEqual(3000f, whoosh[whoosh.Length - 1].Freq, 1e-2f);
                for (int i = 1; i < whoosh.Length; i++) Assert.Greater(whoosh[i].Freq, whoosh[i - 1].Freq);
                Assert.AreEqual(0.12f, whoosh[whoosh.Length - 1].When + 0.12f / whoosh.Length, 1e-4f, "마지막 겹이 0.12초에 맞춰 시작");
            }
            finally
            {
                audio.Dispose();
            }
        }
    }
}
