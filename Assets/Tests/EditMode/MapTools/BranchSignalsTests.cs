using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// <b>후보 신호가 두 갈래를 어떻게 매기고, 그 매김을 어떻게 채점하는가.</b>
    ///
    /// <para>여기서 쓰는 <see cref="BranchOutcome"/>은 손으로 지어낸 값이다 — 실제 맵에서
    /// 재는 것은 에디터 진단이 할 일이고, 이 파일이 지켜야 하는 것은 <b>매김과 채점의 산수</b>
    /// 자체다. 지어낸 값이라야 "이 신호만 갈리고 나머지는 같다"를 하나씩 따로 물어볼 수 있다.</para>
    ///
    /// <para>채점이 왜 양쪽 라벨을 다 받아야 하는지(<see cref="양쪽_라벨을_다_넣어야_반대만_말하는_신호가_안_속인다"/>)가
    /// 이 파일에서 가장 중요한 단언이다 — 그게 깨지면 측정 전체가 스스로를 속인다.</para>
    /// </summary>
    public class BranchSignalsTests
    {
        //  실제 도구가 쓰는 폭(몸 지름 0.45×2)과 같다.
        const float SameReach = 0.9f;

        //  기본값: 두 갈래가 모든 축에서 같다. 테스트마다 <b>한 축만</b> 바꿔 그 신호만 갈리게 한다.
        static BranchOutcome Flat(int alive = 30, float reach = 100f, float vy = 0f, float clearance = 2f,
                                  int openTicks = 30, bool funnelMeasured = true, bool inFunnel = false)
            => new BranchOutcome(alive, reach, vy, clearance, openTicks, funnelMeasured, inFunnel);

        static int Prefer(BranchSignal signal, in BranchOutcome flapped, in BranchOutcome coasted,
                          float epsilon = SameReach)
        {
            Assert.IsTrue(BranchSignals.TryPrefer(signal, flapped, coasted, epsilon, out int prefer),
                $"{signal}가 기권했다 — 이 경우엔 잴 수 있어야 한다.");
            return prefer;
        }

        // ── 신호가 명백한 경우에 옳은 순위를 내는가 ──────────────────────────

        [Test]
        public void 산_틱수는_더_오래_산_쪽을_높게_매긴다()
        {
            Assert.AreEqual(1, Prefer(BranchSignal.AliveTicks, Flat(alive: 60), Flat(alive: 12)));
            Assert.AreEqual(-1, Prefer(BranchSignal.AliveTicks, Flat(alive: 12), Flat(alive: 60)));
            Assert.AreEqual(0, Prefer(BranchSignal.AliveTicks, Flat(alive: 30), Flat(alive: 30)));
        }

        [Test]
        public void 도달_x는_더_멀리_간_쪽을_높게_매긴다()
        {
            Assert.AreEqual(1, Prefer(BranchSignal.ReachX, Flat(reach: 110f), Flat(reach: 100f)));
            Assert.AreEqual(-1, Prefer(BranchSignal.ReachX, Flat(reach: 100f), Flat(reach: 110f)));
        }

        [Test]
        public void 세로_속도는_올라가는_쪽을_높게_매긴다()
        {
            //  관문에서 갈린 실측 그대로 — 통과한 비행은 +17.4~+21.6, 못 지난 비행은 −14.8~−30.0.
            Assert.AreEqual(1, Prefer(BranchSignal.EndVerticalSpeed, Flat(vy: 17.4f), Flat(vy: -14.8f), 0.5f));
            Assert.AreEqual(-1, Prefer(BranchSignal.EndVerticalSpeed, Flat(vy: -30f), Flat(vy: 21.6f), 0.5f));
        }

        [Test]
        public void 세로_여유는_덜_끼인_쪽을_높게_매긴다()
        {
            Assert.AreEqual(1, Prefer(BranchSignal.EndClearance, Flat(clearance: 3f), Flat(clearance: 0.2f), 0.1f));
            Assert.AreEqual(-1, Prefer(BranchSignal.EndClearance, Flat(clearance: 0.2f), Flat(clearance: 3f), 0.1f));
        }

        [Test]
        public void 가드가_안_막은_틱수는_선택지가_더_살아_있던_쪽을_높게_매긴다()
        {
            Assert.AreEqual(1, Prefer(BranchSignal.OpenTicks, Flat(openTicks: 55), Flat(openTicks: 3)));
            Assert.AreEqual(-1, Prefer(BranchSignal.OpenTicks, Flat(openTicks: 3), Flat(openTicks: 55)));
        }

        [Test]
        public void 깔때기는_안으로_들어간_쪽을_높게_매긴다()
        {
            Assert.AreEqual(1, Prefer(BranchSignal.InFunnel, Flat(inFunnel: true), Flat(inFunnel: false)));
            Assert.AreEqual(-1, Prefer(BranchSignal.InFunnel, Flat(inFunnel: false), Flat(inFunnel: true)));
            Assert.AreEqual(0, Prefer(BranchSignal.InFunnel, Flat(inFunnel: true), Flat(inFunnel: true)));
        }

        [Test]
        public void 깔때기를_못_잰_갈래가_있으면_기권한다_아니다가_아니다()
        {
            //  "못 쟀다"를 "아니다"로 뭉개면 관문까지 못 간 갈래가 전부 나쁜 쪽으로 매겨진다.
            Assert.IsFalse(BranchSignals.TryPrefer(BranchSignal.InFunnel,
                Flat(funnelMeasured: false), Flat(inFunnel: true), SameReach, out _),
                "관문까지 못 간 갈래가 있는데도 깔때기 신호가 답을 냈다.");
            Assert.IsFalse(BranchSignals.TryPrefer(BranchSignal.InFunnel,
                Flat(inFunnel: true), Flat(funnelMeasured: false), SameReach, out _));
        }

        // ── 지금 기준은 봇이 쓰는 그 함수여야 한다 ──────────────────────────

        [Test]
        public void 지금_기준은_산_틱수가_먼저고_같을_때만_도달_x를_본다()
        {
            //  ① 산 틱수가 갈리면 도달 x가 반대라도 산 틱수가 이긴다.
            Assert.AreEqual(1, Prefer(BranchSignal.Current,
                Flat(alive: 40, reach: 100f), Flat(alive: 20, reach: 200f)));
            //  ② 산 틱수가 같을 때만 도달 x가 결정한다.
            Assert.AreEqual(-1, Prefer(BranchSignal.Current,
                Flat(alive: 30, reach: 100f), Flat(alive: 30, reach: 200f)));
        }

        [Test]
        public void 지금_기준의_답은_봇이_쓰는_함수와_한_글자도_다르지_않다()
        {
            //  베껴 두면 둘이 조용히 갈라져 "지금 기준의 적중률"이 지금 기준의 것이 아니게 된다.
            var flapped = Flat(alive: 30, reach: 101.2f);
            var coasted = Flat(alive: 30, reach: 100f);
            Assert.AreEqual(
                BotRollout.Prefer(flapped.AsRolloutBranch(), coasted.AsRolloutBranch(), SameReach),
                Prefer(BranchSignal.Current, flapped, coasted));
        }

        // ── 동점 판정이 경계에서 맞는가 ──────────────────────────────────────

        [Test]
        public void 동점_문턱은_초과라야_우열이_난다()
        {
            //  <b>0에서 잰다.</b> 100 근처에서 100f+0.9f를 만들면 뺄셈이 0.9000015…가 되어
            //  "문턱과 정확히 같은 차이"를 float으로 지을 수가 없다(이 테스트를 처음 100에서
            //  쓰고 실제로 빨강을 봤다). 경계를 묻는 테스트는 경계를 정확히 지을 수 있는
            //  자릿수에서 물어야 한다.
            Assert.AreEqual(0, Prefer(BranchSignal.ReachX, Flat(reach: SameReach), Flat(reach: 0f)),
                "폭과 정확히 같은 차이는 동점이어야 한다 — '이상'으로 두면 잡음이 우열로 세어진다.");
            //  폭을 넘으면 우열이 난다.
            Assert.AreEqual(1, Prefer(BranchSignal.ReachX, Flat(reach: SameReach + 0.01f), Flat(reach: 0f)));
            Assert.AreEqual(-1, Prefer(BranchSignal.ReachX, Flat(reach: 0f), Flat(reach: SameReach + 0.01f)));
        }

        [Test]
        public void 정수_신호는_문턱을_안_본다()
        {
            //  폭을 아무리 크게 줘도 산 틱수 1틱 차이는 우열이다.
            Assert.AreEqual(1, Prefer(BranchSignal.AliveTicks, Flat(alive: 31), Flat(alive: 30), epsilon: 999f));
        }

        // ── 적중률 계산이 맞는가 ────────────────────────────────────────────

        [Test]
        public void 적중_동점_거꾸로를_각각_세고_분모는_입을_연_틱이다()
        {
            var ticks = new List<ScoredTick>
            {
                //  적중 — 누르는 쪽이 오래 살았고 정답도 누르는 쪽.
                new ScoredTick(Flat(alive: 50), Flat(alive: 10), GoodBranch.Flap),
                //  거꾸로 — 누르는 쪽이 오래 살았는데 정답은 안 누르는 쪽.
                new ScoredTick(Flat(alive: 50), Flat(alive: 10), GoodBranch.Coast),
                //  동점.
                new ScoredTick(Flat(alive: 30), Flat(alive: 30), GoodBranch.Flap),
                //  적중 — 반대 방향도 센다.
                new ScoredTick(Flat(alive: 10), Flat(alive: 50), GoodBranch.Coast),
            };
            SignalTally tally = BranchSignals.Score(BranchSignal.AliveTicks, ticks, SameReach);
            Assert.AreEqual(2, tally.Hits);
            Assert.AreEqual(1, tally.Ties);
            Assert.AreEqual(1, tally.Misses);
            Assert.AreEqual(0, tally.Abstained);
            Assert.AreEqual(4, tally.Ranked);
            Assert.AreEqual(0.5f, tally.HitRate, 1e-6f);
            Assert.AreEqual(0.25f, tally.TieRate, 1e-6f);
            Assert.AreEqual(0.25f, tally.MissRate, 1e-6f);
        }

        [Test]
        public void 기권한_틱은_분모에서_빠진다()
        {
            var ticks = new List<ScoredTick>
            {
                new ScoredTick(Flat(inFunnel: true), Flat(inFunnel: false), GoodBranch.Flap),
                //  둘 중 하나가 관문까지 못 갔다 — 기권.
                new ScoredTick(Flat(funnelMeasured: false), Flat(inFunnel: false), GoodBranch.Flap),
            };
            SignalTally tally = BranchSignals.Score(BranchSignal.InFunnel, ticks, SameReach);
            Assert.AreEqual(1, tally.Abstained);
            Assert.AreEqual(1, tally.Ranked);
            Assert.AreEqual(2, tally.Total);
            //  기권을 분모에 넣었다면 50%가 나온다 — 말수가 적은 신호가 공짜로 나빠지면 안 된다.
            Assert.AreEqual(1f, tally.HitRate, 1e-6f);
        }

        [Test]
        public void 양쪽_라벨을_다_넣어야_반대만_말하는_신호가_안_속인다()
        {
            //  <b>이 파일에서 가장 중요한 단언.</b> "뒤집었더니 더 갔다"인 틱만 모으면 정답이
            //  언제나 "안 고른 쪽"이라, 지금 기준의 반대만 말하는 <b>빈 신호</b>가 100%를 받는다.
            //  아래는 지금 기준이 늘 '누르는 쪽'을 고르는 네 틱이다.
            var flapWins = Flat(alive: 50);
            var coastLoses = Flat(alive: 10);

            //  ① 한쪽 라벨만 — 정답이 전부 '안 고른 쪽(=Coast)'이다.
            var oneSided = new List<ScoredTick>
            {
                new ScoredTick(flapWins, coastLoses, GoodBranch.Coast),
                new ScoredTick(flapWins, coastLoses, GoodBranch.Coast),
            };
            Assert.AreEqual(0f, BranchSignals.Score(BranchSignal.Current, oneSided, SameReach).HitRate, 1e-6f,
                "지금 기준이 0%가 나와야 한다 — 한쪽 라벨만 모으면 기준은 무조건 틀린 것으로 보인다.");
            //  그 자리에서 '반대만 말하는 빈 신호'는 100%가 된다. 여기서는 지금 기준을 뒤집은
            //  것과 같은 산 틱수 신호를 갈래를 바꿔 넣어 흉내 낸다.
            var mirrored = new List<ScoredTick>
            {
                new ScoredTick(coastLoses, flapWins, GoodBranch.Coast),
                new ScoredTick(coastLoses, flapWins, GoodBranch.Coast),
            };
            Assert.AreEqual(1f, BranchSignals.Score(BranchSignal.AliveTicks, mirrored, SameReach).HitRate, 1e-6f,
                "한쪽 라벨만 있으면 내용 없는 신호가 100%를 받는다 — 그래서 양쪽 라벨이 필요하다.");

            //  ② 양쪽 라벨 — 지금 기준이 절반을 맞힌다. 이제 비율이 뜻을 갖는다.
            var twoSided = new List<ScoredTick>
            {
                new ScoredTick(flapWins, coastLoses, GoodBranch.Coast),
                new ScoredTick(flapWins, coastLoses, GoodBranch.Flap),
            };
            Assert.AreEqual(0.5f, BranchSignals.Score(BranchSignal.Current, twoSided, SameReach).HitRate, 1e-6f);
        }

        // ── 동점 세기(②) ───────────────────────────────────────────────────

        [Test]
        public void 동점_세기는_라벨_없이_굴려_본_모든_틱에서_센다()
        {
            var flapped = new List<BranchOutcome> { Flat(alive: 30), Flat(alive: 50), Flat(alive: 30) };
            var coasted = new List<BranchOutcome> { Flat(alive: 30), Flat(alive: 10), Flat(alive: 30) };
            Assert.AreEqual(2, BranchSignals.CountTies(BranchSignal.Current, flapped, coasted, SameReach));
        }

        [Test]
        public void 갈래가_짝이_안_맞으면_던진다()
        {
            //  조용히 짧은 쪽까지만 세면 "동점 비율"의 분모가 소리 없이 줄어든다.
            var flapped = new List<BranchOutcome> { Flat(), Flat() };
            var coasted = new List<BranchOutcome> { Flat() };
            Assert.Throws<System.ArgumentException>(
                () => BranchSignals.CountTies(BranchSignal.Current, flapped, coasted, SameReach));
        }
    }
}
