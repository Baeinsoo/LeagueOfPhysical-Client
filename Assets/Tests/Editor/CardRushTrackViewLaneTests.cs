using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 트랙 그림의 레인 줄(최종 리뷰 m3) — 규칙보다 먼저 생긴 몸은 받은 자리를 임시 출발점으로 쓰다가 규칙이 오면 월드가 레인을 다시 구한다.
    /// 그림은 처음 본 출발점에 줄을 깔아 두므로, 출발점이 바뀌면 그 줄을 새 자리로 다시 깔아야 한다.
    /// </summary>
    public class CardRushTrackViewLaneTests
    {
        private sealed class FixedSeed : IMatchSeed { public ulong Value => 4242; }

        [Test]
        public void 레인_출발점이_바뀌면_줄을_다시_깐다()
        {
            var rules = new CardRushMapRulesField();
            rules.Set(CardRushTheme.StoneBridge, 2.6f, Vector3.zero, Vector3.forward);
            var registry = new EntityRegistry();
            var world = new CardRushWorld(registry, new WorldEventBuffer(), rules, new FixedSeed(), CardRushServerCorrectionHandlerTests.Config);
            var other = new GameFramework.World.Entity("runner-2");
            other.Add(new EntityKind(EntityType.Character));
            var runner = new CardRushRunner { Lane = 0, LaneOrigin = rules.LaneOrigin(6) + Vector3.forward * 40f };
            other.Add(runner);
            registry.Add(other);
            world.Tick(1, 0.02f);

            var view = new CardRushTrackView(world, registry, null, null, null);
            view.Start();
            try
            {
                view.PostLateTick();
                Assert.AreEqual(1, view.LaneCount);
                var lane = GameObject.Find("CardRushTrack").transform.Find("Lane runner-2");
                Assert.AreEqual(runner.LaneOrigin, lane.position);

                runner.LaneOrigin = rules.LaneOrigin(6);   // 월드가 규칙으로 다시 구한 자리
                view.PostLateTick();
                lane = GameObject.Find("CardRushTrack").transform.Find("Lane runner-2");
                Assert.AreEqual(1, view.LaneCount);
                Assert.Less((lane.position - rules.LaneOrigin(6)).magnitude, 1e-4f, "새 출발점에 다시 깔렸다");
            }
            finally
            {
                view.Dispose();
            }
        }
    }
}
