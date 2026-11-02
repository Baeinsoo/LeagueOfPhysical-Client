using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackFlightFxViewTests
    {
        [Test]
        public void 속도가_0이면_기울지_않는다()
        {
            Assert.AreEqual(0f, KnockbackFlightFxView.LeanDegrees(0f, 0f), 1e-4f);
        }

        [Test]
        public void 속도가_8_이상이면_최대로_기운다()
        {
            Assert.AreEqual(KnockbackFlightFxView.MaxLeanDegrees, KnockbackFlightFxView.LeanDegrees(8f, 0f), 1e-3f);
            Assert.AreEqual(KnockbackFlightFxView.MaxLeanDegrees, KnockbackFlightFxView.LeanDegrees(20f, 0f), 1e-3f);
            Assert.AreEqual(KnockbackFlightFxView.MaxLeanDegrees * 0.5f, KnockbackFlightFxView.LeanDegrees(4f, 0f), 1e-3f);
        }

        [Test]
        public void 날림이_끝나면_0점15초에_걸쳐_0이_된다()
        {
            float max = KnockbackFlightFxView.MaxLeanDegrees;
            Assert.AreEqual(max * 0.5f, KnockbackFlightFxView.LeanDegrees(10f, 0.075f), 1e-3f);
            Assert.AreEqual(0f, KnockbackFlightFxView.LeanDegrees(10f, 0.15f), 1e-4f);
            Assert.AreEqual(0f, KnockbackFlightFxView.LeanDegrees(10f, 1f), 1e-4f);
        }

        [Test]
        public void 축은_속도에_수직인_수평축이고_머리가_속도_반대쪽으로_젖혀진다()
        {
            var v = new Vector3(3f, 0f, 4f);
            var axis = KnockbackFlightFxView.LeanAxis(v);
            Assert.AreEqual(0f, axis.y, 1e-5f);
            Assert.AreEqual(0f, Vector3.Dot(axis, v), 1e-4f);
            Assert.AreEqual(1f, axis.magnitude, 1e-4f);
            var top = Quaternion.AngleAxis(30f, axis) * Vector3.up;
            Assert.Less(Vector3.Dot(top, v.normalized), -0.1f);   // 머리 끝이 가는 방향의 뒤쪽
            Assert.Greater(top.y, 0.5f);
        }

        [Test]
        public void 속도_방향이_없으면_축은_0이다()
        {
            Assert.AreEqual(Vector3.zero, KnockbackFlightFxView.LeanAxis(Vector3.zero));
        }

        [Test]
        public void 흙먼지는_바닥_위에서만_남긴다()
        {
            Assert.IsTrue(KnockbackFlightFxView.LeavesDust(0f));
            Assert.IsFalse(KnockbackFlightFxView.LeavesDust(-0.2f));
        }
    }
}
