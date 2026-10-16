using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Hand = LOP.UI.ArcheryDrawHand;
    using Style = LOP.UI.ArcheryDrawStyle;

    //  오른손(당기는 손)의 두 방식.
    //  - 누르기: 누르면 활이 올라오고, 누른 채 움직이면 겨눈다.
    //  - 끌기: 눌러서 **아래로 끌어야** 활이 올라온다. 끄는 손은 겨누지 않는다(겨누기는 왼손).
    //  어느 쪽이든 화면이 보내는 것은 "당기고 있다"뿐이다 — 얼마나 빨리 당겨지는지는 시뮬이 정한다.
    public class ArcheryPadDrawHandTests
    {
        static readonly Vector2 Press = new Vector2(1.5f, 0.6f);
        static float Start => Hand.PullStartInHeights;
        static float PutDown => Hand.PullPutDownInHeights;

        static Vector2 Below(float heights) => Press - new Vector2(0f, heights);

        // ── 누르기 ───────────────────────────────────────────────

        [Test]
        public void 누르기_누르면_바로_당긴다()
        {
            var hand = new Hand(Style.Press);
            hand.Begin(Press);
            Assert.IsTrue(hand.Holding);
            Assert.IsTrue(hand.Drawing);
            Assert.AreEqual(1f, hand.DrawRatioToSend);
        }

        [Test]
        public void 누르기_당기는_손가락이_겨눈다()
        {
            var hand = new Hand(Style.Press);
            hand.Begin(Press);
            hand.Move(Press + new Vector2(0.1f, 0f));
            Assert.IsTrue(hand.FingerAims);
        }

        [Test]
        public void 누르기_아래로_끌어도_내려놓지_않는다()
        {
            var hand = new Hand(Style.Press);
            hand.Begin(Press);
            hand.Move(Below(0.2f));
            hand.Move(Press + new Vector2(0f, 0.2f));
            Assert.IsTrue(hand.Drawing, "누르기 방식인데 위아래로 겨눴더니 활이 내려갔다");
        }

        // ── 끌기 ─────────────────────────────────────────────────

        [Test]
        public void 끌기_누르기만_해서는_안_당긴다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            Assert.IsTrue(hand.Holding);
            Assert.IsFalse(hand.Drawing);
            Assert.AreEqual(0f, hand.DrawRatioToSend);
        }

        [Test]
        public void 끌기_문턱_전까지는_안_당긴다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            hand.Move(Below(Start * 0.9f));
            Assert.IsFalse(hand.Drawing);
        }

        [Test]
        public void 끌기_문턱을_넘겨_끌면_당기고_버티면_계속_당긴다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            hand.Move(Below(Start));
            Assert.IsTrue(hand.Drawing);
            Assert.AreEqual(1f, hand.DrawRatioToSend);

            //  더 끌어도, 가만히 있어도, 조금 올려도(문턱 사이) 그대로다.
            hand.Move(Below(0.3f));
            hand.Move(Below(0.3f));
            hand.Move(Below(Start * 0.8f));
            Assert.IsTrue(hand.Drawing, "문턱 근처에서 손이 떨리면 당김이 끊긴다 — 매번 0.5초를 새로 기다린다");
        }

        [Test]
        public void 끌기_위로_되돌리면_내려놓는다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            hand.Move(Below(0.2f));
            hand.Move(Below(PutDown * 0.5f));
            Assert.IsFalse(hand.Drawing, "위로 되돌렸는데 활이 그대로다 — 쏘지 않고 내려놓는 길이 없다");
            Assert.IsTrue(hand.Holding, "손가락은 아직 화면에 있다");
        }

        [Test]
        public void 끌기_내려놓았다가_다시_끌면_다시_당긴다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            hand.Move(Below(0.2f));
            hand.Move(Press);
            hand.Move(Below(0.2f));
            Assert.IsTrue(hand.Drawing);
        }

        [Test]
        public void 끌기_손가락은_겨누지_않는다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            hand.Move(Below(0.2f) + new Vector2(0.3f, 0f));
            Assert.IsTrue(hand.Drawing);
            Assert.IsFalse(hand.FingerAims, "끄는 손이 조준을 끌고 내려간다 — 끌 때마다 조준이 아래로 쏠린다");
        }

        //  옆으로만 움직이면 끌기가 아니다 — 세로로 끈 거리만 본다.
        [Test]
        public void 끌기_옆으로_움직여서는_안_당긴다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            hand.Move(Press + new Vector2(0.3f, 0f));
            Assert.IsFalse(hand.Drawing);
        }

        //  화면의 시위 그림(끈 거리)은 꾸밈이다 — 정해진 길이에서 멈춘다. 내려놓기 원까지
        //  닿아 보이면 "원까지 끌어야 만작"으로 읽혀 쏘려다 취소한다.
        [Test]
        public void 끌기_시위_그림은_정해진_길이에서_멈추고_내려놓기_원에_닿지_않는다()
        {
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);
            hand.Move(Below(1f));
            Assert.AreEqual(Hand.PullGaugeFullInHeights, hand.PullInHeights, 1e-5f);

            hand.Move(Press + new Vector2(0f, 0.2f));
            Assert.AreEqual(0f, hand.PullInHeights, 1e-5f, "위로 올린 만큼 그림이 거꾸로 늘어났다");

            float cancelInnerEdge = LOP.UI.ArcheryPadViewModel.CancelBelowPressInHeights
                                    - LOP.UI.ArcheryPadViewModel.CancelRadiusInHeights;
            Assert.Less(Hand.PullGaugeFullInHeights, cancelInnerEdge);
            Assert.Greater(Hand.PullGaugeFullInHeights, Start, "시위 그림이 문턱보다 짧다 — 당겼다는 게 안 보인다");
        }

        [Test]
        public void 끌기_문턱이_조작을_무력화하지_않는다()
        {
            Assert.Greater(Start, 0.02f, "문턱이 너무 낮다 — 누를 때 손가락이 밀리기만 해도 당긴다");
            Assert.Less(Start, 0.12f, "문턱이 너무 멀다 — 엄지를 한참 끌어야 활이 든다");
            Assert.Less(PutDown, Start, "내려놓는 선이 문턱보다 아래면 닿는 순간 다시 풀린다");
            Assert.Greater(PutDown, 0f);
        }

        [Test]
        public void 떼면_둘_다_끝난다()
        {
            foreach (var style in new[] { Style.Press, Style.Pull })
            {
                var hand = new Hand(style);
                hand.Begin(Press);
                hand.Move(Below(0.2f));
                hand.End();
                Assert.IsFalse(hand.Holding, style.ToString());
                Assert.IsFalse(hand.Drawing, style.ToString());
                Assert.AreEqual(0f, hand.DrawRatioToSend, style.ToString());
            }
        }

        // ── 공정성: 빨리 끈다고 빨리 당겨지지 않는다 ───────────────────

        const float TickInterval = 0.02f;   // 50Hz

        static ArcheryConfig NoSwayConfig() => new ArcheryConfig(
            wavePeriodTicks: 100, minTargets: 1, maxTargets: 1,
            spawnRadius: 1f, spawnMinY: 0f, spawnMaxY: 1f, minSeparation: 1f,
            trapRatioMin: 0f, trapRatioMax: 0f,
            shakeFreeSeconds: 1f, shakeRampSeconds: 1f, shakeMaxDegrees: 0f,
            riseHeightMin: 0.1f, riseHeightMax: 0.1f, staggerTicks: 1, restTicks: 1,
            kinds: null);

        //  손가락 길을 틱마다 하나씩 먹이고, **당김이 시작된 틱부터 만작까지 몇 틱**인지 센다.
        //  화면이 보내는 값만 시뮬에 넣는다 — 그래야 "화면이 고를 수 있는 크기가 없다"를 잰다.
        static int TicksFromDrawStartToFull(System.Func<int, float> pullAtTick)
        {
            var entity = new Entity("archer-1");
            entity.Add(new GameFramework.World.Transform { Position = Vector3.zero.ToNumerics() });
            entity.Add(new ArcheryAim());
            entity.Add(new InputBuffer());
            var system = new ArcheryAimSystem(NoSwayConfig());
            var hand = new Hand(Style.Pull);
            hand.Begin(Press);

            int startedAt = -1;
            for (int tick = 0; tick < 500; tick++)
            {
                hand.Move(Below(pullAtTick(tick)));
                entity.Get<InputBuffer>().Current = new InputCommand
                {
                    Drawing = hand.Drawing,
                    DrawRatio = hand.DrawRatioToSend,
                };
                system.Tick(entity, tick, TickInterval);

                var aim = entity.Get<ArcheryAim>();
                if (startedAt < 0 && aim.Drawing)
                {
                    startedAt = tick;
                }
                if (aim.DrawRatio >= 1f - 1e-4f)
                {
                    return tick - startedAt;
                }
            }
            Assert.Fail("만작이 안 됐다");
            return -1;
        }

        [Test]
        public void 끌기_빨리_끌어도_만작까지의_틱_수는_같다()
        {
            //  한 틱에 화면 끝까지 휙 / 천천히 문턱을 넘겨 더 끌기 / 문턱만 살짝 넘기고 버티기.
            int fast = TicksFromDrawStartToFull(_ => 0.9f);
            int slow = TicksFromDrawStartToFull(t => Mathf.Min(0.3f, t * 0.004f));
            int barely = TicksFromDrawStartToFull(t => Mathf.Min(Start * 1.01f, t * 0.004f));

            int expected = Mathf.RoundToInt(ArcheryAimSystem.FullDrawSeconds / TickInterval) - 1;
            Assert.AreEqual(expected, fast, "휙 끌었더니 만작이 빨라졌다 — 손 빠른 쪽이 이긴다");
            Assert.AreEqual(fast, slow, "끈 속도가 당김 속도를 바꾼다");
            Assert.AreEqual(fast, barely, "끈 거리가 당김 속도를 바꾼다");
        }

        [Test]
        public void 누르기와_끌기는_같은_값을_보낸다()
        {
            var press = new Hand(Style.Press);
            var pull = new Hand(Style.Pull);
            press.Begin(Press);
            pull.Begin(Press);
            pull.Move(Below(1f));
            Assert.AreEqual(press.DrawRatioToSend, pull.DrawRatioToSend,
                "두 방식이 다른 크기를 보낸다 — 고르는 방식에 따라 화살이 달라진다");
        }

        // ── 방식 고르기 ─────────────────────────────────────────

        [Test]
        public void 기본은_누르기다()
        {
            Assert.AreEqual(Style.Press, Hand.ResolveStyle(savedValue: -1, toggleAvailable: true));
            Assert.AreEqual(Style.Press, Hand.ResolveStyle(savedValue: 99, toggleAvailable: true));
        }

        [Test]
        public void 저장한_방식을_되살린다()
        {
            Assert.AreEqual(Style.Pull, Hand.ResolveStyle((int)Style.Pull, toggleAvailable: true));
        }

        //  토글이 안 보이는 빌드(출시)에서는 시험용 방식이 저장돼 있어도 누르기다 — 고를 길이 없는데
        //  다른 방식으로 굳어 있으면 되돌릴 수가 없다.
        [Test]
        public void 토글이_없는_빌드는_늘_누르기다()
        {
            Assert.AreEqual(Style.Press, Hand.ResolveStyle((int)Style.Pull, toggleAvailable: false));
        }

        [Test]
        public void 토글_글씨()
        {
            Assert.AreEqual("당기기: 누르기", Hand.LabelOf(Style.Press));
            Assert.AreEqual("당기기: 끌기", Hand.LabelOf(Style.Pull));
            Assert.AreEqual(Style.Pull, Hand.Next(Style.Press));
            Assert.AreEqual(Style.Press, Hand.Next(Style.Pull));
        }
    }
}
