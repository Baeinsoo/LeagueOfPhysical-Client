using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LOP.Tests
{
    /// <summary>
    /// 클라 몸의 레인 번호 — 첫 장 씨앗이라 서버(스폰 자리에서 구함)와 갈리면 내 카드 예측이 매번 틀려 끝없이 되감는다.
    /// 받은 자리는 "지금" 자리(판 도중이면 한참 앞·높이 섞임)라 옆 거리만으로 구해야 한다.
    /// </summary>
    public class CardRushRunnerCreatorTests
    {
        private static CardRushMapRulesField Rules()
        {
            var rules = new CardRushMapRulesField();
            //  비스듬한 맵 — 앞 방향이 축과 나란하지 않아도 옆 거리만 본다.
            rules.Set(CardRushTheme.StoneBridge, 2.6f, new Vector3(5f, 1f, -3f), new Vector3(1f, 0f, 2f));
            return rules;
        }

        [Test]
        public void 앞으로_간_자리에서도_서버와_같은_레인과_출발점()
        {
            var rules = Rules();
            var forward = CardRushLanes.Flat(rules.Forward);
            for (int lane = 0; lane < CardRushLanes.MaxLanes; lane++)
            {
                var received = rules.LaneOrigin(lane) + forward * 30f + Vector3.up * 0.5f;
                var runner = CardRushRunnerCreator.NewRunner(rules, received);
                Assert.AreEqual(lane, runner.Lane, $"레인 {lane}");
                Assert.AreEqual(rules.LaneOrigin(lane), runner.LaneOrigin, $"레인 {lane} 출발점");
                Assert.IsFalse(runner.LanePending);
            }
        }

        [Test]
        public void 규칙이_없으면_받은_자리를_쓰고_레인은_나중에_정한다()
        {
            var position = new Vector3(3f, 0f, 40f);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CardRushRunnerCreator"));
            var runner = CardRushRunnerCreator.NewRunner(new CardRushMapRulesField(), position);
            Assert.AreEqual(position, runner.LaneOrigin);
            Assert.IsTrue(runner.LanePending, "규칙이 오면 월드가 다시 구해야 한다 — 0으로 굳으면 서버와 첫 장이 갈린다");
        }
    }
}
