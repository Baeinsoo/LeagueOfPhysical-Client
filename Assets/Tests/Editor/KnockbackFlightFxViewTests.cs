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

        [TestCase(9.9, 10L, true)]
        [TestCase(10.0, 10L, false)]
        [TestCase(10.5, 10L, false)]
        public void 그리는_시각이_끝_틱_전이면_날림(double drawnTick, long endTick, bool expected)
        {
            Assert.AreEqual(expected, KnockbackFlightFxView.IsFlyingAt(drawnTick, endTick));
        }

        [Test]
        public void 남의_몸은_맞기_전_그려지는_동안엔_날림이_아니다()
        {
            // 끝 틱 20, 날림 8틱 → 시작 12. 그려진 시각 11.9는 아직 맞기 전이다.
            Assert.IsFalse(KnockbackFlightFxView.IsFlyingAt(11.9, 20L, 12L));
            Assert.IsTrue(KnockbackFlightFxView.IsFlyingAt(12.0, 20L, 12L));
            Assert.IsFalse(KnockbackFlightFxView.IsFlyingAt(20.0, 20L, 12L));
        }

        [Test]
        public void 각도는_한_프레임에_튀지_않고_다가간다()
        {
            float up = KnockbackFlightFxView.ApproachDegrees(0f, 35f, 0.016f);
            Assert.Greater(up, 0f);
            Assert.Less(up, 10f);
            // 풀리는 중에 다시 맞으면 지금 각도(12도)에서 이어서 오른다.
            float again = KnockbackFlightFxView.ApproachDegrees(12f, 35f, 0.016f);
            Assert.Greater(again, 12f);
            Assert.Less(again, 22f);
            Assert.AreEqual(0f, KnockbackFlightFxView.ApproachDegrees(1f, 0f, 1f), 1e-5f);
        }
    }
}
