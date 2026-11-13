using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 몸개그는 그림만이다(spec 2026-11-05 §11.3): 자식 Model만 움직이고 시작·끝 자세가 원래 자세라, 기절·대시가 끝나는 틱에
    /// 그림이 시뮬 자리로 돌아온다. 밀림은 시뮬에서 즉시 — 몸개그는 그 자리에서 넘어진다.
    /// </summary>
    public class CardRushGagTests
    {
        private static readonly CardRushTuning Tuning = CardRushServerCorrectionHandlerTests.Config.Tuning;

        private static void AssertRest(CardRushPose p, string label)
        {
            Assert.AreEqual(0f, p.Offset.magnitude, 1e-5f, label + " 오프셋");
            Assert.AreEqual(0f, p.EulerDeg.magnitude, 1e-5f, label + " 기울기");
            Assert.AreEqual(1f, p.Squash, 1e-5f, label + " 찌그러짐");
        }

        [Test]
        public void 몸개그는_처음과_끝에서_원래_자세([Values] CardRushGagKind kind)
        {
            AssertRest(CardRushGag.PoseAt(kind, 0f), kind + " t=0");
            AssertRest(CardRushGag.PoseAt(kind, 1f), kind + " t=1");
        }

        [Test]
        public void 끝_직전은_원래_자세에_가깝다_튀지_않는다([Values] CardRushGagKind kind)
        {
            var p = CardRushGag.PoseAt(kind, 0.999f);
            Assert.Less(p.Offset.magnitude, 0.05f, kind + " 오프셋");
            Assert.Less(Quaternion.Angle(Quaternion.Euler(p.EulerDeg), Quaternion.identity), 3f, kind + " 기울기");
            Assert.AreEqual(1f, p.Squash, 0.02f, kind + " 찌그러짐");
        }

        [Test]
        public void 꽈당_결과는_구르기_길이는_굴러오는_것_기절()
        {
            var s = new CardRushRunnerState { ObstacleEvent = (int)CardRushObstacleEvent.Tumbled, ObstacleEventTick = 2000, StunEndTick = 2000 + Tuning.RollStunTicks };
            var (k0, t0) = CardRushGag.Phase(s, 2000, Tuning);
            Assert.AreEqual(CardRushGagKind.Tumble, k0);
            Assert.AreEqual(0f, t0, 1e-6f);
            var (k, t) = CardRushGag.Phase(s, 2000 + Tuning.RollStunTicks * 0.5, Tuning);
            Assert.AreEqual(CardRushGagKind.Tumble, k);
            Assert.AreEqual(0.5f, t, 1e-6f);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(s, 2000 + Tuning.RollStunTicks, Tuning).kind, "기절이 끝나는 틱엔 없다");
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(s, 1999.5, Tuning).kind);
        }

        [Test]
        public void 구역에_맞으면_납작_길이는_구역_기절()
        {
            var s = new CardRushRunnerState { ZoneEvent = (int)CardRushZoneEvent.Hit, ZoneEventTick = 3000 };
            var (k, t) = CardRushGag.Phase(s, 3000 + Tuning.ZoneStunTicks * 0.25, Tuning);
            Assert.AreEqual(CardRushGagKind.Squash, k);
            Assert.AreEqual(0.25f, t, 1e-6f);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(s, 3000 + Tuning.ZoneStunTicks, Tuning).kind);
            Assert.AreEqual(CardRushGagKind.None,
                CardRushGag.Phase(new CardRushRunnerState { ZoneEvent = (int)CardRushZoneEvent.Held, ZoneEventTick = 3000 }, 3010, Tuning).kind, "버팀은 몸개그가 아니다");
        }

        [Test]
        public void 꽈당은_구르며_떠오르고_엉덩방아에_눌린다()
        {
            float roll = (CardRushGag.TumbleLeanEnd + CardRushGag.TumbleRollEnd) * 0.5f;
            var mid = CardRushGag.PoseAt(CardRushGagKind.Tumble, roll);
            var center = mid.Offset + Quaternion.Euler(mid.EulerDeg.x, 0f, 0f) * new Vector3(0f, CardRushGag.BodyCenterHeight, 0f);
            Assert.AreEqual(CardRushGag.BodyCenterHeight + CardRushGag.TumbleRiseHeight, center.y, 1e-3f, "구르는 가운데에 몸 가운데가 0.6 m 뜬다");
            Assert.Less(mid.EulerDeg.x, -CardRushGag.TumbleLeanDegrees, "뒤로 구른다");
            var sit = CardRushGag.PoseAt(CardRushGagKind.Tumble, 0.75f);
            Assert.AreEqual(CardRushGag.TumbleSitSquash, sit.Squash, 1e-4f);
            Assert.AreEqual(-20f, Mathf.DeltaAngle(0f, sit.EulerDeg.x), 1e-3f, "뒤로 기운 채 앉는다");
        }

        [Test]
        public void 으악은_납작했다가_퐁_튀어_오른다()
        {
            Assert.AreEqual(CardRushGag.SquashFlat, CardRushGag.PoseAt(CardRushGagKind.Squash, 0.4f).Squash, 1e-5f);
            var pop = CardRushGag.PoseAt(CardRushGagKind.Squash, CardRushGag.SquashPopEnd - 0.001f);
            Assert.Greater(pop.Squash, 1f, "튀어 오르며 넘친다");
            Assert.Greater(pop.Offset.y, 0.3f);
        }

        [Test]
        public void 대시_던지기_기다림은_상태에서_정한다()
        {
            var dash = new CardRushRunnerState { DashStartTick = 1000 };
            Assert.AreEqual(CardRushGagKind.Dash, CardRushGag.Phase(dash, 1005, Tuning).kind);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(dash, 1000 + CardRushRules.DashTicks, Tuning).kind);
            Assert.Greater(CardRushGag.PoseAt(CardRushGagKind.Dash, 0.5f).EulerDeg.x, 19f, "앞으로 20° 숙임");

            var whiff = new CardRushRunnerState { WhiffTick = 1000 };
            //  헛방 틱은 꺼진 틱 — 던진 틱은 27틱 앞(973)이라 헛방 틱 때는 이미 던지는 몸짓이 끝났다.
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(whiff, 1005, Tuning).kind);
            int thrownAt = 1000 - CardRushHazardLayout.WhiffFlightTicks;
            Assert.AreEqual(CardRushGagKind.Throw, CardRushGag.Phase(whiff, thrownAt + 5, Tuning).kind, "헛방도 던지는 몸짓(던진 틱부터)");
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(whiff, thrownAt + CardRushGag.ThrowTicks, Tuning).kind);

            var flying = new CardRushRunnerState { ShotTick = 1500 };
            var (tk, tt) = CardRushGag.Phase(flying, 1505, Tuning);
            Assert.AreEqual(CardRushGagKind.Throw, tk, "날고 있는 투사체는 던진 틱 그대로");
            Assert.AreEqual(0.5f, tt, 1e-6f);
            Assert.Greater(CardRushGag.PoseAt(CardRushGagKind.Throw, 0.35f).EulerDeg.x, 14f, "앞으로 숙임");

            //  이미 맞힌 투사체: 던진 틱 = 쿨다운 틱(SpinStartTick) − 20. 28틱 넘게 벌어지면(다른 사용이 덮은 값) 던진 걸로 안 친다.
            var smashed = new CardRushRunnerState { ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 2000, SpinStartTick = 2015 };
            Assert.AreEqual(1995, CardRushGag.ThrowTick(smashed));
            smashed.SpinStartTick = 2000 + 500;
            Assert.AreEqual(0, CardRushGag.ThrowTick(smashed), "쿨다운 틱이 그 뒤 다른 사용으로 덮였다");

            var track = CardRushTrack.ForTests(140000, new[] { new CardRushObstacle(CardRushObstacleKind.Barricade, 20000, 0) }, new CardRushZone[0], 1);
            var waiting = new CardRushRunnerState { ObstacleTick = 1000, PosMm = 19510 };
            Assert.AreEqual(CardRushGagKind.Wait, CardRushGag.Phase(waiting, 1030, Tuning, track).kind);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(waiting, 1030, Tuning).kind, "코스 없이는 바리케이드인지 모른다");
        }

        [Test]
        public void 완주하면_1초_몸짓()
        {
            var s = new CardRushRunnerState { FinishTick = 4000, PosMm = 140000 };
            var (k, t) = CardRushGag.Phase(s, 4025, Tuning);
            Assert.AreEqual(CardRushGagKind.Finish, k);
            Assert.AreEqual(0.5f, t, 1e-6f);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(s, 4000 + CardRushGag.FinishTicks, Tuning).kind);
            Assert.Greater(CardRushGag.PoseAt(CardRushGagKind.Finish, 0.25f).Offset.y, 0.7f, "첫 번째 뜀");
        }

        // ── 4차 ──

        [Test]
        public void 구덩이에_빠지면_꽈당_짧은_판_길이는_빠짐_기절()
        {
            var s = new CardRushRunnerState { ObstacleEvent = (int)CardRushObstacleEvent.Fell, ObstacleEventTick = 2000 };
            var (k, t) = CardRushGag.Phase(s, 2000 + Tuning.PitStunTicks * 0.5, Tuning);
            Assert.AreEqual(CardRushGagKind.Tumble, k);
            Assert.AreEqual(0.5f, t, 1e-6f);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(s, 2000 + Tuning.PitStunTicks, Tuning).kind, "기절이 끝나는 틱엔 원래 자세");
            AssertRest(CardRushGag.PoseAt(k, 0f), "시작");
            AssertRest(CardRushGag.PoseAt(k, 1f), "끝");
        }

        [Test]
        public void 점프는_뛸_때와_내려앉을_때_004초_찌그러진다()
        {
            long start = 3000;
            var air = new CardRushRunnerState { JumpStartTick = start };
            var (k, t) = CardRushGag.Phase(air, start + 2, Tuning);
            Assert.AreEqual(CardRushGagKind.Jump, k, "뛸 때");
            Assert.AreEqual(0.5f, t, 1e-6f);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(air, start + CardRushGag.JumpSquashTicks, Tuning).kind, "공중에선 원래 자세");
            Assert.AreEqual(CardRushGagKind.Jump, CardRushGag.Phase(air, CardRushGag.LandTick(start) + 1, Tuning).kind, "내려앉기 직전 상태가 아직 점프");

            //  내려앉으면 상태가 점프 틱을 지운다 — 그림이 기억한 틱으로 그린다.
            var landed = new CardRushRunnerState();
            long land = CardRushGag.LandTick(start);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(landed, land + 1, Tuning).kind, "기억 없이는 못 그린다");
            var (lk, lt) = CardRushGag.Phase(landed, land + 2, Tuning, null, start);
            Assert.AreEqual(CardRushGagKind.Jump, lk, "내려앉을 때");
            Assert.AreEqual(0.5f, lt, 1e-6f);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(landed, land + CardRushGag.JumpSquashTicks, Tuning, null, start).kind);
            Assert.AreEqual(39 + start, land, "점프 40틱 — 시뮬이 40번째 틱에 땅으로 되돌린다");

            var mid = CardRushGag.PoseAt(CardRushGagKind.Jump, 0.4f);
            Assert.AreEqual(CardRushGag.JumpSquash, mid.Squash, 1e-5f, "가장 눌렸을 때");
            Assert.AreEqual(0f, mid.Offset.magnitude, 1e-6f, "자리는 안 옮긴다 — 높이는 시뮬 자리에 이미 있다");
        }

        [Test]
        public void 던지기와_점프는_던지기가_먼저_보인다()
        {
            var s = new CardRushRunnerState { ShotTick = 1000, JumpStartTick = 1001 };
            Assert.AreEqual(CardRushGagKind.Throw, CardRushGag.Phase(s, 1002, Tuning).kind);
            Assert.AreEqual(CardRushGagKind.None, CardRushGag.Phase(s, 1010, Tuning).kind, "둘 다 끝난 뒤엔 원래 자세");
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

        //  꽈당은 몸 가운데를 축으로 구른다 — 발목 축이면 머리가 땅을 쓸며 큰 원을 돌아 구르기로 안 읽힌다.
        [Test]
        public void 꽈당_구르기는_몸_가운데가_제자리()
        {
            for (int i = 1; i < 100; i++)
            {
                float t = CardRushGag.TumbleLeanEnd + (CardRushGag.TumbleRollEnd - CardRushGag.TumbleLeanEnd) * i / 100f;
                var p = CardRushGag.PoseAt(CardRushGagKind.Tumble, t);
                var center = p.Offset + Quaternion.Euler(p.EulerDeg.x, 0f, 0f) * new Vector3(0f, CardRushGag.BodyCenterHeight, 0f);
                Assert.AreEqual(0f, center.z, 1e-3f, "t=" + t + " 앞뒤");
                Assert.GreaterOrEqual(center.y, CardRushGag.BodyCenterHeight - 1e-3f, "t=" + t + " 땅으로 꺼지지 않는다");
            }
        }

        [Test]
        public void 별은_엉덩방아와_납작_동안만()
        {
            var parent = new GameObject("fx-parent");
            var materials = new CardRushGagFxMaterials();
            try
            {
                var fx = new CardRushGagFx(parent.transform, "fx", materials);
                fx.Show(CardRushGagKind.Tumble, 0.3f, Vector3.up);
                Assert.AreEqual(0, fx.ActiveStars, "구르는 중엔 아직 없다");
                fx.Show(CardRushGagKind.Tumble, 0.7f, Vector3.up);
                Assert.AreEqual(CardRushGagFx.StarCount, fx.ActiveStars, "엉덩방아");
                fx.Show(CardRushGagKind.Tumble, 0.9f, Vector3.up);
                Assert.AreEqual(0, fx.ActiveStars, "벌떡 일어설 땐 없다");
                fx.Show(CardRushGagKind.Squash, 0.4f, Vector3.up);
                Assert.AreEqual(CardRushGagFx.StarCount, fx.ActiveStars, "납작");
                fx.Show(CardRushGagKind.Dash, 0.4f, Vector3.up);
                Assert.AreEqual(0, fx.ActiveStars);
            }
            finally
            {
                materials.Dispose();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
