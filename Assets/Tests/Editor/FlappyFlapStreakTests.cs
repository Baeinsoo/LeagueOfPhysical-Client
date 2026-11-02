using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 진입점(spec 2026-10-27 §5): 매 장 내 새의 <b>시뮬 수</b>(<see cref="FlappyFlapCombo"/>)를 읽어 걸음(<see cref="FlapComboBeat"/>)을 알린다 —
    /// 보이는 수가 곧 고비 보상을 받는 수다. 게이지를 주는 걸음(맵 규칙 간격)마다 단계를, "대시+"는 게이지를 실제로 줬을 때만. 0으로 떨어지면 끊김, 결승·기절이면 달리는 중이 아니다.
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
        public void 게이지를_주는_걸음마다_단계와_대시_보상을_싣는다()
        {
            //  11-02 개정 — 10마다 모든 걸음이 10처럼(한마디·대시+). 단계는 10~20·30~40·50~90·100부터.
            Climb(60);
            Assert.AreEqual(-1, BeatAt(9).Tier);
            Assert.AreEqual(0, BeatAt(10).Tier);
            Assert.IsTrue(BeatAt(10).DashGain, "10은 0.06칸");
            Assert.AreEqual(0, BeatAt(20).Tier, "20도 한마디");
            Assert.IsTrue(BeatAt(20).DashGain, "20도 게이지를 줬다");
            Assert.AreEqual(-1, BeatAt(25).Tier);
            Assert.IsFalse(BeatAt(25).DashGain);
            Assert.AreEqual(1, BeatAt(30).Tier);
            Assert.AreEqual(1, BeatAt(40).Tier);
            Assert.AreEqual(2, BeatAt(50).Tier);
            Assert.AreEqual(2, BeatAt(60).Tier);
            Assert.IsTrue(BeatAt(60).DashGain);
            Assert.IsFalse(BeatAt(40).Big);
            Assert.IsTrue(BeatAt(50).Big, "50은 크게");
            Assert.IsFalse(BeatAt(60).Big);
            Assert.AreEqual(1, beats.FindAll(b => b.Count == 20).Count, "걸음 하나에 한 번");
        }

        [Test]
        public void 보상_없는_맵의_수는_단계가_없다()
        {
            //  M5 — 맵 규칙을 본다. 기본 규칙을 보면 10이 0단계다.
            rules.Set(chaser: false, manualDash: true, diveCharge: true, flapCombo: FlappyFlapComboRules.Off);
            Climb(10);
            Assert.AreEqual(-1, BeatAt(10).Tier);
            Assert.IsFalse(BeatAt(10).DashGain);
        }

        [Test]
        public void 백부터는_전설이고_50마다만_크게()
        {
            Climb(201);
            Assert.AreEqual(3, BeatAt(100).Tier);
            Assert.IsTrue(BeatAt(100).Big);
            Assert.AreEqual(3, BeatAt(110).Tier, "110도 게이지 걸음");
            Assert.IsTrue(BeatAt(110).DashGain);
            Assert.IsFalse(BeatAt(110).Big);
            Assert.IsTrue(BeatAt(150).Big);
            Assert.IsTrue(BeatAt(200).Big);
            Assert.AreEqual(3, BeatAt(160).Tier);
            Assert.AreEqual(-1, BeatAt(149).Tier);
            Assert.AreEqual(-1, BeatAt(201).Tier);
        }

        private int StepBeats(int count) => beats.FindAll(b => b.Count == count && b.Tier >= 0).Count;

        [Test]
        public void 교정으로_내려갔다_다시_오른_걸음은_한_번만_알린다()
        {
            //  리뷰 A1 — 20에서 알리고, 서버 교정으로 19가 됐다가 다시 20이 돼도 한마디·화음·대시+는 한 번(게이지도 한 번 들어갔다).
            Climb(20);
            combo.Count = 19;
            streak.Tick();
            Climb(20);

            Assert.AreEqual(1, StepBeats(20));
            Assert.AreEqual(0, crashes, "20 → 19는 끊김이 아니다");
        }

        [Test]
        public void 교정이_걸음을_건너뛰면_넘은_가장_높은_걸음을_한_번_알린다()
        {
            //  리뷰 A2 — 19에서 교정으로 25가 되면 시뮬은 20에서 게이지를 줬다 — 20을 한 번 알린다. 여러 걸음을 넘으면 가장 높은 것 하나만.
            Climb(19);
            combo.Count = 25;
            streak.Tick();
            Assert.AreEqual(1, beats.FindAll(b => b.Tier >= 0 && b.Count > 10).Count, "10 뒤로는 20 하나");
            Assert.AreEqual(1, StepBeats(20));

            combo.Count = 47;   //  30·40을 넘었다 — 40만
            streak.Tick();
            Assert.AreEqual(0, StepBeats(30));
            Assert.AreEqual(1, StepBeats(40));
            Assert.AreEqual(1, beats.FindAll(b => b.Tier >= 0 && b.Count == 40 && b.DashGain).Count, "게이지도 줬다");

            Climb(50);
            Assert.AreEqual(1, StepBeats(50), "그 뒤 걸음은 평소대로");
        }

        [Test]
        public void 진짜로_끊기면_같은_걸음을_다시_알린다()
        {
            Climb(10);
            stun.StunRemaining = 0.8f;
            combo.Count = 0;
            streak.Tick();
            stun.StunRemaining = 0f;
            Climb(10);

            Assert.AreEqual(2, StepBeats(10), "기절 → 0 → 다시 10");
        }

        [Test]
        public void 처음_본_장의_수까지는_알리지_않는다()
        {
            //  재접속으로 이미 30 — 30을 알리지 않고, 다음 걸음 40부터.
            combo.Count = 30;
            streak = Make();
            streak.Tick();
            Climb(40);

            Assert.AreEqual(0, StepBeats(30));
            Assert.AreEqual(1, StepBeats(40));
        }

        [Test]
        public void 보상이_0인_걸음은_한마디만_뜨고_대시_보상은_아니다()
        {
            //  마커가 간격은 두되 칸을 0으로 줄 수 있다(TryCreate은 0을 허용) — 이땐 한마디(화면)는 떠도
            //  시뮬이 게이지를 준 게 아니니 "대시+"가 붙으면 거짓말이다. 버튼 있음·게이지 안 가득인 채로 본다.
            FlappyFlapComboRules.TryCreate(10, new[] { 0f }, 0f, out var custom);
            rules.Set(chaser: false, manualDash: true, diveCharge: true, flapCombo: custom);
            combo.Count = 0;
            streak.Tick();
            beats.Clear();

            Climb(10);

            Assert.AreEqual(0, BeatAt(10).Tier, "한마디는 뜬다");
            Assert.IsFalse(BeatAt(10).DashGain, "이 고비의 보상은 0이라 대시+는 아니다");
        }

        [Test]
        public void 버튼이_없는_맵이나_가득_찬_게이지면_대시_보상이_아니다()
        {
            rules.Set(chaser: false, manualDash: false);
            Climb(30);
            Assert.AreEqual(1, BeatAt(30).Tier, "한마디는 뜬다");
            Assert.IsFalse(BeatAt(30).DashGain, "버튼이 없다 — 시뮬도 안 줬다");

            rules.Set(chaser: false, manualDash: true, diveCharge: true);
            combo.Count = 0;
            streak.Tick();
            dash.Charge = FlappyDash.MaxCharge;
            streak.Tick();   //  직전 장 게이지를 가득으로 읽힌다
            beats.Clear();
            Climb(30);
            Assert.IsFalse(BeatAt(30).DashGain, "가득이라 더해진 게 없다");
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
