using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 세 층을 가르고 나면 눈으로 못 잡는 버그가 둘 생긴다 —
    /// <b>배경인데 부딪힌다</b>, <b>장애물인데 배경처럼 보인다</b>.
    /// 그 둘을 기계가 잡는다.
    /// </summary>
    public class LayerContractTests
    {
        static readonly string[] Gameplay = { "CityIntact", "CityExposed", "CityCharred" };

        static LayerBlock Block(string name, bool isGameplay, bool hasCollider, string material)
            => new LayerBlock(name, x: 10f, isGameplay, hasCollider, material);

        [Test]
        public void 제대로_된_맵은_위반이_없다()
        {
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_11", isGameplay: true, hasCollider: true, "CityIntact"),
                Block("Midground_40", isGameplay: false, hasCollider: false, "Midground"),
                Block("Skyline_80", isGameplay: false, hasCollider: false, "Skyline"),
            };

            Assert.AreEqual(0, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 배경에_콜라이더가_있으면_위반이다()
        {
            //  "안 닿을 줄 알았는데 닿는다" — 플레이어가 원인을 짚을 수 없는 종류다.
            var blocks = new List<LayerBlock>
            {
                Block("Midground_40", isGameplay: false, hasCollider: true, "Midground"),
            };

            var bad = LayerContract.Check(blocks, Gameplay);

            Assert.AreEqual(1, bad.Count);
            Assert.That(bad[0].Reason.IndexOf("콜라이더", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 장애물_재질인데_콜라이더가_없으면_위반이다()
        {
            //  반대 방향 — 장애물로 칠해 놓고 통과된다.
            var blocks = new List<LayerBlock>
            {
                Block("PipeHigh_22", isGameplay: true, hasCollider: false, "CityIntact"),
            };

            Assert.AreEqual(1, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 소품은_게임_평면에_있어도_콜라이더가_없어도_된다()
        {
            //  결승 배너·동전은 게임 평면 높이에 있지만 닿으라고 둔 것이 아니다. 이걸 위반으로
            //  세면 검사가 26개를 뱉으며 쓸모없어진다(처음 돌렸을 때 실제로 그랬다).
            var blocks = new List<LayerBlock>
            {
                Block("FinishLine", isGameplay: true, hasCollider: false, "Finish"),
                Block("Coin", isGameplay: true, hasCollider: false, "Coin"),
            };

            Assert.AreEqual(0, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 게임_평면이_배경_재질을_쓰면_위반이다()
        {
            //  읽는 규칙("선명하고 테두리가 밝으면 닿는 것")이 깨지는 자리다.
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_33", isGameplay: true, hasCollider: true, "Skyline"),
            };

            var bad = LayerContract.Check(blocks, Gameplay);

            Assert.AreEqual(1, bad.Count);
            Assert.That(bad[0].Reason.IndexOf("배경처럼 읽힌다", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 재질이_없는_게임_평면도_위반이다()
        {
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_44", isGameplay: true, hasCollider: true, null),
            };

            Assert.AreEqual(1, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 배경은_아무_재질이나_써도_된다()
        {
            //  배경 재질 목록까지 강제하면 아트가 손을 못 댄다. 규약은 "닿는 것"에만 건다.
            var blocks = new List<LayerBlock>
            {
                Block("Skyline_80", isGameplay: false, hasCollider: false, "무엇이든"),
            };

            Assert.AreEqual(0, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 훑은_것이_없으면_그렇게_적는다()
        {
            //  빈 절을 찍으면 "재 봤더니 괜찮다"로 잘못 읽힌다.
            string text = LayerContract.Section(new List<LayerBlock>(), Gameplay);
            Assert.That(text.IndexOf("훑은 블록이 없다", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 위반이_없으면_층별_개수를_적는다()
        {
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_11", true, true, "CityIntact"),
                Block("Midground_40", false, false, "Midground"),
            };

            string text = LayerContract.Section(blocks, Gameplay);

            Assert.That(text.IndexOf("✅", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("게임 평면 1", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("배경 1", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 위반은_이름과_자리를_적는다()
        {
            var blocks = new List<LayerBlock> { Block("Midground_40", false, true, "Midground") };

            string text = LayerContract.Section(blocks, Gameplay);

            Assert.That(text.IndexOf("❌", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("Midground_40", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        }
    }
}
