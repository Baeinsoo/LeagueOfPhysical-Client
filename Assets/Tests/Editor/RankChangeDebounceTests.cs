using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 순위 숫자를 튕길지(spec §1 "0.8초 안에 다시 바뀌면", §5 "출발 2.5초 안 무시"). 시제품 규칙 그대로:
    /// 출발 2.5초가 지나고 직전 튕김에서 0.8초가 지났을 때만 알린다. 그 사이 바뀐 것은 숫자만 바뀌고 조용히 삼킨다.
    /// </summary>
    public class RankChangeDebounceTests
    {
        [Test]
        public void 처음_본_순위는_변화가_아니다()
        {
            var gate = new RankChangeDebounce();

            Assert.AreEqual(RankChange.None, gate.Observe(3, 10d, frozen: false));
        }

        [Test]
        public void 출발_2_5초_안의_변화는_무시()
        {
            var gate = new RankChangeDebounce();
            gate.Observe(3, 1.0d, false);

            Assert.AreEqual(RankChange.None, gate.Observe(2, 2.0d, false));
            Assert.AreEqual(RankChange.None, gate.Observe(3, 2.5d, false), "2.5초 정각도 아직 유예");
        }

        [Test]
        public void 오르면_Up_내리면_Down()
        {
            var gate = new RankChangeDebounce();
            gate.Observe(3, 2.6d, false);

            Assert.AreEqual(RankChange.Up, gate.Observe(2, 3.0d, false));
            Assert.AreEqual(RankChange.Down, gate.Observe(3, 4.0d, false));
        }

        [Test]
        public void _0_8초_안의_재변화는_무시()
        {
            var gate = new RankChangeDebounce();
            gate.Observe(3, 2.6d, false);
            Assert.AreEqual(RankChange.Up, gate.Observe(2, 3.0d, false));

            Assert.AreEqual(RankChange.None, gate.Observe(3, 3.5d, false), "직전 튕김 0.5초 뒤 — 삼킨다");
            Assert.AreEqual(RankChange.Up, gate.Observe(2, 3.9d, false), "0.9초 뒤 — 다시 알린다(직전 값 3에서 2로)");
        }

        [Test]
        public void 같은_순위면_변화가_아니다()
        {
            var gate = new RankChangeDebounce();
            gate.Observe(2, 5d, false);

            Assert.AreEqual(RankChange.None, gate.Observe(2, 6d, false));
        }

        [Test]
        public void 내가_결승한_뒤엔_무시()
        {
            //  결승은 시뮬이 바로 알고 등수는 0.2초 늦게 온다 — 그 사이 x 순위에서 등수로 바뀌는 것은 추월이 아니다.
            var gate = new RankChangeDebounce();
            gate.Observe(1, 30d, false);

            Assert.AreEqual(RankChange.None, gate.Observe(3, 31d, frozen: true));
        }

        [Test]
        public void 내_새가_없으면_변화가_아니다()
        {
            var gate = new RankChangeDebounce();
            gate.Observe(2, 10d, false);

            Assert.AreEqual(RankChange.None, gate.Observe(0, 11d, false));
            Assert.AreEqual(RankChange.None, gate.Observe(2, 12d, false), "0에서 돌아온 것도 변화로 안 친다");
        }
    }
}
