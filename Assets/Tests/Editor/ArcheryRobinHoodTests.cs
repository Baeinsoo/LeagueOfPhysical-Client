using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryRobinHoodTests
    {
        [Test]
        public void 이cm_안이면_그_화살을_쪼갠다()
        {
            var earlier = new List<Vector3> { new Vector3(0.1f, 0f, 0f), new Vector3(0.015f, 0f, 0f) };
            var split = new List<bool> { false, false };
            Assert.AreEqual(1, ArcheryRobinHood.Splits(Vector3.zero, earlier, split));
        }

        [Test]
        public void 이cm_밖이면_없다()
        {
            var earlier = new List<Vector3> { new Vector3(0.025f, 0f, 0f) };
            Assert.AreEqual(-1, ArcheryRobinHood.Splits(Vector3.zero, earlier, new List<bool> { false }));
        }

        [Test]
        public void 여럿이면_가장_가까운_것()
        {
            var earlier = new List<Vector3> { new Vector3(0.018f, 0f, 0f), new Vector3(0f, 0.005f, 0f) };
            Assert.AreEqual(1, ArcheryRobinHood.Splits(Vector3.zero, earlier, new List<bool> { false, false }));
        }

        [Test]
        public void 이미_쪼개진_화살은_다시_안_쪼갠다()
        {
            var earlier = new List<Vector3> { new Vector3(0f, 0.005f, 0f), new Vector3(0.018f, 0f, 0f) };
            Assert.AreEqual(1, ArcheryRobinHood.Splits(Vector3.zero, earlier, new List<bool> { true, false }));
            Assert.AreEqual(-1, ArcheryRobinHood.Splits(Vector3.zero, earlier, new List<bool> { true, true }));
        }

        [Test]
        public void 먼저_꽂힌_화살이_없으면_없다()
        {
            Assert.AreEqual(-1, ArcheryRobinHood.Splits(Vector3.zero, new List<Vector3>(), new List<bool>()));
        }
    }
}
