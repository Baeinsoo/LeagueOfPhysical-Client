using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 진입점(spec 2026-10-27 §5): 매 장 내 새의 <b>시뮬 수</b>(<see cref="FlappyFlapCombo"/>)를 읽어 걸음(<see cref="FlapComboBeat"/>)을 알린다 —
    /// 보이는 수가 곧 고비 보상을 받는 수다. 고비는 맵 규칙으로, "대시+"는 그 고비가 게이지를 줬을 때만. 0으로 떨어지면 끊김, 결승·기절이면 달리는 중이 아니다.
    /// 이번 판 최고가 오를 때마다 기록지에 적는다.
    /// </summary>
    public class FlappyFlapStreakTests
    {
        private EntityRegistry registry;
        private FlappyStun stun;
        private FinishState finish;
        private FlappyDash dash;
        private FlappyFlapCombo combo;
        private RigGameDataStore store;
        private FlappyMapRulesField rules;
        private MatchRecordSheet sheet;
        private FlappyFlapStreak streak;
        private List<FlapComboBeat> beats;
        private int crashes;

        [SetUp]
        public void SetUp()
        {
            registry = new EntityRegistry();
            var me = new Entity("me");
            stun = new FlappyStun();
            finish = new FinishState();
            dash = new FlappyDash { Charge = 0f };
            combo = new FlappyFlapCombo();
            me.Add(stun);
            me.Add(finish);
            me.Add(dash);
            me.Add(combo);
            registry.Add(me);
            store = new RigGameDataStore { userEntityId = "me" };
            rules = new FlappyMapRulesField();
            rules.Set(chaser: false, manualDash: true, diveCharge: true);
            sheet = new MatchRecordSheet(() => (6, 2), new FakePersonalRecordStore());
            streak = Make();
            streak.Tick();   //  기준 잡기(0)
        }

        private FlappyFlapStreak Make()
        {
            var made = new FlappyFlapStreak(registry, store, rules, sheet);
            beats = new List<FlapComboBeat>();
            crashes = 0;
            made.Flapped += b => beats.Add(b);
            made.Crashed += () => crashes++;
            return made;
        }

        //  시뮬이 한 틱에 하나씩 받은 것처럼 수를 올리고 장마다 읽힌다.
        private void Climb(int to)
        {
            for (int n = combo.Count + 1; n <= to; n++)
            {
                combo.Count = n;
                streak.Tick();
            }
        }

        private FlapComboBeat BeatAt(int count) => beats.Find(b => b.Count == count);

        [Test]
        public void 시뮬_수가_오르면_오른_수마다_알린다()
        {
            Climb(3);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, beats.ConvertAll(b => b.Count));
            Assert.IsTrue(streak.Racing);
        }

        [Test]
        public void 처음_본_장은_기준만_잡는다()
        {
            combo.Count = 30;
            streak = Make();

            streak.Tick();

            Assert.IsEmpty(beats, "재접속으로 이미 30번 — 우르르 띄우지 않는다");
            Assert.AreEqual(30, streak.Count);
        }

        [Test]
        public void 크게_뛰면_되감기_교정이라_조용히_따른다()
        {
            Climb(1);
            combo.Count = 9;
            streak.Tick();

            CollectionAssert.AreEqual(new[] { 1 }, beats.ConvertAll(b => b.Count));
            Assert.AreEqual(9, streak.Count);
        }

        [Test]
        public void 영으로_떨어지면_끊김을_한_번_알린다()
        {
            Climb(3);
            stun.StunRemaining = 0.8f;
            combo.Count = 0;
            streak.Tick();
            streak.Tick();

            Assert.AreEqual(1, crashes);
            Assert.AreEqual(0, streak.Count);
            Assert.IsFalse(streak.Racing);

            stun.StunRemaining = 0f;
            Climb(1);
            Assert.AreEqual(1, beats[beats.Count - 1].Count, "다음은 +1부터");
        }

        [Test]
        public void 영이_아닌_수로_줄면_끊김이_아니다()
        {
            Climb(5);
            beats.Clear();
            combo.Count = 3;
            streak.Tick();

            Assert.AreEqual(0, crashes);
            Assert.IsEmpty(beats);
        }

        [Test]
        public void 맵_규칙의_고비에_번호와_대시_보상을_싣는다()
        {
            Climb(10);
            Assert.AreEqual(-1, BeatAt(9).Milestone);
            Assert.AreEqual(0, BeatAt(10).Milestone);
            Assert.IsTrue(BeatAt(10).DashGain);
            Assert.IsFalse(BeatAt(9).DashGain);

            FlappyFlapComboRules.TryCreate(new[] { 3 }, new[] { 1f }, out var custom);
            rules.Set(chaser: false, manualDash: true, diveCharge: true, flapCombo: custom);
            combo.Count = 0;
            streak.Tick();
            beats.Clear();
            Climb(10);
            Assert.AreEqual(0, BeatAt(3).Milestone, "이 맵의 고비는 3");
            Assert.AreEqual(-1, BeatAt(10).Milestone, "기본 고비 10은 이 맵엔 없다");
        }

        [Test]
        public void 버튼이_없는_맵이나_가득_찬_게이지면_대시_보상이_아니다()
        {
            rules.Set(chaser: false, manualDash: false);
            Climb(10);
            Assert.AreEqual(0, BeatAt(10).Milestone, "한마디는 뜬다");
            Assert.IsFalse(BeatAt(10).DashGain, "버튼이 없다 — 시뮬도 안 줬다");

            rules.Set(chaser: false, manualDash: true, diveCharge: true);
            combo.Count = 0;
            streak.Tick();
            dash.Charge = FlappyDash.MaxCharge;
            streak.Tick();   //  직전 장 게이지를 가득으로 읽힌다
            beats.Clear();
            Climb(10);
            Assert.IsFalse(BeatAt(10).DashGain, "가득이라 더해진 게 없다");
        }

        [Test]
        public void 기절하거나_결승하면_달리는_중이_아니다()
        {
            Climb(1);
            stun.StunRemaining = 0.8f;
            streak.Tick();
            Assert.IsFalse(streak.Racing);

            stun.StunRemaining = 0f;
            finish.FinishedTick = 151;
            streak.Tick();
            Assert.IsFalse(streak.Racing);
        }

        [Test]
        public void 최고가_오를_때마다_기록지에_적는다()
        {
            Climb(3);
            combo.Count = 0;
            streak.Tick();
            Climb(1);

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(FlappyRecordMetrics.FlapStreak, outcomes[0].Metric);
            Assert.AreEqual(3d, outcomes[0].Value, "끊긴 뒤 +1은 최고를 안 바꾼다");
        }

        [Test]
        public void 내_id가_비어_있으면_예외_없이_달리는_중이_아니다()
        {
            store.userEntityId = null;

            Assert.DoesNotThrow(() => streak.Tick(), "앱을 켠 뒤 첫 판에서는 GameInfoToC가 오기 전까지 id가 null이다");
            Assert.IsFalse(streak.Racing);
        }
    }
}
