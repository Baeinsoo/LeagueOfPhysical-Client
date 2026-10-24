using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class MountedPredictedHitsTests
    {
        private static MountedPredictedHits Make() => new MountedPredictedHits(null, null, null, null, null, null);

        private static float? NoObstacle(Vector3 from, Vector3 to) => null;

        // 몸은 원점, 머리는 +z. 화살은 -z 10m 앞에서 +z로 40m/s.
        private static ArcheryShot Shot(string shooter)
            => new ArcheryShot(shooter, 1, new Vector3(0f, 2.4f, -10f), new Vector3(0f, 0f, 40f));

        private static readonly List<MountedPredictedHits.Target> Bodies = new List<MountedPredictedHits.Target>
        {
            new MountedPredictedHits.Target("other", Vector3.zero, 0f),
        };

        [Test]
        public void 내_화살이_그려진_남의_몸을_지나면_한_번_맞음()
        {
            var hits = Make();
            var shot = Shot("me");

            // 0.2초: 아직 몸 앞. 0.25초: 몸 한가운데를 지난다.
            Assert.IsFalse(hits.Evaluate(shot, "me", 0.2f, 0.02f, Bodies, NoObstacle, out _));
            Assert.IsFalse(hits.IsSpent("me", 1));
            Assert.IsTrue(hits.Evaluate(shot, "me", 0.25f, 0.05f, Bodies, NoObstacle, out Vector3 point));
            Assert.IsTrue(hits.IsSpent("me", 1));
            Assert.AreEqual(-0.62f, point.z, 0.9f);

            // 이미 쓴 화살은 몸 안에 있어도 다시 맞지 않는다.
            Assert.IsFalse(hits.Evaluate(shot, "me", 0.27f, 0.02f, Bodies, NoObstacle, out _));
        }

        [Test]
        public void 남의_화살은_예측하지_않는다()
        {
            var hits = Make();
            var shot = Shot("other");

            Assert.IsFalse(hits.Evaluate(shot, "me", 0.25f, 0.05f, Bodies, NoObstacle, out _));
            Assert.IsFalse(hits.IsSpent("other", 1));
        }

        [Test]
        public void 내_몸은_맞히지_않는다()
        {
            var hits = Make();
            var self = new List<MountedPredictedHits.Target> { new MountedPredictedHits.Target("me", Vector3.zero, 0f) };

            Assert.IsFalse(hits.Evaluate(Shot("me"), "me", 0.25f, 0.05f, self, NoObstacle, out _));
        }

        [Test]
        public void 바위가_먼저_막으면_예측도_안_맞는다()
        {
            var hits = Make();

            // 바위가 구간 앞쪽(0.3)에 있고 몸은 그보다 뒤에서 닿는다.
            Assert.IsFalse(hits.Evaluate(Shot("me"), "me", 0.25f, 0.05f, Bodies, (a, b) => 0.3f, out _));
            Assert.IsFalse(hits.IsSpent("me", 1));
        }

        [Test]
        public void 몸이_바위보다_가까우면_맞는다()
        {
            var hits = Make();

            Assert.IsTrue(hits.Evaluate(Shot("me"), "me", 0.25f, 0.05f, Bodies, (a, b) => 0.99f, out _));
        }

        [Test]
        public void 꽂힌_화살은_다시_재지_않는다()
        {
            var hits = Make();

            // 사람이 없는 구간에서 바위에 꽂힘.
            Assert.IsFalse(hits.Evaluate(Shot("me"), "me", 0.1f, 0.05f, Bodies, (a, b) => 0.5f, out _));
            // 이후 몸 한가운데를 지나가도(장애물 검사가 없어도) 더는 맞지 않는다.
            Assert.IsFalse(hits.Evaluate(Shot("me"), "me", 0.25f, 0.05f, Bodies, NoObstacle, out _));
            Assert.IsFalse(hits.IsSpent("me", 1));
        }
    }
}
